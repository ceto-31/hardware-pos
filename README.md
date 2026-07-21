# HARDWARE — Point of Sale & Inventory

WPF (.NET 10) + SQL Server Express capstone for a hardware store POS and inventory system.

## Prerequisites

- Visual Studio Community 2026 with **.NET desktop development**
- SQL Server Express 2022 (`.\SQLEXPRESS`)
- SQL Server Management Studio (SSMS) recommended

## Database setup

In SSMS, connect to `.\SQLEXPRESS` and run scripts in order:

1. `Database/01_CreateDatabase.sql`
2. `Database/02_CreateTables.sql`
3. `Database/03_SeedData.sql`

Or from PowerShell:

```powershell
sqlcmd -S ".\SQLEXPRESS" -E -i "Database\01_CreateDatabase.sql"
sqlcmd -S ".\SQLEXPRESS" -E -i "Database\02_CreateTables.sql"
sqlcmd -S ".\SQLEXPRESS" -E -i "Database\03_SeedData.sql"
```

## Run the app

1. Open `HardwarePOS.slnx` in Visual Studio
2. Confirm connection string in `src/HardwarePOS/appsettings.json`
3. Press F5

### Demo logins

| Username | Password     | Role    |
|----------|--------------|---------|
| admin    | Password123  | Admin   |
| cashier  | Cashier@123  | Cashier |

## View / check the database (SSMS)

1. Open **SQL Server Management Studio**
2. Connect to server: `.\SQLEXPRESS` (Windows Authentication)
3. In **Object Explorer**, expand: **Databases → HardwarePOS → Tables**
4. Right-click a table (e.g. `dbo.Products`, `dbo.Sales`) → **Select Top 1000 Rows**

Useful tables: `Users`, `Products`, `Suppliers`, `Sales`, `SaleItems`, `StockIns`, `StockOuts`, `InventoryLedger`

## Modules

- Login, Dashboard, Products, Suppliers, Inventory, POS (12% VAT, receipt print)

## Transfer to another laptop

1. Install Visual Studio, SQL Server Express, and SSMS
2. Copy the project folder
3. Re-run the `Database/*.sql` scripts (or restore a `.bak`)
4. Update `appsettings.json` if the SQL instance name differs
5. Open the solution and run
