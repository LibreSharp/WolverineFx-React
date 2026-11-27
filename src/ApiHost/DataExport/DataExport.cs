using System.Text.Json;
using ApiHost.Identity;
using Microsoft.AspNetCore.Identity;
using Wolverine;

namespace ApiHost.DataExport;

// "Download your data": everything the app stores about a user, as one JSON file, to go with
// account deletion. Privacy laws such as the GDPR (right of access and data portability) and
// Thailand's PDPA give people this right. Each feature that stores data about users adds a
// section by registering an IPersonalDataSource, so this code needs no knowledge of features.
public interface IPersonalDataSource
{
    // The section's name in the file, such as "todos".
    string Section { get; }

    Task<object?> ExportAsync(string userId, CancellationToken cancellationToken);
}
#if (Saas)

// An organisation's data, for its owners to download (an organisation is often a customer
// moving their data elsewhere). Each feature that stores organisation data adds a section.
public interface IOrganisationDataSource
{
    string Section { get; }

    Task<object?> ExportAsync(Guid organisationId, CancellationToken cancellationToken);
}
#endif

public static class DataExportFile
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    // { "exportedAt": ..., "<section>": ..., ... } as a file to save.
    public static async Task<IResult> BuildAsync<TKey>(TKey key, IEnumerable<(string Section, Func<TKey, CancellationToken, Task<object?>> Export)> sources, string fileName, TimeProvider clock, CancellationToken cancellationToken)
    {
        var file = new Dictionary<string, object?>(StringComparer.Ordinal) { ["exportedAt"] = clock.GetUtcNow() };
        foreach (var (section, export) in sources)
        {
            file[section] = await export(key, cancellationToken);
        }

        return TypedResults.File(JsonSerializer.SerializeToUtf8Bytes(file, Options), "application/json", fileName);
    }
}

// Puts the user's sections together.
public class PersonalDataExport(IEnumerable<IPersonalDataSource> sources, TimeProvider clock)
{
    public Task<IResult> FileAsync(string userId, CancellationToken cancellationToken)
        => DataExportFile.BuildAsync(userId, sources.Select(s => (s.Section, (Func<string, CancellationToken, Task<object?>>)s.ExportAsync)), "my-data.json", clock, cancellationToken);
}
#if (Saas)

// Puts an organisation's sections together.
public class OrganisationDataExport(IEnumerable<IOrganisationDataSource> sources, TimeProvider clock)
{
    public Task<IResult> FileAsync(Guid organisationId, CancellationToken cancellationToken)
        => DataExportFile.BuildAsync(organisationId, sources.Select(s => (s.Section, (Func<Guid, CancellationToken, Task<object?>>)s.ExportAsync)), "organisation-data.json", clock, cancellationToken);
}
#endif

public static class DataExportSetup
{
    public static void AddDataExport(this IServiceCollection services)
    {
        services.AddScoped<PersonalDataExport>();
        services.AddScoped<IPersonalDataSource, AccountPersonalData>();
#if (Saas)
        services.AddScoped<OrganisationDataExport>();
#endif
        services.ConfigureWolverine(opts =>
        {
            opts.CodeGeneration.AlwaysUseServiceLocationFor<PersonalDataExport>();
#if (Saas)
            opts.CodeGeneration.AlwaysUseServiceLocationFor<OrganisationDataExport>();
#endif
        });
    }
}

// The account itself: profile, roles, linked sign-ins and passkeys. Never the password hash or
// security stamps, which are secrets rather than data about the person.
public class AccountPersonalData(UserManager<ApplicationUser> users) : IPersonalDataSource
{
    public string Section => "account";

    public async Task<object?> ExportAsync(string userId, CancellationToken cancellationToken)
    {
        if (await users.FindByIdAsync(userId) is not { } user)
        {
            return null;
        }

        var passkeys = await users.GetPasskeysAsync(user);
        return new
        {
            user.Id,
            user.Email,
            user.EmailConfirmed,
            user.DisplayName,
            Roles = await users.GetRolesAsync(user),
            LinkedSignIns = (await users.GetLoginsAsync(user)).Select(l => l.LoginProvider),
            Passkeys = passkeys.Select(p => new { Name = p.Name ?? "Passkey", p.CreatedAt }),
            user.LockoutEnd,
        };
    }
}
