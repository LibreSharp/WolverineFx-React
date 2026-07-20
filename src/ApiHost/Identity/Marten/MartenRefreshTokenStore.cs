using Marten;

namespace ApiHost.Identity.Marten;

public class MartenRefreshTokenStore : IRefreshTokenStore
{
    private readonly IDocumentSession _session;

    public MartenRefreshTokenStore(IDocumentSession session)
    {
        _session = session;
    }

    public Task StoreAsync(RefreshToken token, CancellationToken cancellationToken)
    {
        _session.Store(token);
        return _session.SaveChangesAsync(cancellationToken);
    }

    public Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken)
    {
        return _session.Query<RefreshToken>()
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);
    }

    public Task RevokeAsync(RefreshToken token, string? replacedByTokenHash, CancellationToken cancellationToken)
    {
        token.RevokedAtUtc = DateTimeOffset.UtcNow;
        token.ReplacedByTokenHash = replacedByTokenHash;
        _session.Store(token);
        return _session.SaveChangesAsync(cancellationToken);
    }

    public async Task RevokeAllForUserAsync(string userId, CancellationToken cancellationToken)
    {
        var tokens = await _session.Query<RefreshToken>()
            .Where(t => t.UserId == userId && t.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var token in tokens)
        {
            token.RevokedAtUtc = DateTimeOffset.UtcNow;
            _session.Store(token);
        }

        await _session.SaveChangesAsync(cancellationToken);
    }
}
