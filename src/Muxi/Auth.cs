using System.Security.Cryptography;
using System.Text;

namespace Muxi;

public static class Auth
{
    public static (string Signature, long Timestamp) GenerateHmacSignature(string secretKey, string method, string path)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var signPath = path.Split('?')[0];
        var message = $"{timestamp};{method};{signPath}";
        
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
        var signature = Convert.ToBase64String(hash);
        
        return (signature, timestamp);
    }

    public static string BuildAuthHeader(string keyId, string secretKey, string method, string path)
    {
        var (signature, timestamp) = GenerateHmacSignature(secretKey, method, path);
        return $"MUXI-HMAC key={keyId}, timestamp={timestamp}, signature={signature}";
    }
}
