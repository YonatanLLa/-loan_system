using System.Net;
using LoanSystem.Common.Core.Ports.Outbound;
using Microsoft.AspNetCore.Http;

namespace LoanSystem.Common.Adapters.Http;

/// <summary>Reads originating client IP address and User-Agent header from <see cref="IHttpContextAccessor"/>.</summary>
public sealed class HttpClientInfoProvider : IClientInfoProvider
{
    private readonly IHttpContextAccessor _accessor;

    public HttpClientInfoProvider(IHttpContextAccessor accessor)
    {
        _accessor = accessor ?? throw new ArgumentNullException(nameof(accessor));
    }

    public IPAddress? IpAddress => _accessor.HttpContext?.Connection.RemoteIpAddress;

    public string? UserAgent
    {
        get
        {
            var raw = _accessor.HttpContext?.Request.Headers.UserAgent.ToString();
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            return raw.Length > 512 ? raw[..512] : raw;
        }
    }
}
