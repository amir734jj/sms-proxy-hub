using System.Reflection;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http.Extensions;
using Api.Data;
using Api.Data.Entities;
using Api.Interfaces;
using Api.Middleware;
using Api.Providers;
using Api.Utilities;
using EfCoreRepository.Extensions;
using FluentMigrator.Runner;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("System", LogEventLevel.Error)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .Enrich.WithProperty("Application", "sms-proxy-hub")
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        path: "logs/api-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7,
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

builder.Host.UseSerilog();

builder.WebHost.ConfigureKestrel(serverOptions =>
{
    var portConfig = builder.Configuration.GetValue<string>("PORT");
    var port = !string.IsNullOrEmpty(portConfig) && int.TryParse(portConfig, out var p) ? p : 5000;
    serverOptions.ListenAnyIP(port);
});

var connectionString = ConnectionStringUtility.ConnectionStringUrlToPgResource(
    builder.Configuration.GetValue<string>("DATABASE_URL")!);

builder.Services.AddDbContext<AppDbContext>(opt => opt.UseNpgsql(connectionString));

builder.Services
    .AddFluentMigratorCore()
    .ConfigureRunner(rb => rb
        .AddPostgres()
        .WithGlobalConnectionString(connectionString)
        .ScanIn(Assembly.Load("Migrations")).For.Migrations());

builder.Services
    .AddIdentity<User, Role>(opt =>
    {
        opt.Password.RequiredLength = 8;
        opt.Password.RequireNonAlphanumeric = false;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddEfRepository<AppDbContext>(x =>
{
    x.Profile(Assembly.GetAssembly(typeof(AppDbContext)));
});

builder.Services
    .AddAuthentication(opt =>
    {
        opt.DefaultAuthenticateScheme = "Multi";
        opt.DefaultChallengeScheme = "Multi";
    })
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, opt =>
    {
        opt.MapInboundClaims = false;
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)),
            RoleClaimType = "role"
        };
    })
    .AddScheme<AuthenticationSchemeOptions, ApiTokenAuthHandler>(ApiTokenAuthHandler.SchemeName, _ => { })
    .AddPolicyScheme("Multi", "JWT or API Token", opt =>
    {
        opt.ForwardDefaultSelector = context =>
        {
            var authHeader = context.Request.Headers.Authorization.ToString();
            if (!string.IsNullOrWhiteSpace(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                var token = authHeader["Bearer ".Length..].Trim();
                // JWTs have dots, API tokens don't
                return token.Contains('.') ? JwtBearerDefaults.AuthenticationScheme : ApiTokenAuthHandler.SchemeName;
            }

            return JwtBearerDefaults.AuthenticationScheme;
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(opt =>
{
    opt.AddFixedWindowLimiter("login", w =>
    {
        w.Window = TimeSpan.FromMinutes(1);
        w.PermitLimit = 10;
        w.QueueLimit = 0;
        w.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });
    opt.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});
builder.Services.AddMemoryCache();
builder.Services.AddHealthChecks();

builder.Services.AddControllers().AddNewtonsoftJson();

// Register SMS providers
builder.Services.AddSingleton<SmsGateProvider>();
builder.Services.AddSingleton<ISmsProvider>(sp => sp.GetRequiredService<SmsGateProvider>());
builder.Services.AddSingleton<ISmsProvider, TwilioProvider>();
builder.Services.AddSingleton<ISmsProviderFactory, SmsProviderFactory>();

// Auto-register services via Scrutor
builder.Services.Scan(scan => scan
    .FromAssemblies(Assembly.Load("Api"))
    .AddClasses()
    .UsingRegistrationStrategy(Scrutor.RegistrationStrategy.Skip)
    .AsMatchingInterface()
    .WithScopedLifetime());

builder.Services.AddHttpClient();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(opt =>
{
    opt.SwaggerDoc("v1", new OpenApiInfo { Title = "SMS Proxy Hub API", Version = "v1" });
    opt.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT token or API token"
    });
    opt.AddSecurityRequirement(_ => new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecuritySchemeReference("Bearer"),
            []
        }
    });
});

builder.Services.AddCors(opt =>
    opt.AddDefaultPolicy(p => p
        .WithOrigins(
            builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [])
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()));

var app = builder.Build();

// Run pending FluentMigrator migrations
using var migrationScope = app.Services.CreateScope();
migrationScope.ServiceProvider.GetRequiredService<IMigrationRunner>().MigrateUp();

app.UseBlazorFrameworkFiles();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers.CacheControl = "public,max-age=3600";
    }
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "SMS Proxy Hub API v1"));
}

app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseMiddleware<ActiveUserMiddleware>();
app.UseAuthorization();
app.UseSerilogRequestLogging();
app.MapControllers();
app.MapHealthChecks("/api/health").AllowAnonymous();

app.MapFallback("api/{**rest}", async context =>
{
    context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
    await context.Response.WriteAsync(
        $"Failed to find the endpoint for {context.Request.Method}:{context.Request.GetDisplayUrl()}");
});

if (app.Environment.IsDevelopment())
{
    app.MapFallback(() => Results.Text("SMS Proxy Hub API server is running."));
}
else
{
    app.MapFallbackToFile("index.html");
}

await app.RunAsync();
