using ApiHost.Features.Todos;
#if (Identity)
using ApiHost.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
#endif
using Microsoft.EntityFrameworkCore;

namespace ApiHost.Data.EfCore;

#if (Identity)
public class AppDbContext : IdentityDbContext<ApplicationUser>
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
    }
#endif
}
