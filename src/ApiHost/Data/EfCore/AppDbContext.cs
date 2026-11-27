using ApiHost.Features.Todos;
#if (Identity)
using ApiHost.Features.Admin;
using ApiHost.Identity;
using ApiHost.Notifications;
#endif
#if (Saas)
using ApiHost.Billing;
using ApiHost.Integrations.ApiKeys;
using ApiHost.Integrations.Secrets;
using ApiHost.Integrations.Webhooks;
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

    // The back office's audit log (Features/Admin/AuditLog.cs).
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    // Notifications (Notifications/), and which ones each user wants emailed.
    public DbSet<Notification> Notifications => Set<Notification>();

    public DbSet<NotificationPreferences> NotificationPreferences => Set<NotificationPreferences>();
#endif
#if (Saas)

    public DbSet<Organisation> Organisations => Set<Organisation>();

    public DbSet<Membership> Memberships => Set<Membership>();

    public DbSet<OrganisationInvitation> OrganisationInvitations => Set<OrganisationInvitation>();

    public DbSet<BillingAccount> BillingAccounts => Set<BillingAccount>();

    public DbSet<PeriodUsage> PeriodUsage => Set<PeriodUsage>();

    public DbSet<BillingInvoice> BillingInvoices => Set<BillingInvoice>();

    public DbSet<PaymentEventRecord> PaymentEvents => Set<PaymentEventRecord>();

    public DbSet<FeatureOverride> FeatureOverrides => Set<FeatureOverride>();

    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();

    public DbSet<WebhookSubscription> WebhookSubscriptions => Set<WebhookSubscription>();

    public DbSet<WebhookDelivery> WebhookDeliveries => Set<WebhookDelivery>();

    public DbSet<OrganisationSecret> OrganisationSecrets => Set<OrganisationSecret>();

    public DbSet<IncomingWebhookRecord> IncomingWebhooks => Set<IncomingWebhookRecord>();
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

        modelBuilder.Entity<AuditEntry>(entry =>
        {
            entry.HasIndex(e => e.At);
            entry.HasIndex(e => e.TargetId);
            entry.Property(e => e.ActorId).HasMaxLength(450);
            entry.Property(e => e.ActorEmail).HasMaxLength(256);
            entry.Property(e => e.Action).HasMaxLength(50);
            entry.Property(e => e.TargetType).HasMaxLength(50);
            entry.Property(e => e.TargetId).HasMaxLength(4000);
        });

        modelBuilder.Entity<Notification>(notification =>
        {
            notification.HasIndex(n => new { n.UserId, n.CreatedAt });
            notification.Property(n => n.UserId).HasMaxLength(450);
            notification.Property(n => n.Category).HasMaxLength(50);
            notification.Property(n => n.Title).HasMaxLength(300);
            notification.Property(n => n.Body).HasMaxLength(2000);
            notification.Property(n => n.Link).HasMaxLength(500);
        });
        modelBuilder.Entity<NotificationPreferences>().Property(p => p.Id).HasMaxLength(450);
#if (Saas)

        ConfigureOrganisationsAndBilling(modelBuilder);
        ConfigureIntegrations(modelBuilder);
#endif
    }
#if (Saas)

    // Organisations (not tenanted: they are the tenants) and billing.
    private static void ConfigureOrganisationsAndBilling(ModelBuilder modelBuilder)
    {
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
        modelBuilder.Entity<FeatureOverride>(featureOverride =>
        {
            featureOverride.Property(o => o.Id).HasMaxLength(200);
            featureOverride.Property(o => o.Feature).HasMaxLength(100);
            featureOverride.Property(o => o.Scope).HasMaxLength(20);
            featureOverride.Property(o => o.ScopeKey).HasMaxLength(50);
        });
        modelBuilder.Entity<Organisation>().Property(o => o.SuspensionReason).HasMaxLength(500);
        modelBuilder.Entity<PaymentEventRecord>(paymentEvent =>
        {
            paymentEvent.ToTable("PaymentEvents");
            paymentEvent.Property(e => e.Id).HasMaxLength(300);
            paymentEvent.Property(e => e.Type).HasMaxLength(100);
        });
    }

    // API keys, webhooks and secrets: each row names its organisation.
    private static void ConfigureIntegrations(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ApiKey>(key =>
        {
            key.HasIndex(k => k.SecretHash).IsUnique();
            key.HasIndex(k => k.OrganisationId);
            key.Property(k => k.Name).HasMaxLength(100);
            key.Property(k => k.Hint).HasMaxLength(20);
            key.Property(k => k.SecretHash).HasMaxLength(64);
            key.Property(k => k.Scope).HasMaxLength(20);
            key.Property(k => k.CreatedByUserId).HasMaxLength(450);
        });
        modelBuilder.Entity<WebhookSubscription>(subscription =>
        {
            subscription.HasIndex(s => s.OrganisationId);
            subscription.Property(s => s.Url).HasMaxLength(2000);
            subscription.Property(s => s.Description).HasMaxLength(200);
        });
        modelBuilder.Entity<WebhookDelivery>(delivery =>
        {
            delivery.HasIndex(d => new { d.OrganisationId, d.CreatedAt });
            delivery.HasIndex(d => new { d.SubscriptionId, d.CreatedAt });
            delivery.Property(d => d.EventType).HasMaxLength(100);
            delivery.Property(d => d.Response).HasMaxLength(1000);
        });
        modelBuilder.Entity<OrganisationSecret>(secret =>
        {
            secret.Property(s => s.Id).HasMaxLength(110);
            secret.Property(s => s.Name).HasMaxLength(64);
            secret.Property(s => s.Hint).HasMaxLength(4);
            secret.Property(s => s.UpdatedByUserId).HasMaxLength(450);
            secret.HasIndex(s => s.OrganisationId);
        });
        modelBuilder.Entity<IncomingWebhookRecord>(record =>
        {
            record.ToTable("IncomingWebhooks");
            record.Property(r => r.Id).HasMaxLength(300);
            record.Property(r => r.Source).HasMaxLength(50);
        });
    }
#endif
#endif
}
