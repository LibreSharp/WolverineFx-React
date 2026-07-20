using ApiHost.Data.EfCore;
using Microsoft.EntityFrameworkCore;

namespace ApiHost.Identity.EfCore;

public class EfCoreRefreshTokenStore : IRefreshTokenStore
{
    private readonly AppDbContext _db;

    public EfCoreRefreshTokenStore(AppDbContext db)
    {
        _db = db;
    }

    public async Task StoreAsync(RefreshToken token, CancellationToken cancellationToken)
    {
        _db.RefreshTokens.Add(token);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken)
    {
        return _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);
    }

    public async Task RevokeAsync(RefreshToken token, string? replacedByTokenHash, CancellationToken cancellationToken)
    {
        token.RevokedAtUtc = DateTimeOffset.UtcNow;
        token.ReplacedByTokenHash = replacedByTokenHash;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task RevokeAllForUserAsync(string userId, CancellationToken cancellationToken)
    {
        var tokens = await _db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var token in tokens)
        {
            token.RevokedAtUtc = DateTimeOffset.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}
