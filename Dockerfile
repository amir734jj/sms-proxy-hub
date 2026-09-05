FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
ARG BUILD_CONFIGURATION=Release

WORKDIR /src
COPY . .

RUN dotnet restore && \
    dotnet publish UI/UI.csproj -c Release -o /publish/ui --no-restore -p:SkipNSwag=true && \
    dotnet publish Api/Api.csproj -c Release -o /publish/api --no-restore -p:SkipNSwag=true

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final

WORKDIR /app
COPY --from=build /publish/api .
COPY --from=build /publish/ui/wwwroot ./wwwroot

ENV ASPNETCORE_URLS=http://+:80
ENV PORT=80
ENV ASPNETCORE_ENVIRONMENT=Production

RUN apk add --no-cache tzdata krb5-libs curl

HEALTHCHECK --interval=30s --timeout=5s --start-period=30s --retries=3 \
    CMD curl --fail --silent --show-error http://127.0.0.1:80/api/health || exit 1

ENTRYPOINT ["dotnet", "Api.dll"]
