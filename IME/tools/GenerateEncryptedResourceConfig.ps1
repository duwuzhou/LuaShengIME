param(
    [Parameter(Mandatory = $true)]
    [string]$OutFile,
    [Parameter(Mandatory = $true)]
    [string]$KeyBase64
)

$code = @"
using System;
namespace IME.Shared.ResourceProtection;
internal static partial class EncryptedResourceConfig
{
    internal const string KeyBase64 = "$KeyBase64";
    internal static readonly byte[] Key = Convert.FromBase64String(KeyBase64);
}
"@

Set-Content -Encoding UTF8 -Path $OutFile -Value $code
