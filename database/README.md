# Database

SIKOPY stores everything in PostgreSQL (version 13 or newer). This page covers:
- setting up a new database
- updating one you already have
- connecting the app to it
- fixing common problems

The examples use the database name **`sikopy`**. PostgreSQL treats `Sikopy` and
`sikopy` as **different databases**, so use the same name everywhere. That includes
your `database.local.json`.

---

## Contents

1. [New database](#new-database)
2. [Updating an existing database](#updating-an-existing-database)
3. [Connecting the app](#connecting-the-app)
4. [First admin account](#first-admin-account)
5. [Tables](#tables)
6. [Changing the schema](#changing-the-schema)
7. [Troubleshooting](#troubleshooting)

---

## New database

`database/schema.sql` always holds the **complete, current** schema, including every
migration. On a new, empty database you only run this one file.

### Command line

```bash
createdb -U postgres sikopy
psql -U postgres -d sikopy -f database/schema.sql
```

### pgAdmin

1. Right-click **Databases → Create → Database…**, name it `sikopy`, then **Save**.
2. Right-click `sikopy` and choose **Query Tool**.
3. **File → Open File**, then choose `database/schema.sql`.
4. Press **F5** to run it.
5. Right-click `sikopy` and choose **Refresh**. You should see 9 tables under
   **Schemas → public → Tables**.

### TablePlus

Connect, select the `sikopy` database, open `database/schema.sql` in a new query, and run the whole file.

---

## Updating an existing database

If your database was created earlier, **don't run `schema.sql` again**, because the
tables already exist. Run only the migrations you don't have yet, in number order:

| Migration | Adds | You need it if… |
|---|---|---|
| `001_initial_schema.sql` | All tables | Never; `schema.sql` already covers it |
| `002_raw_material_constraints.sql` | Stock/price can't be negative; history rows must be valid | Bahan Baku shows constraint errors |
| `003_user_active_flag.sql` | `pengguna.is_aktif`, used to deactivate accounts | Login fails with "Struktur database belum sesuai" |

```bash
psql -U postgres -d sikopy -f database/migrations/003_user_active_flag.sql
```

How to check what your database already has:

```sql
-- 002 is applied if this returns 4 rows
SELECT conname FROM pg_constraint WHERE conname LIKE 'ck_%';

-- 003 is applied if this returns 1 row
SELECT column_name FROM information_schema.columns
WHERE table_name = 'pengguna' AND column_name = 'is_aktif';
```

> When you pull and see a new file in `database/migrations/`, run it before you
> start the app.

---

## Connecting the app

The app looks for the connection string in this order:

1. the environment variable `SIKOPY_DATABASE_CONNECTION_STRING`;
2. the file `SIKOPY.Desktop/database.local.json`.

### Recommended: the local file

```bash
cp SIKOPY.Desktop/database.local.example.json SIKOPY.Desktop/database.local.json
```

```json
{
  "ConnectionString": "Host=localhost;Port=5432;Database=sikopy;Username=postgres;Password=your_password"
}
```

- Leave out `Password=...` if your local server doesn't ask for one.
- The file is in `.gitignore`, so your password is never committed.
- It is copied next to the app at build time. After you edit it, run or build again.

### Alternative: environment variable

This overrides the file, but only in the terminal or IDE run configuration where you set it.

```bash
# macOS / Linux
export SIKOPY_DATABASE_CONNECTION_STRING='Host=localhost;Port=5432;Database=sikopy;Username=postgres;Password=your_password'
```

```powershell
# Windows PowerShell
$env:SIKOPY_DATABASE_CONNECTION_STRING = "Host=localhost;Port=5432;Database=sikopy;Username=postgres;Password=your_password"
```

In Rider, add it under **Run → Edit Configurations… → SIKOPY.Desktop → Environment variables**.

---

## First admin account

New accounts made on the **Daftar** page get the role `Pelanggan` (customer).
To get the first admin, register in the app, then run:

```sql
UPDATE pengguna SET peran = 'Admin' WHERE nama_pengguna = 'your_username';
```

After that, manage everyone else from the **Pengguna** page in the app:
change roles, reset passwords, deactivate accounts. The roles are `Pelanggan`,
`Staf` and `Admin`.

Locked out, for example because the only admin forgot their password? Make another
account an admin with the SQL above, then reset the password from **Pengguna**.

---

## Tables

| Table | Holds |
|---|---|
| `pengguna` | Accounts: username, password hash, name, role (`peran`), `is_aktif` |
| `jenis_biji_kopi_mentah` | Green bean types (filled automatically from the form) |
| `metode_proses` | Processing methods (filled automatically from the form) |
| `biji_kopi_mentah` | Green beans: origin, stock in **kg**, price per kg |
| `batch_roasting` | Roasting batches: green bean used (**g**), result (**g**), level, date |
| `biji_kopi_roasted` | Roast bean products, one per batch: stock in **g**, `harga_per_gram` |
| `riwayat_stok` | Every stock movement (MASUK/KELUAR). `referensi` = batch code or invoice number |
| `penjualan` | Sales: invoice number, buyer, total |
| `detail_penjualan` | Sale lines: product, grams, price per gram, subtotal |

Nothing that has history is hard-deleted. Batches, products and accounts are hidden
with `is_aktif = FALSE` instead. The one exception is **Hapus permanen** on a green
bean, which is only allowed when no batch has used it.

---

## Changing the schema

1. Add a new numbered file: `database/migrations/004_short_name.sql`. Wrap it in
   `BEGIN; … COMMIT;`, and use `IF NOT EXISTS` where you can, so it is safe to run twice.
2. Make the same change in `database/schema.sql`, so new databases get it too.
3. Add a row to the table in [Updating an existing database](#updating-an-existing-database).
4. Say in your PR that teammates must run the migration.

Never edit a migration that is already merged. Write a new one instead.

---

## Troubleshooting

| Message | Fix |
|---|---|
| "Koneksi database belum diatur" | Create `SIKOPY.Desktop/database.local.json` ([Connecting the app](#connecting-the-app)) |
| "Database tidak ditemukan" | The name in your connection string doesn't match. Check `sikopy` vs `Sikopy` |
| "Login ke database gagal" | Wrong username or password in the connection string |
| "Struktur database belum sesuai" | A migration is missing. See [Updating an existing database](#updating-an-existing-database) |
| "Tidak dapat terhubung ke database" | PostgreSQL isn't running, or the host/port is wrong |
| `current transaction is aborted` while importing | Only the **first** error matters. Usually the tables already exist. On an empty dev database, drop it, recreate it, and import again |

To reset a local dev database, drop it in pgAdmin (**Delete/Drop**), create it again,
and run `schema.sql`. **This deletes all its data.**
