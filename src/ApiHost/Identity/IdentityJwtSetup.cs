using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using ApiHost.Identity.Email;
using ApiHost.Identity.External;
using ApiHost.Identity.Passkeys;
using ApiHost.Realtime;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Wolverine;
#if (Saas)
using ApiHost.Organisations;
#endif
#if (IsMarten)
using ApiHost.Identity.Marten;
#elif (IsEfCore)
using ApiHost.Identity.EfCore;
#endif

namespace ApiHost.Identity;

public static class IdentityJwtSetup
{
    public static void AddIdentityJwt(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.AddSingleton<JwtTokenService>();
        services.AddScoped<TokenIssuer>();
        services.Configure<AccountOptions>(configuration.GetSection(AccountOptions.SectionName));

        var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? throw new InvalidOperationException("Missing 'Jwt' configuration section");
        ValidateSigningKey(jwtOptions.SigningKey, environment);
        ClientUrl.Validate(configuration, environment);

        services.AddApplicationIdentity().AddAppTokenProviders();
        services.Configure<IdentityOptions>(ConfigureIdentity);

        services.AddJwtBearerAuthentication(jwtOptions);

        // Google and GitHub sign-in, each only when configured.
        services.AddAuthentication().AddExternalProviders(configuration);
        services.AddScoped<ExternalLoginService>();
        services.AddSingleton<ExternalLinkTickets>();

        // Passkeys (Identity's WebAuthn support); see Passkeys/.
        services.AddPasskeys(configuration);

        services.AddAuthorization();
#if (Saas)

        // Organisations: each user's data belongs to the organisation they work in (Organisations/).
        services.AddOrganisations(configuration);
#endif

        // Limits login, registration and password attempts per client IP (see AuthRateLimiting).
        services.AddAuthRateLimiting(configuration);

        // UserManager<T> takes IServiceProvider in its constructor, so Wolverine can't build it inline
        // in generated endpoint code, and its default ServiceLocationPolicy.NotAllowed then fails the
        // request. Explicitly allow resolving it from the container.
        services.ConfigureWolverine(opts =>
        {
            opts.CodeGeneration.AlwaysUseServiceLocationFor<UserManager<ApplicationUser>>();
            opts.CodeGeneration.AlwaysUseServiceLocationFor<SignInManager<ApplicationUser>>();
            opts.CodeGeneration.AlwaysUseServiceLocationFor<TokenIssuer>();
            opts.CodeGeneration.AlwaysUseServiceLocationFor<ExternalLoginService>();
            opts.CodeGeneration.AlwaysUseServiceLocationFor<IPasskeyHandler<ApplicationUser>>();
        });

        // A ready-made login for local development only.
        if (environment.IsDevelopment())
        {
            services.AddHostedService<DevelopmentUserSeeder>();
        }

        // After the development user, so it can be made an admin (Accounts:AdminEmails).
        services.AddHostedService<RoleSeeder>();

        services.AddAccountEmail(configuration);
    }

    // ASP.NET Core Identity's rules for accounts.
    private static void ConfigureIdentity(IdentityOptions options)
    {
        options.User.RequireUniqueEmail = true;

        // Identity's schema with passkeys (the AspNetUserPasskeys table, with EF Core).
        options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;

        // Login is refused until the user opens the link in the confirmation email sent when
        // they register (see LoginEndpoint and RegisterEndpoint).
        options.SignIn.RequireConfirmedEmail = true;

        // Five wrong passwords in a row lock the account for 15 minutes (see LoginEndpoint).
        // An admin can also lock an account indefinitely (see Lockout.cs).
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    }

    private static void AddJwtBearerAuthentication(this IServiceCollection services, JwtOptions jwtOptions)
    {
        // AddIdentity (in AddApplicationIdentity, which must run first) makes Identity's cookie
        // scheme the default, which would answer an unauthenticated API call with a 302 redirect to
        // a login page. This is a bearer-token API, so JWT must be the default for authenticate and
        // challenge (401), set after AddIdentity.
        services.AddAuthentication(options =>
            {
                options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtOptions.Audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
                    ClockSkew = TimeSpan.FromSeconds(30)
                };

                // Browsers can't set an Authorization header on a WebSocket, so the SignalR
                // client sends the token as ?access_token=; accept it for the hub only.
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var token = context.Request.Query["access_token"].ToString();
                        if (!string.IsNullOrEmpty(token) && context.HttpContext.Request.Path.StartsWithSegments(AppHub.Path, StringComparison.OrdinalIgnoreCase))
                        {
                            context.Token = token;
                        }

                        return Task.CompletedTask;
                    }
                };
            });
    }

    private static void AddAccountEmail(this IServiceCollection services, IConfiguration configuration)
    {
        // Account emails go through SMTP when it's configured (Development uses Mailpit, from
        // docker-compose.yml); otherwise nothing is sent and a warning is logged. See Identity/Email/.
        services.Configure<SmtpOptions>(configuration.GetSection(SmtpOptions.SectionName));
        if (!string.IsNullOrWhiteSpace(configuration[$"{SmtpOptions.SectionName}:Host"]))
        {
            services.AddSingleton<IEmailSender, SmtpEmailSender>();
        }
        else
        {
            services.AddSingleton<IEmailSender, NotConfiguredEmailSender>();
        }
    }

    // Fail at startup rather than sign tokens with a missing, weak, or publicly known key.
    private static void ValidateSigningKey(string signingKey, IHostEnvironment environment)
    {
        if (Encoding.UTF8.GetByteCount(signingKey) < JwtOptions.MinimumSigningKeyBytes)
        {
            throw new InvalidOperationException(
                $"'Jwt:SigningKey' must be at least {JwtOptions.MinimumSigningKeyBytes} bytes. " +
                "Set it via user-secrets, the 'Jwt__SigningKey' environment variable, or a secret store.");
        }

        if (!environment.IsDevelopment() && signingKey.Contains(JwtOptions.DevOnlyKeyMarker, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "'Jwt:SigningKey' is the development key. Configure a real secret outside Development.");
        }
    }
}
