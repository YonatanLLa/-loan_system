<#
.SYNOPSIS
    Rotates RS256 signing keys in a Jwt__SigningKeys JSON secret file (BE-SEC-10).
.DESCRIPTION
    Generates a new RSA-3072 PKCS#8 key pair pre-published by default (notBefore = UtcNow + 24h,
    notAfter = notBefore + 104d) so JWKS propagates to consumers before signing begins,
    prunes keys expired beyond the token lifetime grace window, and writes the JSON file atomically.
    Compatible with both Windows PowerShell 5.1 (.NET Framework) and PowerShell 7+ (pwsh).
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Path,

    [int]$KeySizeBits = 3072,
    [int]$PrePublishHours = 24,
    [int]$ValidDays = 104,
    [int]$PruneGraceHours = 1,
    [switch]$ActivateImmediately
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($KeySizeBits -lt 2048) {
    throw "KeySizeBits must be at least 2048."
}

function ConvertTo-Base64Url([byte[]]$bytes) {
    [Convert]::ToBase64String($bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_')
}

function Export-Pkcs8Pem([System.Security.Cryptography.RSA]$rsa) {
    if ($rsa.PSObject.Methods['ExportPkcs8PrivateKeyPem']) {
        return $rsa.ExportPkcs8PrivateKeyPem()
    }

    if ($rsa -is [System.Security.Cryptography.RSACng]) {
        $pkcs8 = $rsa.Key.Export([System.Security.Cryptography.CngKeyBlobFormat]::Pkcs8PrivateBlob)
        $b64 = [Convert]::ToBase64String($pkcs8)
        $lines = for ($i = 0; $i -lt $b64.Length; $i += 64) {
            $b64.Substring($i, [Math]::Min(64, $b64.Length - $i))
        }
        return "-----BEGIN PRIVATE KEY-----`n" + ($lines -join "`n") + "`n-----END PRIVATE KEY-----"
    }

    throw "RSA PKCS#8 export is unavailable on this runtime. Use PowerShell 7+ (pwsh) or rotate-jwt-keys.sh."
}

$now = [DateTimeOffset]::UtcNow
$notBefore = if ($ActivateImmediately) { $now.AddMinutes(-1) } else { $now.AddHours($PrePublishHours) }
$notAfter = $notBefore.AddDays($ValidDays)

$existingKeys = @()
$prunedCount = 0
if (Test-Path -LiteralPath $Path) {
    $raw = [System.IO.File]::ReadAllText($Path, [System.Text.Encoding]::UTF8)
    if (-not [string]::IsNullOrWhiteSpace($raw)) {
        $doc = $raw | ConvertFrom-Json
        if ($null -ne $doc.keys) {
            foreach ($entry in $doc.keys) {
                $entryNotAfter = [DateTimeOffset]::Parse($entry.notAfter).ToUniversalTime()
                if ($entryNotAfter.AddHours($PruneGraceHours) -gt $now) {
                    $existingKeys += [PSCustomObject]@{
                        privateKeyPem = [string]$entry.privateKeyPem
                        notBefore     = ([DateTimeOffset]::Parse($entry.notBefore).ToUniversalTime()).ToString('O')
                        notAfter      = $entryNotAfter.ToString('O')
                    }
                }
                else {
                    $prunedCount++
                }
            }
        }
    }
}

$rsa = [System.Security.Cryptography.RSA]::Create($KeySizeBits)
$sha = [System.Security.Cryptography.SHA256]::Create()
try {
    $pem = Export-Pkcs8Pem $rsa
    $pub = $rsa.ExportParameters($false)

    $e = ConvertTo-Base64Url $pub.Exponent
    $n = ConvertTo-Base64Url $pub.Modulus
    $canonical = "{`"e`":`"$e`",`"kty`":`"RSA`",`"n`":`"$n`"}"
    $hash = $sha.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($canonical))
    $newKid = ConvertTo-Base64Url $hash
}
finally {
    $sha.Dispose()
    $rsa.Dispose()
}

$allKeys = @($existingKeys) + @([PSCustomObject]@{
    privateKeyPem = $pem
    notBefore     = $notBefore.ToString('O')
    notAfter      = $notAfter.ToString('O')
})

$parentDir = Split-Path -Parent $Path
if (-not [string]::IsNullOrWhiteSpace($parentDir) -and -not (Test-Path -LiteralPath $parentDir)) {
    New-Item -ItemType Directory -Path $parentDir -Force | Out-Null
}

$jsonOut = ([PSCustomObject]@{ keys = $allKeys }) | ConvertTo-Json -Depth 4
$tempFile = "$Path.tmp.$([Guid]::NewGuid().ToString('N'))"
try {
    [System.IO.File]::WriteAllText($tempFile, $jsonOut, [System.Text.UTF8Encoding]::new($false))
    Move-Item -LiteralPath $tempFile -Destination $Path -Force
}
finally {
    if (Test-Path -LiteralPath $tempFile) {
        Remove-Item -LiteralPath $tempFile -Force
    }
}

Write-Output "Added RS$KeySizeBits key kid=$newKid (notBefore=$($notBefore.ToString('O')), notAfter=$($notAfter.ToString('O'))). Retained=$($allKeys.Count), Pruned=$prunedCount."
