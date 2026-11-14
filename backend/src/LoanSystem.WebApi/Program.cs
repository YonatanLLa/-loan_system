using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Scalar.AspNetCore;
using LoanSystem.Common.Adapters.Clock;
using LoanSystem.Common.Adapters.Configuration;
using LoanSystem.Common.Adapters.Http;
using LoanSystem.Common.Adapters.Http.Idempotency;
using LoanSystem.Common.Core.Application;
using LoanSystem.Common.Core.Ports.Outbound;
using LoanSystem.Modules.Security.Adapters;
using LoanSystem.Modules.Security.Adapters.Inbound.Http;
using LoanSystem.Modules.Customers.Adapters;
using LoanSystem.Modules.Customers.Adapters.Inbound.Http;
using LoanSystem.Modules.Products.Adapters;
using LoanSystem.Modules.Products.Adapters.Inbound.Http;
using LoanSystem.Modules.Origination.Adapters;
using LoanSystem.Modules.Origination.Adapters.Inbound.Http;
using LoanSystem.Modules.Loans.Adapters;
using LoanSystem.Modules.Loans.Adapters.Inbound.Http;
using LoanSystem.Modules.Payments.Adapters;
using LoanSystem.Modules.Payments.Adapters.Inbound.Http;
using LoanSystem.Modules.Treasury.Adapters;
using LoanSystem.Modules.Treasury.Adapters.Inbound.Http;
using LoanSystem.Modules.Collections.Adapters;
using LoanSystem.Modules.Collections.Adapters.Inbound.Http;
using LoanSystem.Modules.Integrations.Adapters;
using LoanSystem.Modules.Integrations.Adapters.Inbound.Http;
using LoanSystem.Modules.Notifications.Adapters;
using LoanSystem.Modules.Notifications.Adapters.Inbound.Http;
using LoanSystem.Modules.Administration.Adapters;
using LoanSystem.Modules.Administration.Adapters.Inbound.Http;
using LoanSystem.Modules.Reports.Adapters;
using LoanSystem.Modules.Reports.Adapters.Inbound.Http;

var builder = WebApplication.CreateBuilder(args);

// Configuración de secretos pluggable (Development: UserSecrets/appsettings; Producción: KeyPerFile u otros)
builder.Configuration.AddSecretManager(builder.Environment);

// Servicios base de infraestructura
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUserProvider>();
builder.Services.AddSingleton<IClock, SystemClock>();
// El interceptor de auditoría (BE-SEC-07) lo registra cada módulo con AddAuditLogging()

// IP real del cliente detrás de un proxy/balanceador (BE-SEC-07: user_ip de la auditoría).
// Solo se confía en X-Forwarded-* si vienen de los proxies o redes configurados;
// sin configuración se mantiene el valor por defecto del framework (solo loopback).
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

    foreach (var proxy in builder.Configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? [])
    {
        options.KnownProxies.Add(IPAddress.Parse(proxy));
    }

    foreach (var network in builder.Configuration.GetSection("ForwardedHeaders:KnownNetworks").Get<string[]>() ?? [])
    {
        options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
    }
});

// Soporte de Idempotency-Key: Redis si ConnectionStrings:Redis está configurado; en memoria en caso contrario
var idempotencyUsesRedis = builder.Services.AddIdempotency(builder.Configuration);

// Autenticación RS256 con keyring rotatorio y autorización por roles (BE-SEC-04, BE-SEC-10)
builder.Services.AddSecurityAuthentication(builder.Configuration, builder.Environment);

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(SecurityPolicies.Admin, p => p.RequireRole(SecurityPolicies.Admin));
    options.AddPolicy(SecurityPolicies.Analyst, p => p.RequireRole(SecurityPolicies.Admin, SecurityPolicies.Analyst));
    options.AddPolicy(SecurityPolicies.Operator, p => p.RequireRole(SecurityPolicies.Admin, SecurityPolicies.Operator));
    options.AddPolicy(SecurityPolicies.Collector, p => p.RequireRole(SecurityPolicies.Admin, SecurityPolicies.Collector));
    options.AddPolicy(SecurityPolicies.Auditor, p => p.RequireRole(SecurityPolicies.Admin, SecurityPolicies.Auditor));
    options.AddPolicy(SecurityPolicies.Client, p => p.RequireRole(SecurityPolicies.Client));

    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
});

// Registro de los 12 módulos
builder.Services.AddSecurityModule(builder.Configuration);
builder.Services.AddCustomersModule(builder.Configuration);
builder.Services.AddProductsModule(builder.Configuration);
builder.Services.AddOriginationModule(builder.Configuration);
builder.Services.AddLoansModule(builder.Configuration);
builder.Services.AddPaymentsModule(builder.Configuration);
builder.Services.AddTreasuryModule(builder.Configuration);
builder.Services.AddCollectionsModule(builder.Configuration);
builder.Services.AddIntegrationsModule(builder.Configuration);
builder.Services.AddNotificationsModule(builder.Configuration);
builder.Services.AddAdministrationModule(builder.Configuration);
builder.Services.AddReportsModule(builder.Configuration);

var app = builder.Build();

if (!idempotencyUsesRedis)
{
    app.Logger.LogWarning(
        "ConnectionStrings:Redis is not configured: idempotency keys are stored in memory and are not shared between API instances.");
}

// Primero: el resto del pipeline (auditoría, logs) ve la IP real del cliente
app.UseForwardedHeaders();

app.UseExceptionHandler();

app.UseAuthentication();
app.UseAuthorization();

// Después de la autenticación: las claves de idempotencia se asocian al usuario autenticado
app.UseMiddleware<IdempotencyMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.WithTitle("Sistema de Préstamos API")
               .WithTheme(ScalarTheme.Moon);
    });
}

// Falla rápido fuera de Development si faltan secretos obligatorios o hay valores inseguros de dev
app.Configuration.ValidateStartupSecrets(app.Environment, requireHttpSecuritySecrets: true);

app.MapGet("/health", async (
    Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckService healthChecks,
    CancellationToken ct) =>
{
    var report = await healthChecks.CheckHealthAsync(ct);
    var payload = new
    {
        Status = report.Status.ToString(),
        Timestamp = DateTimeOffset.UtcNow,
        Service = "LoanSystem.WebApi"
    };

    return report.Status == Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy
        ? Results.Json(payload, statusCode: StatusCodes.Status503ServiceUnavailable)
        : Results.Ok(payload);
})
.WithTags("Health")
.AllowAnonymous();

// Mapeo de endpoints de los 12 módulos
app.MapSecurityEndpoints();
app.MapCustomersEndpoints();
app.MapProductsEndpoints();
app.MapOriginationEndpoints();
app.MapLoansEndpoints();
app.MapPaymentsEndpoints();
app.MapTreasuryEndpoints();
app.MapCollectionsEndpoints();
app.MapIntegrationsEndpoints();
app.MapNotificationsEndpoints();
app.MapAdministrationEndpoints();
app.MapReportsEndpoints();

app.Run();

// Necesario para WebApplicationFactory en los tests de integración
public partial class Program { }
