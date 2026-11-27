# Change the database schema (EF Core migrations)

The app uses EF Core migrations, in `src/ApiHost/Data/EfCore/Migrations/`. They're applied
automatically when the app starts (`MigrateDatabaseOnStartup`), in every environment.

1. Install the EF Core tools once: `dotnet tool install --global dotnet-ef`.
2. Change the model: an entity class, a `DbSet` in `Data/EfCore/AppDbContext.cs`, or
   configuration in its `OnModelCreating`.
3. Create a migration from the repository root:

   ```bash
   dotnet ef migrations add AddNotes --project src/ApiHost --output-dir Data/EfCore/Migrations
   ```

4. Read the generated migration before committing it. EF Core warns when a change may lose data
   (dropping or shortening a column); rename columns explicitly rather than letting EF drop and
   re-add them.
5. Run the app (or the tests) to apply it.

Don't edit a migration that has already run anywhere else: add a new one. To undo the last
migration before it has been applied anywhere, use `dotnet ef migrations remove --project src/ApiHost`.

To check that the model and migrations agree (useful in CI):
`dotnet ef migrations has-pending-model-changes --project src/ApiHost`.
