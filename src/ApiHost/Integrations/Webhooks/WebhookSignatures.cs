using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ApiHost.Integrations.Webhooks;

// Signs and checks webhooks the Standard Webhooks way (https://www.standardwebhooks.com/), which
// many providers use and which has libraries for receivers in most languages:
//
//   webhook-id:        the event's id (the same on every retry)
//   webhook-timestamp: Unix seconds when it was sent
//   webhook-signature: "v1," + base64(HMAC-SHA256(secret, "{id}.{timestamp}.{body}"))
//
// Secrets look like "whsec_" + base64 of 24 random bytes. Check is for webhooks the app receives
// (see docs/integrations.md, "Receiving webhooks").
public static class WebhookSignatures
{
    public const string SecretPrefix = "whsec_";
    public const string IdHeader = "webhook-id";
    public const string TimestampHeader = "webhook-timestamp";
    public const string SignatureHeader = "webhook-signature";

    // How far a received webhook's timestamp may be from now, which stops an old one being replayed.
    public static readonly TimeSpan Tolerance = TimeSpan.FromMinutes(5);

    public static string GenerateSecret() => SecretPrefix + Convert.ToBase64String(RandomNumberGenerator.GetBytes(24));

    public static string Sign(string secret, string id, DateTimeOffset timestamp, string body)
    {
        var signed = $"{id}.{timestamp.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)}.{body}";
        return "v1," + Convert.ToBase64String(HMACSHA256.HashData(KeyOf(secret), Encoding.UTF8.GetBytes(signed)));
    }

    // True if the headers carry a valid signature of the body, made recently.
    public static bool Check(string secret, string? id, string? timestamp, string? signatures, string body, DateTimeOffset now)
    {
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(signatures)
            || !long.TryParse(timestamp, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds))
        {
            return false;
        }

        var sentAt = DateTimeOffset.FromUnixTimeSeconds(seconds);
        if ((now - sentAt).Duration() > Tolerance)
        {
            return false;
        }

        // The header can list several signatures (while a secret is being changed), separated by spaces.
        var expected = Encoding.ASCII.GetBytes(Sign(secret, id, sentAt, body));
        return signatures.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Any(candidate => CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(candidate), expected));
    }

    private static byte[] KeyOf(string secret)
        => Convert.FromBase64String(secret.StartsWith(SecretPrefix, StringComparison.Ordinal) ? secret[SecretPrefix.Length..] : secret);
}
