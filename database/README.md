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

## Raw material feature

The application provides a **Bahan Baku** (raw materials) screen for production
employees. It can:

- list active green beans with origin, type, process, grade, altitude, stock,
  price per kg and harvest month (10 per page);
- search by name or origin, and filter by type, process or low stock
  (below 50 kg, shown in red);
- add a green bean (with an optional initial stock) and edit its details;
- add incoming stock in kilograms from the row's **⋯** menu;
- deactivate a green bean (`is_aktif = FALSE`), which keeps its history;
- permanently delete a green bean (**⋯ → Hapus permanen**). Its `riwayat_stok`
  rows are deleted in the same transaction. Deleting is refused while any
  `batch_roasting` row uses the bean; deactivate it instead;
- show the 10 most recent stock movements.

Every stock increase, including the initial stock, is written to `riwayat_stok`
in the same PostgreSQL transaction as the stock update. Editing a green bean
never changes its stock directly.

Number fields (**Ketinggian**, **Harga per kg**, **Stok awal**, **Jumlah
masuk**) only accept digits, plus `.` and `,` for price and stock; letters are
blocked when typed and removed when pasted. **Tanggal panen** accepts digits
and `/` only and is entered as `dd/mm/yyyy` (or picked from the calendar).

In the add/edit form, **Jenis** and **Proses** can be picked from the list or
typed. A name that does not exist yet (compared ignoring case) is inserted into
`jenis_biji_kopi_mentah` or `metode_proses` in the same transaction as the
green bean, so an empty database needs no seed data.

Before running the application, apply the current schema. For an existing
database, run this migration once:

```text
database/migrations/002_raw_material_constraints.sql
```

For a new database, `database/schema.sql` already includes these constraints.

## Connecting the application

The application uses the `Npgsql` package. It looks for the PostgreSQL
connection string in this order:

1. the `SIKOPY_DATABASE_CONNECTION_STRING` environment variable;
2. the file `SIKOPY.Desktop/database.local.json`.

The file is the easiest option: set it up once and it works with
`dotnet run`, Rider and Visual Studio. It is listed in `.gitignore`, so your
password is never committed.

### Recommended: local settings file

Copy the example file and fill in your own values:

```bash
cp SIKOPY.Desktop/database.local.example.json SIKOPY.Desktop/database.local.json
```

```json
{
  "ConnectionString": "Host=localhost;Port=5432;Database=Sikopy;Username=postgres;Password=your_password"
}
```

- `Database` must match your database name exactly; PostgreSQL treats
  `Sikopy` and `sikopy` as different databases.
- Leave out `Password=...` if your local server does not ask for one.

Then run the app normally:

```bash
dotnet run --project SIKOPY.Desktop
```

The file is copied next to the app when it builds. After editing it, run or
build again.

### Alternative: environment variable

The environment variable takes priority over the file. It only applies to the
terminal (or IDE run configuration) where it is set.

macOS/Linux:

```bash
export SIKOPY_DATABASE_CONNECTION_STRING='Host=localhost;Port=5432;Database=Sikopy;Username=postgres;Password=your_password'
dotnet run --project SIKOPY.Desktop
```

Windows PowerShell:

```powershell
$env:SIKOPY_DATABASE_CONNECTION_STRING = "Host=localhost;Port=5432;Database=Sikopy;Username=postgres;Password=your_password"
dotnet run --project SIKOPY.Desktop
```

In Rider, add the variable under **Run → Edit Configurations… →
SIKOPY.Desktop → Environment variables**.

If neither is set, the app shows "Koneksi database belum diatur".

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
