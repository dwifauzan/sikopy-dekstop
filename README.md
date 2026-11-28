# SIKOPY

Inventory and sales app for a coffee roastery. Staff track green bean stock, record
roasting batches, price the roast beans and see everything on a dashboard. Customers
buy roast beans from the **Etalase** (shop).

Built with [Avalonia UI](https://avaloniaui.net/) and .NET 10 on a PostgreSQL database.
One shared codebase runs on Desktop (Windows, macOS, Linux), Android, iOS and Browser;
**Desktop is the main target** and the one we test.

---

## Contents

1. [Quick start](#quick-start)
2. [Features by role](#features-by-role)
3. [How stock moves](#how-stock-moves)
4. [Project structure](#project-structure)
5. [Working on the code](#working-on-the-code)
6. [Other platforms](#other-platforms)
7. [Known limits](#known-limits)
8. [More documentation](#more-documentation)

---

## Quick start

Do these once, in order. Details for every database step are in
[database/README.md](database/README.md).

**1. Install**

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0). Check with `dotnet --version` (should print `10.0.x`).
- PostgreSQL 13 or newer, plus pgAdmin or TablePlus if you prefer a GUI.
- An IDE: JetBrains Rider (recommended), Visual Studio 2026, or VS Code + C# Dev Kit.

**2. Create the database**

```bash
createdb -U postgres sikopy
psql -U postgres -d sikopy -f database/schema.sql
```

Already have a database from before? Don't run `schema.sql` again. Run the
[migrations you are missing](database/README.md#updating-an-existing-database) instead.

**3. Tell the app where the database is**

```bash
cp SIKOPY.Desktop/database.local.example.json SIKOPY.Desktop/database.local.json
```

Edit the new file: put your own database name and password in it. This file is in `.gitignore`, so it is never committed.

**4. Run**

```bash
dotnet run --project SIKOPY.Desktop
```

**5. Make yourself admin**

Register an account on the **Daftar** page. New accounts are customers, so promote yours once:

```sql
UPDATE pengguna SET peran = 'Admin' WHERE nama_pengguna = 'your_username';
```

Log in again. From then on, give roles to other people from the **Pengguna** page.

---

## Features by role

There are three roles. A role change takes effect the next time that person logs in.

| Menu | Pelanggan (customer) | Staf | Admin |
|---|:---:|:---:|:---:|
| Dashboard | | ✅ | ✅ |
| Bahan Baku | | ✅ | ✅ |
| Batch Roasting | | ✅ | ✅ |
| Etalase | ✅ | ✅ | ✅ |
| Riwayat | | ✅ | ✅ |
| Pengguna | | | ✅ |

After login, staff land on the **Dashboard** and customers land on the **Etalase**.

### Dashboard
- Totals for green bean and roast bean stock.
- How many green beans are low (below 50 kg) and how many roast beans have no price yet.
- Batches roasted this week (the week starts Monday), the green bean they used, and their average shrinkage.
- Sales today and this month, the list of low-stock green beans, and the 5 latest sales.

### Bahan Baku (raw materials)
- **Green beans:** list, search, and filter by type, process or low stock.
  - Add a bean, optionally with its starting stock.
  - Edit a bean's details with ✏️. Editing never changes stock.
  - Delete a bean permanently. This is refused if a roasting batch used it.
- **Roast Bean:** every roast bean product with its stock and selling price. Set the price per kg with the ✏️ button.
  - A product only appears in the Etalase once it has a price.
- **Riwayat Stok Terbaru:** the last 10 green bean stock movements.

### Batch Roasting
- Record a batch: the green bean used (kg), the roast result (g), the roast level and notes.
- **Edit** a batch. Stock is adjusted by the difference only.
- **Detail batch ↗** shows:
  - amount used, result, and shrinkage
  - remaining roast bean and its price
  - who created the batch, and its stock history
- **Hapus** returns the green bean to stock and removes the roast bean. This is refused once any of that roast bean has been sold or used.

### Etalase (shop)
- Shows roast beans that are active, have stock, and have a price. Search and sort them.
- Add to the cart, change the grams, and save the cart for later.
- **Bayar** places the order. Stock and price are checked again against the database at that moment, so nobody can buy more than is in stock.

### Riwayat (history)
- **Mutasi Stok:** every stock movement for both green bean and roast bean.
  - Search by product, batch code or invoice number.
  - Filter by in or out (masuk/keluar) and by date range.
- **Penjualan:** every sale with invoice, customer, products and total.

### Pengguna (users, admin only)
- **Ubah peran:** change someone's role.
- **Reset password:** generates a random password and shows it once, so you can give it to the user. **Lupa password?** on the login page only tells users to ask an admin; there is no self-service reset.
- **Nonaktifkan / Aktifkan:** a deactivated account can't log in, but its data stays.
- Safety rules:
  - The last active admin can't be demoted or deactivated.
  - An admin can't change their own role or deactivate themselves.

---

## How stock moves

Every stock change happens in **one database transaction**, together with a row in
`riwayat_stok`, so stock and history always match.

| Action | Green bean | Roast bean | Also written |
|---|---|---|---|
| Add green bean with starting stock | + kg | | history MASUK |
| Create batch | − kg used | + g result | batch, product, 2 history rows |
| Edit batch | ± difference | ± difference | history for each difference |
| Delete batch | + kg back | set to 0 | history rows; batch is hidden, not erased |
| Bayar (checkout) | | − g sold | sale + line items, history KELUAR with the invoice number |

Prices are stored **per gram** (`harga_per_gram`). The screens show and accept prices per kg.

---

## Project structure

```
SIKOPY/
├── SIKOPY/                     # Shared app: almost all work happens here
│   ├── Database/               # SQL access (Npgsql). One repository per area
│   │   ├── AuthRepository.cs       login, register
│   │   ├── RawMaterialRepository.cs green beans
│   │   ├── RoastingRepository.cs   batches, roast bean products, prices
│   │   ├── SalesRepository.cs      etalase products, checkout, sales list, full history
│   │   ├── UserRepository.cs       roles, password reset, deactivate
│   │   └── DashboardRepository.cs  dashboard numbers
│   ├── Models/                 # Plain data classes (+ display text helpers)
│   ├── ViewModels/             # One ViewModel per screen (CommunityToolkit.Mvvm)
│   ├── Views/                  # .axaml screens; XxxView pairs with XxxViewModel
│   ├── Helpers/                # PasswordHasher (PBKDF2)
│   ├── Behaviors/              # Input filters (numbers only, etc.)
│   └── Styles/Icons.axaml      # Sidebar and button icons
├── SIKOPY.Desktop/             # Desktop launcher + database.local.json
├── SIKOPY.Android/ .iOS/ .Browser/   # Other launchers (thin)
├── database/                   # schema.sql, migrations/, README
├── docs/                       # ARCHITECTURE.md, CONTRIBUTING.md
└── Directory.Packages.props    # All NuGet versions in one place
```

**Navigation** lives in `ViewModels/MainViewModel.cs`. Its `ShowXxx()` methods
switch pages, and they check the role before doing so. `Views/MainView.axaml` is
the sidebar. Hiding a menu item in the sidebar is not enough: every page must also be
guarded in `MainViewModel`.

---

## Working on the code

| Task | Command |
|---|---|
| Run | `dotnet run --project SIKOPY.Desktop` |
| Build (must have 0 warnings) | `dotnet build SIKOPY.Desktop` |
| Avalonia DevTools | Press **F12** in a Debug build |

**Adding a screen:**
1. Create `XxxViewModel` and `XxxView.axaml`. `ViewLocator` pairs them by name.
2. Add a property and a `ShowXxx()` method in `MainViewModel`, with a role check.
3. Add the sidebar item in `MainView.axaml`.

**Changing the database:**
1. Add a new numbered file, such as `database/migrations/004_xxx.sql`.
2. Make the same change in `database/schema.sql`.
3. Mention it in your PR, because teammates must run it before they pull.

Never edit a migration that has already been merged.

**Git flow:**
1. Work on `dev`, or on a feature branch off `dev`.
2. Open a pull request into `main`.
3. Use [Conventional Commits](https://www.conventionalcommits.org/) messages (`feat:`, `fix:`, `docs:` …).

More rules are in [docs/CONTRIBUTING.md](docs/CONTRIBUTING.md).

### Tech stack

| Area | Technology | Version |
|---|---|---|
| Runtime | .NET | 10.0 |
| UI | Avalonia UI (Fluent theme) | 12.1.4 |
| MVVM | CommunityToolkit.Mvvm | 8.4.2 |
| Database | PostgreSQL + Npgsql | PG 13+ |
| Package versions | NuGet Central Package Management (`Directory.Packages.props`) | — |

---

## Other platforms

You only need this to build mobile or browser targets.

```bash
dotnet workload install android ios     # + wasm-tools for Browser
```

| Platform | Command | Notes |
|---|---|---|
| Browser | `dotnet run --project SIKOPY.Browser` | `https://localhost:7169` |
| Android | `dotnet build SIKOPY.Android -t:Run -f net10.0-android` | Needs **JDK 21** (newer JDKs fail with `XA0030`) and Android SDK platform **36** exactly |
| iOS | `dotnet build SIKOPY.iOS -t:Run -f net10.0-ios` | macOS only, needs Xcode: `sudo xcode-select -s /Applications/Xcode.app/Contents/Developer`, `sudo xcodebuild -license accept`, then `xcodebuild -downloadPlatform iOS` for the simulator |

To install the Android SDK platform 36 that the build needs, run this. Adjust both paths for your machine.

```bash
dotnet build SIKOPY.Android/SIKOPY.Android.csproj -t:InstallAndroidDependencies -f net10.0-android \
  "-p:AndroidSdkDirectory=$HOME/Library/Android/sdk" \
  "-p:JavaSdkDirectory=/Applications/Android Studio.app/Contents/jbr/Contents/Home" \
  -p:AcceptAndroidSDKLicenses=true
```

The database connection file only exists for Desktop. Mobile and Browser builds
cannot reach a local PostgreSQL server as they are now.

---

## Known limits

- **Roles are only enforced by the app.** Every copy of the app connects straight to
  PostgreSQL with full rights. Anyone with the connection string can change the data.
  Fixing that needs a server/API in between, or separate database roles.
- Role changes and deactivations apply at the user's **next login**. Someone who is
  already logged in keeps their access until they log out.
- After a password reset, the user is not forced to choose a new password. There is
  also no screen to change your own password.
- The Etalase cart is saved **per computer**, not per account
  (`<LocalAppData>/SIKOPY/shop_cart.json`).
- In Bahan Baku, the green bean **⋯** menu items **Tambah stok masuk** and
  **Nonaktifkan** are commented out in `RawMaterialsView.axaml`. Their code still works,
  but no button reaches it, so green bean stock can only be set when the bean is
  first added. Editing a bean uses the ✏️ button.
- There are no automated tests yet.

---

## More documentation

| Document | Contents |
|---|---|
| [database/README.md](database/README.md) | Database setup, migrations, connection, troubleshooting |
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | How the app is structured, MVVM basics, startup flow, adding screens |
| [docs/CONTRIBUTING.md](docs/CONTRIBUTING.md) | Git workflow, commit messages, coding conventions |
