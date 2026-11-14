using System.Net;

namespace LoanSystem.Common.Core.Ports.Outbound;

/// <summary>
/// Provides metadata (originating IP address and HTTP user-agent) for the active request, if any.
/// </summary>
public interface IClientInfoProvider
{
    IPAddress? IpAddress { get; }
    string? UserAgent { get; }
}
