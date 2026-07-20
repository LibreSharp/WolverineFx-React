namespace Api.Identity;

public interface IRefreshTokenStore
{
    Task StoreAsync(RefreshToken token, CancellationToken cancellationToken);

    Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken);

    Task RevokeAsync(RefreshToken token, string? replacedByTokenHash, CancellationToken cancellationToken);

    Task RevokeAllForUserAsync(string userId, CancellationToken cancellationToken);
}
