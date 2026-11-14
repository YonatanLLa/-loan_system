namespace LoanSystem.Modules.Customers.Core.Domain.Validators;

/// <summary>
/// Inspects leading file magic bytes to verify uploaded KYC documents match allowed binary formats
/// (JPEG, PNG, WEBP, PDF) regardless of file extension (BE-CUS-03, CA-03.b).
/// </summary>
internal static class FileSignatureSniffer
{
    public const int HeaderReadSize = 12;

    public static string? DetectAllowedMimeType(ReadOnlySpan<byte> header, IReadOnlyCollection<string> allowedTypes)
    {
        string? detected = null;

        // JPEG: FF D8 FF
        if (header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
        {
            detected = "image/jpeg";
        }
        // PNG: 89 50 4E 47 0D 0A 1A 0A
        else if (header.Length >= 8 &&
                 header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47 &&
                 header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A)
        {
            detected = "image/png";
        }
        // WEBP: "RIFF" .... "WEBP"
        else if (header.Length >= 12 &&
                 header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46 &&
                 header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50)
        {
            detected = "image/webp";
        }
        // PDF: "%PDF-"
        else if (header.Length >= 5 &&
                 header[0] == 0x25 && header[1] == 0x50 && header[2] == 0x44 && header[3] == 0x46 && header[4] == 0x2D)
        {
            detected = "application/pdf";
        }

        if (detected is null)
        {
            return null;
        }

        return allowedTypes.Contains(detected, StringComparer.OrdinalIgnoreCase) ? detected : null;
    }

    public static string GetExtensionForMimeType(string mimeType) =>
        mimeType.ToLowerInvariant() switch
        {
            "image/jpeg" => "jpg",
            "image/png" => "png",
            "image/webp" => "webp",
            "application/pdf" => "pdf",
            _ => "bin"
        };
}
