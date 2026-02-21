using System.Security.Cryptography;
using System.Text;

namespace TradePilot.Tests.Common;

internal static class HmacTestHelper
{
    public static string ComputeHexSignature(string secret, string timestamp, string nonce, string body)
    {
        var payload = $"{timestamp}.{nonce}.{body}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
