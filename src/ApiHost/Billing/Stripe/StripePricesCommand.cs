using JasperFx.CommandLine;
using Microsoft.Extensions.Options;
using Stripe;

namespace ApiHost.Billing.Stripe;

// `dotnet run -- stripe-prices`: creates the plan catalogue's products and prices in Stripe
// (Billing/Plans.cs), one price per plan version and currency, with the lookup keys that checkout
// finds them by ("pro-v1-usd"). Safe to run again: it only adds what's missing. Run it with the
// test key while developing, and with the live key before going live.
[Description("Creates the plans' products and prices in Stripe, from Billing/Plans.cs", Name = "stripe-prices")]
public class StripePricesCommand : JasperFxAsyncCommand<NetCoreInput>
{
    public StripePricesCommand()
    {
        Usage("Create the Stripe products and prices");
    }

    public override async Task<bool> Execute(NetCoreInput input)
    {
        using var host = input.BuildHost();
        var options = host.Services.GetRequiredService<IOptions<StripeOptions>>().Value;
        if (string.IsNullOrEmpty(options.SecretKey))
        {
            Console.Error.WriteLine($"Set {StripeOptions.SectionName}:SecretKey first (docs/billing.md).");
            return false;
        }

        var client = new StripeClient(options.SecretKey);
        var products = new ProductService(client);
        var prices = new PriceService(client);
        var ok = true;

        foreach (var plan in PlanCatalogue.All.Where(p => !p.IsFree))
        {
            var productId = $"plan_{plan.Id}";
            try
            {
                await products.GetAsync(productId);
            }
            catch (StripeException e) when (e.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
            {
                await products.CreateAsync(new ProductCreateOptions { Id = productId, Name = plan.Name, Description = plan.Description });
                Console.WriteLine($"Created product {productId}");
            }

            foreach (var price in plan.Prices)
            {
                var key = StripePaymentProvider.LookupKey(plan, price.Currency);
                var existing = (await prices.ListAsync(new PriceListOptions { LookupKeys = [key] })).Data.FirstOrDefault();
                if (existing is null)
                {
                    await prices.CreateAsync(new PriceCreateOptions
                    {
                        Product = productId,
                        Currency = price.Currency.ToLowerInvariant(),
                        UnitAmount = price.MonthlyAmount,
                        Recurring = new PriceRecurringOptions { Interval = "month" },
                        LookupKey = key,
                        Nickname = $"{plan.Name} v{plan.Version} ({price.Currency})",
                    });
                    Console.WriteLine($"Created price {key}");
                }
                else if (existing.UnitAmount != price.MonthlyAmount)
                {
                    // Stripe prices can't change: add a new plan version instead (Billing/Plans.cs).
                    Console.Error.WriteLine($"Price {key} is {existing.UnitAmount} in Stripe but {price.MonthlyAmount} in Plans.cs: add a new plan version rather than changing a price.");
                    ok = false;
                }
            }
        }

        return ok;
    }
}
