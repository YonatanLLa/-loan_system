using System.Net;
using LoanSystem.Common.Core.Ports.Outbound;
using LoanSystem.Modules.Customers.Adapters.Outbound.Providers;
using LoanSystem.Modules.Customers.Core.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace LoanSystem.Adapters.UnitTests.Customers;

public class DocumentStorageTests
{
    [Fact]
    public async Task LocalDocumentStorage_PutStreamsAtomic_PreventsTraversal_AndValidatesSignedTokens()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"loansystem-docs-{Guid.NewGuid():N}");
        try
        {
            var clock = new MutableClock(new DateTimeOffset(2026, 11, 14, 12, 0, 0, TimeSpan.Zero));
            var options = new CustomersOptions
            {
                Documents = new DocumentsOptions { MaxFileSizeMb = 1 },
                Storage = new StorageOptions { Provider = "Local", LocalPath = tempRoot }
            };
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Customers:Storage:SigningSecret"] = "test-signing-secret-32chars-minimum!" })
                .Build();
            var storage = new LocalDocumentStorage(options, config, new TestEnv(), clock);

            // Reject traversal
            await using var sample = new MemoryStream("%PDF-1.7 content"u8.ToArray());
            await Should.ThrowAsync<InvalidOperationException>(() =>
                storage.PutAsync("../escape.pdf", sample, "application/pdf"));

            // Put valid object
            sample.Position = 0;
            var key = $"customers/{Guid.NewGuid()}/DNI_FRONTAL/{Guid.NewGuid()}.pdf";
            var stored = await storage.PutAsync(key, sample, "application/pdf");
            stored.SizeBytes.ShouldBe(sample.Length);
            stored.Sha256.Length.ShouldBe(64);
            (await storage.ExistsAsync(key)).ShouldBeTrue();

            // Signed read URL works within TTL and expires after TTL
            var signedUri = await storage.GetSignedReadUrlAsync(key, TimeSpan.FromSeconds(60));
            signedUri.ToString().ShouldStartWith("/api/customers/files/");
            var token = Uri.UnescapeDataString(signedUri.ToString()["/api/customers/files/".Length..]);

            var opened = await storage.OpenSignedReadAsync(token);
            opened.ShouldNotBeNull();
            opened.ContentType.ShouldBe("application/pdf");
            await using (opened.Stream)
            {
                using var sr = new StreamReader(opened.Stream);
                (await sr.ReadToEndAsync()).ShouldBe("%PDF-1.7 content");
            }

            // Tampered token rejected
            (await storage.OpenSignedReadAsync(token + "x")).ShouldBeNull();

            // Expired token rejected
            clock.UtcNow = clock.UtcNow.AddSeconds(61);
            (await storage.OpenSignedReadAsync(token)).ShouldBeNull();
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task S3DocumentStorage_SignsPutAndHeadRequestsWithSigV4_AndBuildsPresignedGetUrl()
    {
        var handler = new CapturingHttpMessageHandler();
        using var http = new HttpClient(handler);
        var clock = new MutableClock(new DateTimeOffset(2026, 11, 14, 12, 0, 0, TimeSpan.Zero));
        var storage = new S3DocumentStorage(
            http,
            new StorageOptions
            {
                Provider = "S3",
                Bucket = "loans-kyc-docs",
                Region = "us-east-1",
                ServiceUrl = "https://minio.local:9000",
                ForcePathStyle = true
            },
            maxFileSizeBytes: 5 * 1024 * 1024,
            clock,
            accessKey: "AKIA_TEST",
            secretKey: "SECRET_TEST",
            serverSideEncryption: "AES256");

        await using var data = new MemoryStream([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        var stored = await storage.PutAsync("customers/c1/doc.png", data, "image/png");

        stored.StorageUri.ShouldBe("s3://loans-kyc-docs/customers/c1/doc.png");
        handler.Requests.Count.ShouldBe(1);
        var putReq = handler.Requests[0];
        putReq.Method.ShouldBe(HttpMethod.Put);
        putReq.RequestUri!.ToString().ShouldBe("https://minio.local:9000/loans-kyc-docs/customers/c1/doc.png");
        putReq.Headers.TryGetValues("Authorization", out var auth).ShouldBeTrue();
        auth!.First().ShouldStartWith("AWS4-HMAC-SHA256 Credential=AKIA_TEST/20261114/us-east-1/s3/aws4_request");
        putReq.Headers.Contains("x-amz-date").ShouldBeTrue();
        putReq.Headers.Contains("x-amz-content-sha256").ShouldBeTrue();
        putReq.Headers.GetValues("x-amz-server-side-encryption").First().ShouldBe("AES256");

        // HEAD check: 200 -> true, 404 -> false
        (await storage.ExistsAsync("customers/c1/doc.png")).ShouldBeTrue();
        handler.NextStatusCode = HttpStatusCode.NotFound;
        (await storage.ExistsAsync("customers/c1/missing.png")).ShouldBeFalse();

        // Pre-signed GET URL
        var presigned = await storage.GetSignedReadUrlAsync("customers/c1/doc.png", TimeSpan.FromMinutes(5));
        presigned.Query.ShouldContain("X-Amz-Algorithm=AWS4-HMAC-SHA256");
        presigned.Query.ShouldContain("X-Amz-Expires=300");
        presigned.Query.ShouldContain("X-Amz-SignedHeaders=host");
        presigned.Query.ShouldContain("X-Amz-Signature=");
    }

    private sealed class CapturingHttpMessageHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];
        public HttpStatusCode NextStatusCode { get; set; } = HttpStatusCode.OK;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(new HttpResponseMessage(NextStatusCode));
        }
    }

    private sealed class MutableClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = now;
        public DateTime LimaNow => UtcNow.UtcDateTime.AddHours(-5);
        public DateOnly LimaToday => DateOnly.FromDateTime(LimaNow);
    }

    private sealed class TestEnv : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "LoanSystem.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
