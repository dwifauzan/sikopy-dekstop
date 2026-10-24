# Database

SIKOPY uses PostgreSQL locally. The database schema is stored in numbered SQL
migrations so every team member can recreate the same database structure.

## Fresh database with pgAdmin

### 1. Create the database

1. Open **pgAdmin** and connect to your local PostgreSQL server.
2. In the left panel, right-click **Databases** and select **Create >
   Database...**.
3. Set **Database** to `sikopy`, leave the owner as your PostgreSQL user, then
   click **Save**.

### 2. Open the Query Tool

1. Expand **Databases** and select the `sikopy` database.
2. Right-click `sikopy` and choose **Query Tool**.
3. Open `database/schema.sql` from this repository:
   - Select **File > Open File** in Query Tool, or
   - Copy the contents of the file into the query editor.

### 3. Run the schema

Click the **Execute/Play** button or press `F5`.

`database/schema.sql` is standalone and does not use `\ir`, so it can also be
opened and executed directly in TablePlus.

### TablePlus

1. Connect to the PostgreSQL server.
2. Select the `sikopy` database.
3. Open `database/schema.sql` in a new SQL query.
4. Run the complete file.

Use `database/migrations/001_initial_schema.sql` for the same initial schema
when working with the migration workflow.

### If you see `current transaction is aborted`

PostgreSQL stops executing statements after the first error inside a
transaction. The message shown is usually a follow-up error; inspect the first
error in the output.

For a fresh import, the most common cause is that some tables already exist.
Drop and recreate the empty local `sikopy` database, then run
`database/schema.sql` again. Do not drop a database containing data you need.

### 4. Verify the tables

In the left panel, right-click the `sikopy` database and choose **Refresh**.
Expand:

```text
sikopy
└── Schemas
    └── public
        └── Tables
```

You should see these nine tables:

```text
jenis_biji_kopi_mentah
metode_proses
pengguna
biji_kopi_mentah
batch_roasting
biji_kopi_roasted
riwayat_stok
penjualan
detail_penjualan
```

You can also verify from Query Tool:

```sql
SELECT table_name
FROM information_schema.tables
WHERE table_schema = 'public'
ORDER BY table_name;
```

## Fresh database with the command line

Create a local database, then import the complete schema:

```sql
CREATE DATABASE sikopy;
```

```bash
psql -U postgres -d sikopy -f database/schema.sql
```

Alternatively, run the initial migration directly:

```bash
psql -U postgres -d sikopy -f database/migrations/001_initial_schema.sql
```

## Reset a local development database

Only do this if the database contains no data that needs to be kept. In pgAdmin,
right-click the `sikopy` database, choose **Delete/Drop**, recreate it, and run
the import steps again. Dropping the database permanently deletes its data.

## Schema changes

- Add a new numbered file under `database/migrations/` for every schema change.
- Never edit a migration that another developer may already have applied.
- Apply migrations in numeric order.
- Keep seed/development data separate from schema migrations.

The schema currently defines master data, raw coffee beans, roasting batches,
roasted products, stock history, sales, and sales details.

## Connecting the application

The application uses the `Npgsql` package. It reads the PostgreSQL connection
string from the `SIKOPY_DATABASE_CONNECTION_STRING` environment variable so
passwords are not committed to the repository.

### macOS/Linux

Run this from the repository root before starting the app:

```bash
export SIKOPY_DATABASE_CONNECTION_STRING='Host=localhost;Port=5432;Database=sikopy;Username=postgres;Password=your_password'
dotnet run --project SIKOPY.Desktop
```

### Windows PowerShell

```powershell
$env:SIKOPY_DATABASE_CONNECTION_STRING = "Host=localhost;Port=5432;Database=sikopy;Username=postgres;Password=your_password"
dotnet run --project SIKOPY.Desktop
```

The shared connection code is in `SIKOPY/Database/`:

- `PostgresConnectionFactory.OpenAsync()` opens and returns an
  `NpgsqlConnection`.
- `PostgresHealthCheck.CanConnectAsync()` runs `SELECT 1` to verify the
  connection.

Example from a ViewModel or service:

```csharp
await using var connection =
    await PostgresConnectionFactory.OpenAsync(cancellationToken);

await using var command = connection.CreateCommand();
command.CommandText = "SELECT nama FROM jenis_biji_kopi_mentah ORDER BY nama;";

await using var reader = await command.ExecuteReaderAsync(cancellationToken);
while (await reader.ReadAsync(cancellationToken))
{
    var nama = reader.GetString(0);
    // Use nama in the application.
}
```

Do not commit real passwords, connection strings, or environment files.
