using System.Net;

namespace LoanSystem.Modules.Security.Core.Ports.Outbound;

/// <summary>Exposes the originating client's IP and user-agent for device-bound refresh tokens (BE-SEC-01).</summary>
internal interface IClientInfoProvider
{
    IPAddress? IpAddress { get; }
    string? UserAgent { get; }
}
