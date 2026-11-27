using System.Security.Cryptography;
using System.Text;

namespace ApiHost.Integrations.ApiKeys;

// A key that lets a program (a script, another service, an AI assistant through /mcp) use the
// API as the organisation, instead of a user's login. Only the key's SHA-256 hash is stored: the
// key itself is shown once, when it's created. Not tenanted: it names its organisation.
public class ApiKey
{
    public Guid Id { get; set; }

    public Guid OrganisationId { get; set; }

    public string Name { get; set; } = string.Empty;

    // The start of the key ("sk_AbCd1234"), to tell keys apart in a list.
    public string Hint { get; set; } = string.Empty;

    public string SecretHash { get; set; } = string.Empty;

    // One of ApiKeyScopes.
    public string Scope { get; set; } = ApiKeyScopes.Read;

    // The member who created the key. Requests with it act as them, so they stop working if that
    // member leaves the organisation.
    public string CreatedByUserId { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }

    public DateTimeOffset? LastUsedAt { get; set; }

    public bool IsExpired(DateTimeOffset now) => ExpiresAt is { } expires && expires <= now;
}

public static class ApiKeyScopes
{
    // GET requests only, and MCP tools that only read.
    public const string Read = "read";

    // Everything an API key may do (ApiKeyOptions.AllowedPaths).
    public const string Write = "write";

    public static readonly IReadOnlyList<string> All = [Read, Write];
}

public class ApiKeyOptions
{
    public const string SectionName = "ApiKeys";

    // The API's paths an API key can be used on; everything else (account, organisation, billing
    // and admin endpoints, and the realtime hub) needs a user's login.
    public string[] AllowedPaths { get; set; } = ["/api/todos", "/mcp"];

    // Requests a minute for each key, after which it gets 429 Too Many Requests.
    public int RequestsPerMinute { get; set; } = 120;

    public int MaxPerOrganisation { get; set; } = 25;

    // How long the old key keeps working after a rotation, so programs can switch over.
    public TimeSpan RotationGracePeriod { get; set; } = TimeSpan.FromHours(24);
}

public static class ApiKeySecrets
{
    // Tells an API key apart from a JWT in the Authorization header.
    public const string Prefix = "sk_";

    // 32 random bytes (256 bits): too many to guess, so a fast hash is enough to store them.
    public static string Generate() => Prefix + Base64Url(RandomNumberGenerator.GetBytes(32));

    public static string Hash(string key) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(key)));

    public static string HintOf(string key) => key[..Math.Min(key.Length, Prefix.Length + 8)];

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
