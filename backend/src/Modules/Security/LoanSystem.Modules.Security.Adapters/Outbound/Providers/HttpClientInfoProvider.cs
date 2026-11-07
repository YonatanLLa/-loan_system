using System.Net;
using LoanSystem.Modules.Security.Core.Ports.Outbound;
using Microsoft.AspNetCore.Http;

namespace LoanSystem.Modules.Security.Adapters.Outbound.Providers;

/// <summary>Reads the originating client's IP and user-agent from the current HTTP request (BE-SEC-01).</summary>
internal sealed class HttpClientInfoProvider : IClientInfoProvider
{
    private readonly IHttpContextAccessor _accessor;

    public HttpClientInfoProvider(IHttpContextAccessor accessor) => _accessor = accessor;

    public IPAddress? IpAddress => _accessor.HttpContext?.Connection.RemoteIpAddress;

    public string? UserAgent
    {
        get
        {
            var userAgent = _accessor.HttpContext?.Request.Headers.UserAgent.ToString();
            return string.IsNullOrWhiteSpace(userAgent) ? null : userAgent;
        }
    }
}
