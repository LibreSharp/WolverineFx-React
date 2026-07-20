using Api.Features.Todos;
#if (Identity)
using Api.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
#endif
using Microsoft.EntityFrameworkCore;

namespace Api.Data.EfCore;

#if (Identity)
public class AppDbContext : IdentityDbContext<ApplicationUser>
#else
public class AppDbContext : DbContext
#endif
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Todo> Todos => Set<Todo>();

#if (Identity)
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
#endif
}
