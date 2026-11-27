# Add a feature

A feature is a folder under `src/ApiHost/Features/` with its endpoints, request records,
validators, messages and handlers, often in one file each, plus a folder under
`src/Client/src/features/`. Nothing needs registering: Wolverine finds endpoints by their
attributes and handlers by name. This guide adds a small "Notes" feature; the Todo feature shows
every pattern in more depth (see [Architecture](../architecture.md)).

## 1. The API

Create `src/ApiHost/Features/Notes/Notes.cs`:

```csharp
<!--#if (IsMarten) -->
using Marten;
<!--#else -->
using ApiHost.Data.EfCore;
using Microsoft.EntityFrameworkCore;
<!--#endif -->
<!--#if (Identity) -->
using System.Security.Claims;
using ApiHost.Identity;
using Microsoft.AspNetCore.Authorization;
<!--#endif -->
using FluentValidation;
using Wolverine.Http;

namespace ApiHost.Features.Notes;

public class Note
{
    public Guid Id { get; set; }
    public string Text { get; set; } = string.Empty;
<!--#if (Identity) -->
    public string OwnerId { get; set; } = string.Empty;
<!--#endif -->
}

public record AddNote(string Text);

// Runs before the endpoint; an invalid request gets a 400 ProblemDetails response.
public class AddNoteValidator : AbstractValidator<AddNote>
{
    public AddNoteValidator() => RuleFor(x => x.Text).NotEmpty().MaximumLength(500);
}

public static class NoteEndpoints
{
<!--#if (Identity) -->
    [Authorize]
<!--#endif -->
    [WolverinePost("/api/notes")]
<!--#if (IsMarten && Identity) -->
    public static Note Add(AddNote request, IDocumentSession session, ClaimsPrincipal user)
    {
        var note = new Note { Id = Guid.CreateVersion7(), Text = request.Text, OwnerId = user.GetUserId() };
        session.Store(note);   // saved after the endpoint runs, with any messages it returns
        return note;
    }
<!--#elseif (IsMarten) -->
    public static Note Add(AddNote request, IDocumentSession session)
    {
        var note = new Note { Id = Guid.CreateVersion7(), Text = request.Text };
        session.Store(note);   // saved after the endpoint runs, with any messages it returns
        return note;
    }
<!--#elseif (Identity) -->
    public static Note Add(AddNote request, AppDbContext db, ClaimsPrincipal user)
    {
        var note = new Note { Id = Guid.CreateVersion7(), Text = request.Text, OwnerId = user.GetUserId() };
        db.Notes.Add(note);    // saved after the endpoint runs, with any messages it returns
        return note;
    }
<!--#else -->
    public static Note Add(AddNote request, AppDbContext db)
    {
        var note = new Note { Id = Guid.CreateVersion7(), Text = request.Text };
        db.Notes.Add(note);    // saved after the endpoint runs, with any messages it returns
        return note;
    }
<!--#endif -->

<!--#if (Identity) -->
    [Authorize]
<!--#endif -->
    [WolverineGet("/api/notes")]
<!--#if (IsMarten && Identity) -->
    public static Task<IReadOnlyList<Note>> List(IQuerySession session, ClaimsPrincipal user)
    {
        var ownerId = user.GetUserId();
        return session.Query<Note>().Where(n => n.OwnerId == ownerId).ToListAsync();
    }
<!--#elseif (IsMarten) -->
    public static Task<IReadOnlyList<Note>> List(IQuerySession session)
        => session.Query<Note>().ToListAsync();
<!--#elseif (Identity) -->
    public static Task<List<Note>> List(AppDbContext db, ClaimsPrincipal user)
    {
        var ownerId = user.GetUserId();
        return db.Notes.Where(n => n.OwnerId == ownerId).ToListAsync();
    }
<!--#else -->
    public static Task<List<Note>> List(AppDbContext db) => db.Notes.ToListAsync();
<!--#endif -->
}
```

<!--#if (IsMarten) -->
Marten stores `Note` as a JSON document and creates its table when the app starts.
<!--#if (Identity) -->
Index the owner for fast per-user queries in `Data/Marten/MartenPersistence.cs`:
`opts.Schema.For<Note>().Index(n => n.OwnerId);`
<!--#endif -->
<!--#else -->
Add the table: in `Data/EfCore/AppDbContext.cs` add `public DbSet<Note> Notes => Set<Note>();`
(and, with an owner, an index on `OwnerId` in `OnModelCreating`), then
[add a migration](add-a-migration.md).
<!--#endif -->

Other things a feature often needs:

- **Background work:** return a message from the endpoint and handle it in a class whose name ends
  in `Handler`, like `TodoCompleted` and `TodoCompletedHandler`. It's saved with your data
  (the outbox) and runs even after a restart.
- **Live updates:** route a message to browsers with `opts.PublishMessage<YourMessage>().ToSignalR();`
  in `Program.cs`, and listen for it in the client like `features/todos/useTodoRealtime.ts`.
<!--#if (Identity) -->
- **Users' data:** handle `AccountDeleted` and delete the user's notes, like
  `Features/Todos/DeleteTodosOfDeletedAccountHandler.cs`.
- **Security:** answer 404, not 403, when someone asks for another user's record, so its
  existence isn't revealed.
<!--#endif -->

## 2. The client

1. Run the API and update the client's types: `npm run gen:api` in `src/Client`.
2. Create `src/Client/src/features/notes/` with TanStack Query hooks (copy
   `features/todos/todoQueries.ts`) and a page component.
3. Add a route in `src/Client/src/router.tsx`, with a `handle` giving its title (and
   `index: true` only if search engines should list it), and a `NavItem` for it. Add its path to
   `src/Client/src/routes.ts` too; a unit test fails until they match.
<!--#if (Identity) -->
   Put the route inside `RequireAuth` if it needs a login.
<!--#endif -->

## 3. Tests
<!--#if (IncludeTests) -->

- An API test in `tests/ApiHost.Tests` (copy `TodoEndpointsTests.cs`): adding, listing and
  validation.
<!--#if (Identity) -->
- That one user can't see another's notes (copy `TodoAuthorizationTests.cs`).
<!--#endif -->
<!--#endif -->
- A client test next to the page (`NotesPage.test.tsx`; copy `features/todos/TodoList.test.tsx`).

See [Testing](../testing.md).
