using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
#if (IsMarten)
using ApiHost.Identity.Marten;
#elif (IsEfCore)
using ApiHost.Identity.EfCore;
#endif

namespace ApiHost.Identity;

public static class IdentityJwtSetup
{
    public static void AddIdentityJwt(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.AddSingleton<JwtTokenService>();

        var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? throw new InvalidOperationException("Missing 'Jwt' configuration section");

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
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
            });

        services.AddAuthorization();

        services.AddApplicationIdentity();
    }
}
