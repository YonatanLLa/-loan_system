using System.Text.Json;
using Shouldly;
using Xunit;

namespace LoanSystem.Architecture.Tests;

public class ConfigurationSecretsTests
{
    private static readonly string[] ForbiddenConfigKeyNames =
    [
        "Key",
        "SigningKeys",
        "EncryptionKey",
        "EncryptionKeys"
    ];

    private static readonly string[] ForbiddenContentMarkers =
    [
        "-----BEGIN PRIVATE KEY-----",
        "-----BEGIN RSA PRIVATE KEY-----",
        "-----BEGIN EC PRIVATE KEY-----",
        "SecretKeyForJwtSigningShouldBeReplaced"
    ];

    private static string FindBackendRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "LoanSystem.slnx")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate 'backend' solution root.");
    }

    [Theory]
    [InlineData("src/LoanSystem.WebApi/appsettings.json", false)]
    [InlineData("src/LoanSystem.WebApi/appsettings.Development.json", true)]
    [InlineData("src/LoanSystem.Workers/appsettings.json", false)]
    [InlineData("src/LoanSystem.Workers/appsettings.Development.json", true)]
    public void AppsettingsFiles_MustNotContainCommittedSecretsOrJwtSymmetricKey(
        string relativePath,
        bool isDevelopmentFile)
    {
        var fullPath = Path.Combine(FindBackendRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar));
        File.Exists(fullPath).ShouldBeTrue($"Expected config file '{relativePath}' to exist.");

        var raw = File.ReadAllText(fullPath);

        foreach (var marker in ForbiddenContentMarkers)
        {
            raw.ShouldNotContain(marker, Case.Insensitive);
        }

        if (!isDevelopmentFile)
        {
            raw.ShouldNotContain("postgrespassword", Case.Insensitive);
            raw.ShouldNotContain("Password=", Case.Insensitive);
        }

        using var doc = JsonDocument.Parse(raw);
        AssertNoForbiddenSecretKeys(doc.RootElement, relativePath);
    }

    [Fact]
    public void SourceFiles_MustNotContainPrivateKeyPemBlocks()
    {
        var srcDir = Path.Combine(FindBackendRoot(), "src");
        var files = Directory.EnumerateFiles(srcDir, "*.*", SearchOption.AllDirectories)
            .Where(path =>
            {
                var normalized = path.Replace('\\', '/');
                if (normalized.Contains("/obj/") || normalized.Contains("/bin/"))
                {
                    return false;
                }

                var ext = Path.GetExtension(path);
                return ext is ".cs" or ".json";
            });

        foreach (var file in files)
        {
            var content = File.ReadAllText(file);
            foreach (var marker in ForbiddenContentMarkers)
            {
                content.ShouldNotContain(
                    marker,
                    Case.Insensitive,
                    $"Committed source file '{file}' must never contain private key material or hardcoded JWT secrets.");
            }
        }
    }

    [Theory]
    [InlineData("src/LoanSystem.WebApi/LoanSystem.WebApi.csproj", true)]
    [InlineData("src/LoanSystem.Workers/LoanSystem.Workers.csproj", false)]
    public void HostProjectFiles_MustExcludeDevelopmentSettingsFromPublish(
        string relativeCsprojPath,
        bool expectUserSecretsId)
    {
        var fullPath = Path.Combine(FindBackendRoot(), relativeCsprojPath.Replace('/', Path.DirectorySeparatorChar));
        var content = File.ReadAllText(fullPath);

        content.ShouldContain("appsettings.Development.json");
        content.ShouldContain("CopyToPublishDirectory=\"Never\"");

        if (expectUserSecretsId)
        {
            content.ShouldContain("<UserSecretsId>");
        }
    }

    private static void AssertNoForbiddenSecretKeys(JsonElement element, string fileLabel)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in element.EnumerateObject())
            {
                ForbiddenConfigKeyNames.ShouldNotContain(
                    prop.Name,
                    $"Config file '{fileLabel}' must not define secret property '{prop.Name}'.");

                AssertNoForbiddenSecretKeys(prop.Value, fileLabel);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                AssertNoForbiddenSecretKeys(item, fileLabel);
            }
        }
    }
}
