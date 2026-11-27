using System.Net;
using System.Net.Sockets;
using System.Text;
using ApiHost.Integrations.Secrets;
using Microsoft.Extensions.Options;

namespace ApiHost.Integrations.Webhooks;

// Posts one delivery to its subscription's URL, signed, and records how the receiver answered.
public class WebhookSender(IHttpClientFactory clients, SecretProtection protection, TimeProvider clock)
{
    public const string ClientName = "webhooks";
    public const string SecretPurpose = "Webhooks";

    // How much of the receiver's answer is kept.
    private const int MaxResponseLength = 1000;

    // True if the receiver answered with a 2xx status.
    public async Task<bool> SendAsync(WebhookSubscription subscription, WebhookDelivery delivery, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var secret = protection.Unprotect(SecretPurpose, subscription.Id.ToString(), subscription.ProtectedSecret);
        var id = delivery.EventId.ToString();
        using var request = new HttpRequestMessage(HttpMethod.Post, subscription.Url)
        {
            Content = new StringContent(delivery.Payload, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add(WebhookSignatures.IdHeader, id);
        request.Headers.Add(WebhookSignatures.TimestampHeader, now.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture));
        request.Headers.Add(WebhookSignatures.SignatureHeader, WebhookSignatures.Sign(secret, id, now, delivery.Payload));

        delivery.Attempts++;
        delivery.LastAttemptAt = now;
        try
        {
            using var response = await clients.CreateClient(ClientName).SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            delivery.StatusCode = (int)response.StatusCode;
            delivery.Response = body.Length > MaxResponseLength ? body[..MaxResponseLength] : body;
            if (response.IsSuccessStatusCode)
            {
                delivery.DeliveredAt = now;
                return true;
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            delivery.StatusCode = null;
            delivery.Response = exception is TaskCanceledException ? "No answer in time." : exception.Message;
        }

        return false;
    }

    // The HTTP client for webhooks: no redirects (a redirect could lead anywhere), a short timeout,
    // and, outside Development, no connections to private networks, so a webhook URL can't be used
    // to reach the app's own network or the cloud provider's metadata service. Checked on the
    // address actually connected to, after DNS, so a public name pointing at a private address is
    // refused too.
    public static void AddClient(IServiceCollection services)
        => services.AddHttpClient(ClientName, (provider, client) => client.Timeout = provider.GetRequiredService<IOptions<WebhookOptions>>().Value.Timeout)
            .ConfigurePrimaryHttpMessageHandler(provider => new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                ConnectCallback = provider.GetRequiredService<IOptions<WebhookOptions>>().Value.AllowPrivateNetworks ? null : ConnectToPublicAddressAsync,
            });

    private static async ValueTask<Stream> ConnectToPublicAddressAsync(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
    {
        var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, cancellationToken);
        var address = addresses.FirstOrDefault(PrivateNetworks.IsPublic)
            ?? throw new HttpRequestException($"{context.DnsEndPoint.Host} is not a public address.");
        var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(address, context.DnsEndPoint.Port, cancellationToken);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }
}

public static class PrivateNetworks
{
    // False for loopback, private, link-local (including cloud metadata at 169.254.169.254),
    // carrier-grade NAT, multicast and unspecified addresses.
    public static bool IsPublic(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any))
        {
            return false;
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return !(address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6UniqueLocal || address.IsIPv6Multicast);
        }

        var b = address.GetAddressBytes();
        return !(b[0] == 10
            || b[0] == 0
            || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
            || (b[0] == 192 && b[1] == 168)
            || (b[0] == 169 && b[1] == 254)
            || (b[0] == 100 && b[1] >= 64 && b[1] <= 127)
            || b[0] >= 224);
    }
}
