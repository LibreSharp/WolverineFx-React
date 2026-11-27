# Put Cloudflare in front

Cloudflare's proxy adds a cache and DDoS protection in front of the server in `deploy/`
(Caddy, then the app). Three things need setting up: TLS between Cloudflare and Caddy, what
Cloudflare caches, and telling Caddy to trust Cloudflare so the app sees each visitor's real
address.

## 1. TLS: "Full (strict)"

Caddy has its own certificate (from Let's Encrypt), so Cloudflare can connect to it securely.

1. Add the site to Cloudflare and point the domain's `A`/`AAAA` records at your server, at first
   as **DNS only** (grey cloud). Start the server and open the site, so Caddy gets its
   certificate.
2. In Cloudflare, set **SSL/TLS → Overview** to **Full (strict)**.
3. Switch the DNS records to **Proxied** (orange cloud).

Leave Cloudflare's **Always Use HTTPS** off: Caddy already redirects HTTP to HTTPS, except for the
path Let's Encrypt uses to renew the certificate, which must still reach Caddy over HTTP.

**WebSockets** must be on (**Network → WebSockets**, on by default): the app's live updates use
them.

## 2. Caching

The app already sends the right headers, and Cloudflare's defaults follow them:

| Path | The app sends | Cloudflare |
|---|---|---|
| `/assets/*` (the built client, names contain a content hash) | `public, max-age=31536000, immutable` | Cached at the edge for a year |
| Pages (the app's HTML) | `no-cache` | Fetched each time, so a deployment shows at once |
| `/api/*` | Not cacheable | Never cached |

Add one **Cache Rule** to make the last row explicit: when the URI path starts with `/api/`,
**Bypass cache**. Never use "Cache Everything" on `/api/`: responses there belong to one user.

## 3. Real visitor addresses

Behind Cloudflare, every request reaches Caddy from one of Cloudflare's addresses. Unless Caddy
trusts Cloudflare, the app sees those addresses instead of visitors'.
<!--#if (Identity) -->
The sign-in rate limits would then count attempts per Cloudflare server rather than per visitor,
locking out unrelated people and letting an attacker spread attempts.
<!--#endif -->

Add this block at the top of `deploy/Caddyfile`, and `header_up` inside `reverse_proxy`:

```caddyfile
{
	servers {
		# Cloudflare's addresses, from https://www.cloudflare.com/ips/ (check for changes).
		trusted_proxies static 173.245.48.0/20 103.21.244.0/22 103.22.200.0/22 103.31.4.0/22 141.101.64.0/18 108.162.192.0/18 190.93.240.0/20 188.114.96.0/20 197.234.240.0/22 198.41.128.0/17 162.158.0.0/15 104.16.0.0/13 104.24.0.0/14 172.64.0.0/13 131.0.72.0/22 2400:cb00::/32 2606:4700::/32 2803:f800::/32 2405:b500::/32 2405:8100::/32 2a06:98c0::/29 2c0f:f248::/32
		# The visitor's address, as Cloudflare reports it.
		client_ip_headers CF-Connecting-IP
	}
}

{$DOMAIN} {
	encode zstd gzip
	reverse_proxy app:8080 {
		health_uri /health/ready
		# Pass the app only the visitor's address.
		header_up X-Forwarded-For {client_ip}
	}
}
```

Caddy only believes `CF-Connecting-IP` on requests that come from those addresses; anyone else
sending it is ignored, so it can't be forged. The app reads `X-Forwarded-For` because
`ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` is set in `deploy/docker-compose.yml`.

Restart Caddy (`docker compose restart caddy`), then check the app's logs show visitors' addresses
rather than Cloudflare's.

## Optional: only accept traffic from Cloudflare

Visitors can still reach your server directly by its IP address, bypassing Cloudflare. To stop
that, allow ports 80 and 443 only from Cloudflare's addresses in the server's firewall (for
example with `ufw`), keeping SSH open for yourself. Let's Encrypt renewals still work, since they
come through Cloudflare.
