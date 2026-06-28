using Shared.Contracts;

namespace Api.Interfaces;

public interface IDeviceStatusService
{
    // Records that a device is alive (from any inbound webhook) and captures health from system:ping.
    void Record(Guid connectionId, string? rawBody);

    DeviceStatusDto? Get(Guid connectionId);
}
