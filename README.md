# 4KV Hardware — Point of Sale & Inventory

WPF (.NET 10) + SQL Server Express capstone for a hardware store POS and inventory system.

**GitHub (private):** https://github.com/ceto-31/hardware-pos

## Prerequisites

Install these on **every** laptop that will run the app:

1. **Visual Studio Community 2026** (or newer)
   - Workload: **.NET desktop development**
2. **SQL Server Express 2022**
   - Default instance name: `.\SQLEXPRESS`
3. **SQL Server Management Studio (SSMS)** — recommended for running scripts and checking data
4. **Git** — needed to clone from GitHub  
   - Download: https://git-scm.com/download/win  
   - Or install **GitHub Desktop**: https://desktop.github.com/

## Transfer to another laptop (via GitHub)

Use this when moving the project to a new PC. You do **not** need to copy the folder by USB — clone from the private repo instead.

### On the old laptop (push latest work)

1. Commit and push any unfinished changes so GitHub is up to date.
2. Confirm the remote is:
   - `https://github.com/ceto-31/hardware-pos.git`

### On the new laptop

#### 1. Install prerequisites

Install Visual Studio (desktop workload), SQL Server Express, SSMS, and Git as listed above.

#### 2. Sign in to GitHub

Because the repo is **private**, sign in before cloning:

- **Option A — GitHub Desktop:** File → Clone repository → pick `ceto-31/hardware-pos`
- **Option B — Git CLI:** authenticate once (`gh auth login` or Git Credential Manager when prompted)

#### 3. Clone the repository

```powershell
cd $env:USERPROFILE\Documents
git clone https://github.com/ceto-31/hardware-pos.git HardwarePOS
cd HardwarePOS
```

#### 4. Create the database

In SSMS, connect to `.\SQLEXPRESS` (Windows Authentication) and run scripts **in order**:

1. `Database/01_CreateDatabase.sql`
2. `Database/02_CreateTables.sql`
3. `Database/03_SeedData.sql`
4. `Database/04_BackfillOpeningLedger.sql` *(optional on a fresh seed; safe to re-run)*
5. `Database/05_UpgradeSchema.sql` *(required for Units, ProductCode, security questions, ActivityLog, supplier archive, product photos)*

Or from PowerShell (from the project root):

```powershell
sqlcmd -S ".\SQLEXPRESS" -E -i "Database\01_CreateDatabase.sql"
sqlcmd -S ".\SQLEXPRESS" -E -i "Database\02_CreateTables.sql"
sqlcmd -S ".\SQLEXPRESS" -E -i "Database\03_SeedData.sql"
sqlcmd -S ".\SQLEXPRESS" -E -i "Database\04_BackfillOpeningLedger.sql"
sqlcmd -S ".\SQLEXPRESS" -E -i "Database\05_UpgradeSchema.sql"
```

> Fresh install note: `03_SeedData.sql` already inserts opening ledger rows for seeded stock. Script `04` is mainly for databases that existed before that change. Always run `05` after upgrading an existing database.

#### 5. Check the connection string

Open `src/HardwarePOS/appsettings.json`. Default:

```json
{
  "ConnectionStrings": {
    "HardwarePOS": "Server=.\\SQLEXPRESS;Database=HardwarePOS;Trusted_Connection=True;TrustServerCertificate=True;"
  }
}
```

Only change `Server=...` if your SQL instance name is different (e.g. `localhost`, `.\SQLEXPRESS01`, or `(localdb)\\MSSQLLocalDB`).

#### 6. Run the app

1. Open `HardwarePOS.slnx` in Visual Studio
2. Press **F5**

### Demo logins

| Username | Password     | Role    |
|----------|--------------|---------|
| admin    | Password123  | Admin   |
| cashier  | Cashier@123  | Cashier |

**Forgot Password** security answers (demo users): favorite color `blue`, favorite number `7`, hobby `reading`. Wrong answers show which question failed.

### Pull later updates from GitHub

On the new laptop, after the first clone:

```powershell
cd $env:USERPROFILE\Documents\HardwarePOS
git pull
```

Then rebuild/run in Visual Studio. Re-run SQL scripts only if the update adds or changes files under `Database/`.

## Transfer to another laptop (manual ZIP / USB)

Use this when you cannot clone from GitHub (no internet, no GitHub access, or you just want a USB copy).

### On the old laptop (create the ZIP)

1. Close Visual Studio if the project is open.
2. Open File Explorer and go to the project folder (example: `Documents\HardwarePOS`).
3. **Optional but recommended — shrink the ZIP:** delete or skip these folders if they exist (they are rebuildable):
   - `src\HardwarePOS\bin`
   - `src\HardwarePOS\obj`
   - `.vs`
