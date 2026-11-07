using System.Security.Claims;
using LoanSystem.Common.Core.Ports.Outbound;
using Microsoft.AspNetCore.Http;

namespace LoanSystem.Common.Adapters.Http;

public sealed class CurrentUserProvider : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public Guid? UserId
    {
        get
        {
            var claim = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                     ?? User?.FindFirst("sub")?.Value;
            return Guid.TryParse(claim, out var id) ? id : null;
        }
    }

    public string? Email => User?.FindFirst(ClaimTypes.Email)?.Value;

    public IReadOnlyCollection<string> Roles => User?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray()
                                             ?? Array.Empty<string>();

    public Guid? CustomerId
    {
        get
        {
            var claim = User?.FindFirst("customer_id")?.Value;
            return Guid.TryParse(claim, out var id) ? id : null;
        }
    }

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public bool IsInRole(string role) => User?.IsInRole(role) ?? false;
}
