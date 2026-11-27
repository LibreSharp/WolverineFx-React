using ApiHost.DataExport;
using Microsoft.Extensions.Options;
using Wolverine;

namespace ApiHost.Notifications;

public static class NotificationSetup
{
    // Notifications in the header, emailed unless turned off (see Notification.cs).
    public static void AddNotifications(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<NotificationOptions>(configuration.GetSection(NotificationOptions.SectionName));
        services.AddScoped<INotificationStore, NotificationStore>();
        services.AddSingleton<UnsubscribeTokens>();
        services.AddScoped<IPersonalDataSource, NotificationsPersonalData>();
        services.ConfigureWolverine(opts => opts.CodeGeneration.AlwaysUseServiceLocationFor<INotificationStore>());
    }
}

// The user's notifications and email choices, in "Download your data".
public class NotificationsPersonalData(INotificationStore store, IOptions<NotificationOptions> options) : IPersonalDataSource
{
    public string Section => "notifications";

    public async Task<object?> ExportAsync(string userId, CancellationToken cancellationToken)
        => new
        {
            Notifications = await store.ListAsync(userId, options.Value.KeepPerUser, cancellationToken),
            EmailsTurnedOff = (await store.PreferencesAsync(userId, cancellationToken)).EmailOff,
        };
}
