using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace ApiHost.Identity;

// A row in Identity's AspNetUsers table (Data/EfCore/AppDbContext.cs). Roles, external logins
// and passkeys are in Identity's own tables.
public class ApplicationUser : IdentityUser
{
    // Optional name shown in the app instead of the email address.
    [MaxLength(100)]
    public string? DisplayName { get; set; }
}
