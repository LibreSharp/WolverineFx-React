using ApiHost.Features.Todos;
#if (Identity)
using ApiHost.Identity;
#endif
#if (Saas)
using ApiHost.Organisations;
#endif
#if (Identity)
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
#if (Saas)

    public DbSet<Organisation> Organisations => Set<Organisation>();

    public DbSet<Membership> Memberships => Set<Membership>();

    public DbSet<OrganisationInvitation> OrganisationInvitations => Set<OrganisationInvitation>();
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
#if (Saas)

        modelBuilder.Entity<Organisation>().Property(o => o.Name).HasMaxLength(OrganisationNames.MaxLength);
        modelBuilder.Entity<Membership>().HasIndex(m => m.UserId);
        modelBuilder.Entity<Membership>().HasIndex(m => new { m.OrganisationId, m.UserId }).IsUnique();
        modelBuilder.Entity<Membership>().Property(m => m.Role).HasMaxLength(20);
        modelBuilder.Entity<OrganisationInvitation>().HasIndex(i => i.TokenHash).IsUnique();
        modelBuilder.Entity<OrganisationInvitation>().HasIndex(i => new { i.OrganisationId, i.NormalizedEmail });
        modelBuilder.Entity<OrganisationInvitation>().Property(i => i.Email).HasMaxLength(256);
        modelBuilder.Entity<OrganisationInvitation>().Property(i => i.NormalizedEmail).HasMaxLength(256);
        modelBuilder.Entity<OrganisationInvitation>().Property(i => i.Role).HasMaxLength(20);
#endif
    }
#endif
}
