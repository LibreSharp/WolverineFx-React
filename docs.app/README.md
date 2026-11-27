# Documentation

How ApiHost works, how to change it, and how to run it in production. Start with the main
[README](../README.md) to run it locally.

## Guides

- [Architecture](architecture.md): how the example feature works, pattern by pattern.
<!--#if (Identity) -->
- [Accounts](accounts.md): sign-in, emails, settings, lockout, roles and the admin page,
  Google/GitHub and passkeys.
<!--#endif -->
<!--#if (Saas) -->
- [Plans, trials and limits](billing.md): the plan catalogue, the reverse trial, feature flags,
  and checking features and limits.
- [Integrations](integrations.md): API keys, the MCP endpoint for AI assistants, webhooks (sent
  and received) and encrypted secrets.
<!--#endif -->
- [Security](security.md): what the app does to protect users and data, and what's left to you.
- [Testing](testing.md): the test suites, how to run them, and how to add tests.
- [Observability](observability.md): health checks, traces, metrics and logs.
- [Deployment](deployment.md): publishing, configuration, and running in production. For a
  step-by-step guide to a small server, see [deploy/README.md](../deploy/README.md).

## How-to guides

- [Add a feature](how-to/add-a-feature.md)
- [Put Cloudflare in front](how-to/put-cloudflare-in-front.md)
<!--#if (IsEfCore) -->
- [Change the database schema (EF Core migrations)](how-to/add-a-migration.md)
<!--#endif -->
<!--#if (Identity) -->
- [Send email through your email provider](how-to/configure-email.md)
- [Set up "Continue with Google" and "Continue with GitHub"](how-to/set-up-external-sign-in.md)
- [Add a role](how-to/add-a-role.md)
- [Make someone an admin](how-to/make-an-admin.md)
- [Change the JWT signing key](how-to/rotate-the-signing-key.md)
<!--#endif -->