4. Right-click the `HardwarePOS` folder → **Compress to ZIP file** (or **Send to → Compressed (zipped) folder**).
5. Copy `HardwarePOS.zip` to a USB drive / cloud folder / external disk.

> Tip: Keep `Database\*.sql` and `src\` inside the ZIP. Those are required.

### On the new laptop (extract and run)

#### 1. Install prerequisites

Install Visual Studio (desktop workload), SQL Server Express, and SSMS. Git is optional for this method.

#### 2. Copy and extract

1. Copy `HardwarePOS.zip` from the USB/cloud to the new PC (example: `Documents\`).
2. Right-click → **Extract All…**
3. Open the extracted `HardwarePOS` folder and confirm you see:
   - `HardwarePOS.slnx`
   - `README.md`
   - `Database\`
   - `src\`

#### 3. Create the database

Same as GitHub transfer — run scripts in order:

1. `Database/01_CreateDatabase.sql`
2. `Database/02_CreateTables.sql`
3. `Database/03_SeedData.sql`
4. `Database/04_BackfillOpeningLedger.sql` *(optional on a fresh seed)*

Or from PowerShell (from the extracted project root):

```powershell
sqlcmd -S ".\SQLEXPRESS" -E -i "Database\01_CreateDatabase.sql"
sqlcmd -S ".\SQLEXPRESS" -E -i "Database\02_CreateTables.sql"
sqlcmd -S ".\SQLEXPRESS" -E -i "Database\03_SeedData.sql"
sqlcmd -S ".\SQLEXPRESS" -E -i "Database\04_BackfillOpeningLedger.sql"
```

#### 4. Check the connection string

Open `src/HardwarePOS/appsettings.json` and adjust `Server=` only if your SQL instance name is not `.\SQLEXPRESS`.

#### 5. Run the app

1. Open `HardwarePOS.slnx` in Visual Studio
2. Wait for NuGet restore if prompted
3. Press **F5**

Use the same demo logins as above (`admin` / `Password123`, `cashier` / `Cashier@123`).

### ZIP vs GitHub — which to use?

| Method | Best when… |
|--------|------------|
| **GitHub clone** | You want easy updates later (`git pull`) and a clean copy without `bin`/`obj` |
| **Manual ZIP** | No GitHub access, offline transfer, or one-time handoff via USB |

### Moving real sales data (optional)

A ZIP of the project folder does **not** include your SQL database. To bring old products/sales to the new PC:

1. On the old PC (SSMS): right-click `HardwarePOS` → **Tasks → Back Up…** → create a `.bak` file
2. Copy the `.bak` with your ZIP
3. On the new PC (SSMS): right-click **Databases → Restore Database…** and select that `.bak`

If you skip backup/restore, just run the seed scripts for demo data.

### Moving product photos

Product photos are **not** in GitHub or the SQL `.bak`. They live in:

`%LocalAppData%\4KVHardware\ProductImages`

Example: `C:\Users\<you>\AppData\Local\4KVHardware\ProductImages`

Copy that folder to the same path on the new PC so thumbnails still appear.

## Database setup (reference)

Same scripts as in the transfer steps above. Always run in order: `01` → `02` → `03` → (`04` if needed).

## View / check the database (SSMS)

1. Open **SQL Server Management Studio**
2. Connect to server: `.\SQLEXPRESS` (Windows Authentication)
3. In **Object Explorer**, expand: **Databases → HardwarePOS → Tables**
4. Right-click a table (e.g. `dbo.Products`, `dbo.Sales`) → **Select Top 1000 Rows**

Useful tables: `Users`, `Products`, `Suppliers`, `Sales`, `SaleItems`, `StockIns`, `StockOuts`, `InventoryLedger`

## Modules

- Login, Dashboard, Products (with photos), Suppliers, Inventory, POS (12% VAT, receipt print)
- Admin-only: Products, Suppliers, Inventory
- Cashier: Dashboard + POS

## Troubleshooting

| Problem | What to try |
|---------|-------------|
| Clone fails / 404 | Repo is private — sign in to GitHub (same account that owns/accesses the repo) |
| Login fails / DB errors | Confirm SQL Express is running; re-check `appsettings.json`; confirm database `HardwarePOS` exists |
| Wrong SQL instance | In SSMS, note the exact server name you connect with and put it in `Server=` |
| Build errors after clone/extract | Open Visual Studio → restore NuGet packages → rebuild |
| ZIP extract looks incomplete | Confirm `HardwarePOS.slnx`, `Database\`, and `src\` are present |
| Old data not on new PC | ZIP/clone gives **code only**. Sales/products live in the local SQL DB. Back up/restore a `.bak` in SSMS, or re-seed with the SQL scripts (demo data only) |
| Product photos missing | Copy `%LocalAppData%\4KVHardware\ProductImages` from the old PC. Re-run `Database/05_UpgradeSchema.sql` if Save photo fails |
