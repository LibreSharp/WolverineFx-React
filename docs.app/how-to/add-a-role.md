# Add a role

Roles are constants in `src/ApiHost/Identity/AppRoles.cs`. The app creates them in the database
when it starts, and the admin page offers every role in `AppRoles.All` as a checkbox.

1. Add the role:

   ```csharp
   public static class AppRoles
   {
       public const string Admin = "Admin";
       public const string Editor = "Editor";

       public static IReadOnlyList<string> All { get; } = [Admin, Editor];
   }
   ```

2. Protect endpoints with it: `[Authorize(Roles = AppRoles.Editor)]`. Several roles are allowed
   with a comma: `[Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Editor}")]`.
3. In the client, check the role from `useMe()` (`me.data?.roles.includes('Editor')`) to show or
   hide links and pages, as `RequireAdmin` in `features/auth/routeGuards.tsx` does for the admin
   page. The API's check is what protects the data; the client's only tidies the interface.
4. Restart the app, then give people the role on the admin page. It reaches their access token at
   their next token refresh (within a few minutes) or login.
<!--#if (IncludeTests) -->
5. Add a test that the endpoint refuses users without the role (403), like
   `only_admins_can_use_the_admin_endpoints` in `AdminEndpointsTests.cs`.
<!--#endif -->

Removing a role from `AppRoles` doesn't delete it from the database or from users; remove it from
users on the admin page first.
