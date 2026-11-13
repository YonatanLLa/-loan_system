using DbUp;
using DbUp.Engine;
using Microsoft.Extensions.Configuration;

namespace LoanSystem.DbMigrator;

public static class Program
{
    public static int Main(string[] args)
    {
        Console.WriteLine("=================================================");
        Console.WriteLine(" LoanSystem Database Migrator (DbUp - PostgreSQL)");
        Console.WriteLine("=================================================");

        var configuration = BuildConfiguration();

        // No fallback in code: the connection string must come from an argument, environment, or secret store.
        string? connectionString = args.FirstOrDefault(a => !a.StartsWith("--"))
            ?? configuration["ConnectionStrings:Database"]
            ?? Environment.GetEnvironmentVariable("DATABASE_URL");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("ERROR: No connection string provided.");
            Console.WriteLine("Pass it as the first argument or set ConnectionStrings__Database (or DATABASE_URL).");
            Console.ResetColor();
            return 1;
        }

        Console.WriteLine($"Target database: {GetMaskedConnectionString(connectionString)}");

        var databaseDir = FindDatabaseDirectory();
        if (databaseDir is null)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("ERROR: Could not locate 'database' directory.");
            Console.ResetColor();
            return 1;
        }

        Console.WriteLine($"Found database directory at: {databaseDir}");

        var result = Upgrade(connectionString, databaseDir);

        if (!result.Successful)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Migration failed: {result.Error}");
            Console.ResetColor();
            return 1;
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("✓ All database migrations applied successfully!");
        Console.ResetColor();
        return 0;
    }

    public static DatabaseUpgradeResult Upgrade(string connectionString, string databaseDir)
    {
        try
        {
            EnsureDatabase.For.PostgresqlDatabase(connectionString);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Note: EnsureDatabase warning: {ex.Message}");
        }

        var scripts = GetOrderedScripts(databaseDir);
        var upgraderBuilder = DeployChanges.To
            .PostgresqlDatabase(connectionString)
            .WithVariablesDisabled()
            .LogToConsole();

        int orderIndex = 1;
        foreach (var (scriptName, filePath) in scripts)
        {
            var content = File.ReadAllText(filePath);
            var orderedName = $"{orderIndex:D3}_{scriptName}";
            upgraderBuilder.WithScript(orderedName, content);
            orderIndex++;
        }

        var upgrader = upgraderBuilder.Build();
        return upgrader.PerformUpgrade();
    }

    public static List<(string Name, string Path)> GetOrderedScripts(string databaseDir)
    {
        var relativePaths = new[]
        {
            "migrations/00_extensions_and_enums.sql",
            "functions/fn_updated_at.sql",
            "migrations/01_admin_config.sql",
            "migrations/02_security_users.sql",
            "migrations/03_customers_kyc.sql",
            "migrations/04_loan_products.sql",
            "migrations/05_loan_applications.sql",
            "migrations/06_loans_schedules.sql",
            "migrations/07_payments_ledger.sql",
            "migrations/08_treasury_reconciliation.sql",
            "migrations/09_collections_notifications.sql",
            "migrations/10_integrations_reconciliation.sql",
            "indexes/11_indexes.sql",
            "seeds/12_seed_data.sql",
            // New scripts are APPENDED: DbUp journals scripts by their ordered name (001_, 002_, ...),
            // so inserting in the middle would rename already-applied scripts.
            "migrations/13_outbox_messages.sql",
            "migrations/14_audit_logs_user_agent.sql",
            "migrations/15_audit_logs_append_only.sql",
            "migrations/16_users_mfa.sql",
            "migrations/17_approval_requests.sql"
        };

        var list = new List<(string Name, string Path)>();
        foreach (var rel in relativePaths)
        {
            var fullPath = Path.Combine(databaseDir, rel.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(fullPath))
            {
                var scriptName = Path.GetFileName(rel);
                list.Add((scriptName, fullPath));
            }
        }

        return list;
    }

    public static string? FindDatabaseDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "database");
            if (Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, "run_all.sql")))
            {
                return candidate;
            }

            // Check parent repo structure
            candidate = Path.Combine(dir.FullName, "..", "database");
            if (Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, "run_all.sql")))
            {
                return Path.GetFullPath(candidate);
            }

            dir = dir.Parent;
        }

        return null;
    }

    private static string GetMaskedConnectionString(string connStr)
    {
        try
        {
            var builder = new Npgsql.NpgsqlConnectionStringBuilder(connStr);
            if (!string.IsNullOrEmpty(builder.Password))
            {
                builder.Password = "******";
            }
            return builder.ConnectionString;
        }
        catch
        {
            return "Custom Connection String";
        }
    }

    private static IConfiguration BuildConfiguration()
    {
        var builder = new ConfigurationBuilder()
            .AddEnvironmentVariables();

        var preConfig = builder.Build();
        var provider = preConfig["Secrets:Provider"];
        if (string.Equals(provider, "KeyPerFile", StringComparison.OrdinalIgnoreCase))
        {
            var path = preConfig["Secrets:Path"];
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new InvalidOperationException(
                    "Configuration value 'Secrets:Path' is required when 'Secrets:Provider' is 'KeyPerFile'.");
            }

            builder.AddKeyPerFile(Path.GetFullPath(path), optional: false);
        }

        return builder.Build();
    }
}
