using ApiHost.Features.Todos;
#if (Identity)
using ApiHost.Identity;
#endif
#if (Saas)
using ApiHost.Billing;
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

    public DbSet<BillingAccount> BillingAccounts => Set<BillingAccount>();

    public DbSet<PeriodUsage> PeriodUsage => Set<PeriodUsage>();

    public DbSet<BillingInvoice> BillingInvoices => Set<BillingInvoice>();

    public DbSet<PaymentEventRecord> PaymentEvents => Set<PaymentEventRecord>();
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

        modelBuilder.Entity<BillingAccount>(account =>
        {
            account.Property(a => a.Id).ValueGeneratedNever();
            account.Property(a => a.PlanId).HasMaxLength(50);
            account.Property(a => a.Status).HasMaxLength(20);
            account.Property(a => a.Currency).HasMaxLength(3);
            account.Property(a => a.Provider).HasMaxLength(20);
            account.Property(a => a.CustomerId).HasMaxLength(255);
            account.Property(a => a.SubscriptionId).HasMaxLength(255);
            account.HasIndex(a => a.CustomerId);
            account.HasIndex(a => a.SubscriptionId);
        });
        modelBuilder.Entity<PeriodUsage>(usage =>
        {
            usage.ToTable("PeriodUsage");
            usage.HasKey(u => new { u.OrganisationId, u.LimitName, u.PeriodStart });
            usage.Property(u => u.LimitName).HasMaxLength(50);
        });
        modelBuilder.Entity<BillingInvoice>(invoice =>
        {
            invoice.Property(i => i.Id).HasMaxLength(255);
            invoice.Property(i => i.Provider).HasMaxLength(20);
            invoice.Property(i => i.Number).HasMaxLength(100);
            invoice.Property(i => i.Status).HasMaxLength(20);
            invoice.Property(i => i.Currency).HasMaxLength(3);
            invoice.HasIndex(i => i.OrganisationId);
        });
        modelBuilder.Entity<PaymentEventRecord>(paymentEvent =>
        {
            paymentEvent.ToTable("PaymentEvents");
            paymentEvent.Property(e => e.Id).HasMaxLength(300);
            paymentEvent.Property(e => e.Type).HasMaxLength(100);
        });
#endif
    }
#endif
}
