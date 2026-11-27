namespace ApiHost.Billing;

// What plans can include: features, which are on or off, and limits, which are numbers. A feature
// is checked with Entitlements.IsEnabledAsync, and a limit with Entitlements.CanAddAsync (Count
// limits) or TryUseAsync (PerPeriod limits). Add a constant here, then give it to plans below.
public static class PlanFeatures
{
    public const string TodoReminders = "TodoReminders";

    // Shown on the pricing page and in upgrade prompts.
    public static readonly IReadOnlyDictionary<string, string> Descriptions = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [TodoReminders] = "Todo reminders",
    };
}

public static class LimitKinds
{
    // How many the organisation has now, such as open Todos or members. Each has an ILimitUsage
    // that counts them.
    public const string Count = "Count";

    // How many the organisation has used this billing period, such as reminders sent. Starts
    // again at zero each period.
    public const string PerPeriod = "PerPeriod";
}

public record LimitDefinition(string Name, string Kind, string Description);

public static class PlanLimits
{
    public const string OpenTodos = "OpenTodos";
    public const string Members = "Members";
    public const string Reminders = "Reminders";

    public static readonly IReadOnlyList<LimitDefinition> All =
    [
        new(OpenTodos, LimitKinds.Count, "Open Todos"),
        new(Members, LimitKinds.Count, "Members"),
        new(Reminders, LimitKinds.PerPeriod, "Reminders a month"),
    ];

    public static LimitDefinition Get(string name)
        => All.FirstOrDefault(l => l.Name == name) ?? throw new ArgumentException($"Unknown limit '{name}'", nameof(name));
}

// A monthly price in the currency's smallest unit (cents for USD and EUR), as payment providers
// take it.
public record PlanPrice(string Currency, long MonthlyAmount);

// One version of a plan. Limits: a missing limit is unlimited.
public record Plan(
    string Id,
    int Version,
    string Name,
    string Description,
    IReadOnlyList<PlanPrice> Prices,
    IReadOnlyList<string> Features,
    IReadOnlyDictionary<string, int> Limits)
{
    public bool IsFree => Prices.All(p => p.MonthlyAmount == 0);

    public bool Includes(string feature) => Features.Contains(feature, StringComparer.Ordinal);

    public int? LimitOf(string limit) => Limits.TryGetValue(limit, out var value) ? value : null;

    public PlanPrice? PriceIn(string currency)
        => Prices.FirstOrDefault(p => string.Equals(p.Currency, currency, StringComparison.OrdinalIgnoreCase));
}

// The plans, in one place: the API, the pricing page (generated from GET /api/billing/plans; see
// docs/billing.md) and the payment provider's prices all come from here.
public static class PlanCatalogue
{
    public const string Free = "free";
    public const string Pro = "pro";

    // Every version of every plan. To change a plan's price, features or limits, add a new version
    // instead of editing one: organisations subscribed to an older version keep it (and its price)
    // until they change plan ("grandfathering"), and new subscriptions get the latest. Remove a
    // version only when no organisation is on it.
    public static readonly IReadOnlyList<Plan> All =
    [
        new(Free, 1, "Free", "For trying the app and for small teams.",
            [],
            [],
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [PlanLimits.OpenTodos] = 25,
                [PlanLimits.Members] = 3,
                [PlanLimits.Reminders] = 0,
            }),
        new(Pro, 1, "Pro", "For teams that rely on the app every day.",
            [new("USD", 1200), new("EUR", 1100)],
            [PlanFeatures.TodoReminders],
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [PlanLimits.Members] = 50,
                [PlanLimits.Reminders] = 1000,
            }),
    ];

    // The plans new subscribers can choose: the latest version of each, free first.
    public static IReadOnlyList<Plan> Offered
        => [.. All.GroupBy(p => p.Id, StringComparer.Ordinal).Select(g => g.MaxBy(p => p.Version)!).OrderBy(p => p.IsFree ? 0 : 1).ThenBy(p => p.Prices.Min(x => (long?)x.MonthlyAmount))];

    public static Plan? Find(string id, int version)
        => All.FirstOrDefault(p => p.Id == id && p.Version == version);

    public static Plan Latest(string id)
        => All.Where(p => p.Id == id).MaxBy(p => p.Version) ?? throw new ArgumentException($"Unknown plan '{id}'", nameof(id));
}
