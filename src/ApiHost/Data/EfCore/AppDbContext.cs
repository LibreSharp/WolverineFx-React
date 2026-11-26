using ApiHost.Features.Todos;
#if (Identity)
using ApiHost.Identity;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
#endif
using Microsoft.EntityFrameworkCore;

namespace ApiHost.Data.EfCore;

#if (Identity)
public class AppDbContext : IdentityDbContext<ApplicationUser>, IDataProtectionKeyContext
#else
public class AppDbContext : DbContext
#endif
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Todo> Todos => Set<Todo>();

#if (Identity)
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    // ASP.NET Core Data Protection keys (they protect password reset tokens), shared by every
    // instance of the app and kept across restarts. See EfCoreIdentityExtensions.
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();
#endif

#if (Identity)
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<RefreshToken>()
            .HasIndex(t => t.TokenHash)
            .IsUnique();

        modelBuilder.Entity<RefreshToken>()
            .HasIndex(t => t.UserId);

        modelBuilder.Entity<Todo>()
            .HasIndex(t => t.OwnerId);
    }
#endif
}
