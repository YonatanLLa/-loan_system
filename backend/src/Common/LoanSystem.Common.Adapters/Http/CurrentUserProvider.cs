using System.Security.Claims;
using LoanSystem.Common.Core.Ports.Outbound;
using Microsoft.AspNetCore.Http;

namespace LoanSystem.Common.Adapters.Http;

/// <summary>
/// Adaptador que implementa el puerto <see cref="ICurrentUser"/> leyendo los claims del JWT
/// de la petición HTTP en curso. Así los casos de uso conocen al usuario autenticado sin
/// depender de ASP.NET Core. Se registra como Scoped: una instancia por petición.
/// </summary>
public sealed class CurrentUserProvider : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    // Usuario de la petición actual (rellenado por el middleware JwtBearer).
    // Es null fuera de una petición HTTP (p. ej. jobs en segundo plano).
    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    /// <summary>Id del usuario autenticado, o null si no hay usuario o el claim no es un Guid válido.</summary>
    public Guid? UserId
    {
        get
        {
            // JwtBearer suele mapear "sub" a ClaimTypes.NameIdentifier; se consultan ambos
            // por si el mapeo de claims entrantes está desactivado.
            var claim = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                     ?? User?.FindFirst("sub")?.Value;
            return Guid.TryParse(claim, out var id) ? id : null;
        }
    }

    /// <summary>Email del usuario según el token, o null si no viene.</summary>
    public string? Email => User?.FindFirst(ClaimTypes.Email)?.Value;

    /// <summary>Roles del usuario (p. ej. ROLE_ADMIN). Colección vacía si no hay usuario.</summary>
    public IReadOnlyCollection<string> Roles => User?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray()
                                             ?? Array.Empty<string>();

    /// <summary>
    /// Id del cliente asociado (claim personalizado "customer_id"), pensado para que un usuario
    /// ROLE_CLIENT acceda solo a sus propios datos. Nota: hoy el emisor de JWT no agrega este
    /// claim, así que siempre devuelve null.
    /// </summary>
    public Guid? CustomerId
    {
        get
        {
            var claim = User?.FindFirst("customer_id")?.Value;
            return Guid.TryParse(claim, out var id) ? id : null;
        }
    }

    /// <summary>True si la petición trae un token válido.</summary>
    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    /// <summary>Comprueba si el usuario tiene el rol indicado; false si no hay usuario.</summary>
    public bool IsInRole(string role) => User?.IsInRole(role) ?? false;
}
