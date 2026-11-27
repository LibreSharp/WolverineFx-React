# Set up "Continue with Google" and "Continue with GitHub"

Each provider is off until its client ID and secret are set. Once set, the login and register
pages show its button, and users can link it in Settings. Only email addresses the provider has
verified are accepted (see [Accounts](../accounts.md#google-and-github)).

Each provider sends people back to a callback URL on your site, which you register with it:

| Provider | Callback URL |
|---|---|
| Google | `https://your-domain/api/auth/external/signin-google` |
| GitHub | `https://your-domain/api/auth/external/signin-github` |

In Development the callback goes to the API itself, not the Vite dev server:
`http://localhost:5133/api/auth/external/signin-google` (or `signin-github`).

## Google

1. In the [Google Cloud console](https://console.cloud.google.com/), create a project (or pick
   one).
2. Set up the OAuth consent screen (Google Auth Platform): the app's name, a support email, and
   your domain. Scopes: `openid`, `email` and `profile` are enough.
3. Create credentials of type **OAuth client ID**, application type **Web application**, and add
   the callback URLs above under **Authorized redirect URIs** (production and, for development,
   the `localhost` one).
4. Copy the client ID and secret into the app's settings:
   `Authentication__Google__ClientId` and `Authentication__Google__ClientSecret`.

While the consent screen is in testing, only the test users you list can sign in; publish it for
everyone.

## GitHub

1. In GitHub, open **Settings → Developer settings → OAuth Apps → New OAuth App**.
2. Set the homepage URL to your site and the **Authorization callback URL** to the GitHub
   callback above. A GitHub OAuth app has one callback URL, so create a separate one for
   development (`http://localhost:5133/...`).
3. Generate a client secret, and copy the client ID and secret into the app's settings:
   `Authentication__GitHub__ClientId` and `Authentication__GitHub__ClientSecret`.

The app asks for the `user:email` scope, so it can see whether the account's primary email is
verified, even when it's private.

## Keep the secrets out of source control

- In Development, use user secrets. In `src/ApiHost`, run `dotnet user-secrets init` once, then:

  ```bash
  dotnet user-secrets set Authentication:Google:ClientId "<client id>"
  dotnet user-secrets set Authentication:Google:ClientSecret "<client secret>"
  ```

- In production, set environment variables, or the `GOOGLE_*`/`GITHUB_*` values in `deploy/.env`.

## Check it

Open the login page: "Continue with Google" (or GitHub) appears. Sign in with it, then check
Settings → Sign-in methods lists the provider. Behind a reverse proxy, the app must see HTTPS and
your domain (`ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`), or the provider rejects the callback URL
as not matching.
