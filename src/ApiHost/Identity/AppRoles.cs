namespace ApiHost.Identity;

// The app's roles. RoleSeeder creates them in the role table at startup; admins assign them on
// the admin page. To add a role, add a constant here and to All, then protect endpoints with
// [Authorize(Roles = AppRoles.YourRole)].
public static class AppRoles
{
    public const string Admin = "Admin";

    public static IReadOnlyList<string> All { get; } = [Admin];
}
