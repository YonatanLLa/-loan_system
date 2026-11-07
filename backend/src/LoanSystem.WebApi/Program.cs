using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using LoanSystem.Common.Adapters.Clock;
using LoanSystem.Common.Adapters.Configuration;
using LoanSystem.Common.Adapters.Http;
using LoanSystem.Common.Adapters.Http.Idempotency;
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

// Core infrastructure services
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUserProvider>();
builder.Services.AddSingleton<IClock, SystemClock>();

// Idempotency-Key support: Redis when ConnectionStrings:Redis is set, in-memory otherwise
var idempotencyUsesRedis = builder.Services.AddIdempotency(builder.Configuration);

// Authentication & Authorization (Roles)
// The signing key has no code default: it must come from configuration (Development file, env var or secret store)
var jwtKey = builder.Configuration.GetRequiredValue("Jwt:Key");
var jwtKeyBytes = Encoding.UTF8.GetBytes(jwtKey);
if (jwtKeyBytes.Length < 32)
{
    throw new InvalidOperationException("Configuration value 'Jwt:Key' must be at least 32 bytes (256 bits) for HMAC-SHA256.");
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "LoanSystem.Identity",
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "LoanSystem.App",
            IssuerSigningKey = new SymmetricSecurityKey(jwtKeyBytes),
            RoleClaimType = ClaimTypes.Role
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ROLE_ADMIN", p => p.RequireRole("ROLE_ADMIN"));
    options.AddPolicy("ROLE_ANALYST", p => p.RequireRole("ROLE_ADMIN", "ROLE_ANALYST"));
    options.AddPolicy("ROLE_OPERATOR", p => p.RequireRole("ROLE_ADMIN", "ROLE_OPERATOR"));
    options.AddPolicy("ROLE_COLLECTOR", p => p.RequireRole("ROLE_ADMIN", "ROLE_COLLECTOR"));
    options.AddPolicy("ROLE_AUDITOR", p => p.RequireRole("ROLE_ADMIN", "ROLE_AUDITOR"));
    options.AddPolicy("ROLE_CLIENT", p => p.RequireRole("ROLE_CLIENT"));
});

// Register all 12 modules
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

app.UseExceptionHandler();

app.UseAuthentication();
app.UseAuthorization();

// After authentication: idempotency keys are scoped to the authenticated user
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

app.MapGet("/health", () => Results.Ok(new
{
    Status = "Healthy",
    Timestamp = DateTimeOffset.UtcNow,
    Service = "LoanSystem.WebApi"
}))
.WithTags("Health");

// Map endpoints for all 12 modules
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

// Required for WebApplicationFactory in integration tests
public partial class Program { }
