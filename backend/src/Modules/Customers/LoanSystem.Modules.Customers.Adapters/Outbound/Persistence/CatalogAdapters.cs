using Dapper;
using LoanSystem.Common.Adapters.Persistence;
using LoanSystem.Modules.Customers.Core.Domain;
using LoanSystem.Modules.Customers.Core.Ports.Outbound;
using Microsoft.Extensions.Caching.Memory;

namespace LoanSystem.Modules.Customers.Adapters.Outbound.Persistence;

internal sealed class DocumentTypeCatalog : IDocumentTypeCatalog
{
    private const string CacheKey = "customers:document_types:all";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);

    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IMemoryCache _cache;

    public DocumentTypeCatalog(IDbConnectionFactory connectionFactory, IMemoryCache cache)
    {
        _connectionFactory = connectionFactory;
        _cache = cache;
    }

    public async Task<DocumentTypeRule?> GetRuleAsync(DocumentType type, CancellationToken ct = default)
    {
        var all = await _cache.GetOrCreateAsync(CacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheTtl;

            await using var conn = await _connectionFactory.CreateOpenConnectionAsync(ct) as System.Data.Common.DbConnection
                ?? throw new InvalidOperationException("Expected DbConnection.");

            var rows = await conn.QueryAsync<DocumentTypeRow>(new CommandDefinition(
                """
                SELECT code AS Code,
                       name AS Name,
                       char_length AS CharLength,
                       validation_regex AS ValidationRegex,
                       is_active AS IsActive
                FROM document_types
                """,
                cancellationToken: ct));

            var dict = new Dictionary<DocumentType, DocumentTypeRule>();
            foreach (var row in rows)
            {
                if (Enum.TryParse<DocumentType>(row.Code, ignoreCase: true, out var parsed))
                {
                    dict[parsed] = new DocumentTypeRule(parsed, row.Name, row.CharLength, row.ValidationRegex, row.IsActive);
                }
            }

            return dict;
        });

        return all is not null && all.TryGetValue(type, out var rule) ? rule : null;
    }

    private sealed record DocumentTypeRow(string Code, string Name, short? CharLength, string? ValidationRegex, bool IsActive);
}

internal sealed class StaticBanksCatalog : IBanksCatalog
{
    // Peruvian SBS / Cámara de Compensación Electrónica (CCE) 3-digit entity codes (R4 MVP catalog).
    private static readonly IReadOnlyDictionary<string, string> Banks = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["002"] = "BCP",
        ["003"] = "Interbank",
        ["007"] = "Citibank",
        ["009"] = "Scotiabank",
        ["011"] = "BBVA",
        ["018"] = "Banco de la Nación",
        ["023"] = "Banco de Comercio",
        ["035"] = "Banco Pichincha",
        ["038"] = "BanBif",
        ["043"] = "Crediscotia",
        ["049"] = "MiBanco",
        ["053"] = "Banco GNB",
        ["054"] = "Banco Falabella",
        ["055"] = "Banco Ripley",
        ["056"] = "Santander Perú",
        ["058"] = "Alfin Banco",
        ["060"] = "ICBC Perú",
        ["063"] = "Bank of China Perú",
        ["091"] = "Compartamos Financiera",
        ["801"] = "Caja Arequipa",
        ["802"] = "Caja Cusco",
        ["803"] = "Caja Huancayo",
        ["804"] = "Caja Piura",
        ["805"] = "Caja Sullana",
        ["806"] = "Caja Trujillo",
        ["807"] = "Caja Ica"
    };

    public bool TryGetBankName(string bankCode, out string bankName) =>
        Banks.TryGetValue(bankCode, out bankName!);

    public IReadOnlyDictionary<string, string> GetAll() => Banks;
}
