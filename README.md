# Clothing Store POS

A Windows desktop point-of-sale system for clothing and fashion retail, built with **C# / .NET 10 (LTS) / WPF**, using MVVM with
**Entity Framework Core + SQL Server**, so several tills can share one database.

## Features

| Area | What it does |
| --- | --- |
| **Register** | Scan barcodes or search by name/SKU/colour/size; size & colour variants; quantity +/-; line and cart discounts (% or amount); cashier discount limit with manager override; hold & resume sales (fitting room); keyboard shortcuts (F2 search, F4 customer, F6 qty, F7/F8 discounts, F9/F10 hold/resume, F12 pay) |
| **Payments** | Split tender across cash (dollars and Lebanese pounds), card, mobile wallet, store credit and loyalty points; quick-cash buttons for both currencies; change in dollars, pounds or both; printable receipts with the LBP total (auto-fits 58/80 mm thermal or A4) |
| **Lebanese pounds** | Prices stay in dollars; LBP is a second cash currency at the store's rate. Managers and admins change the rate from the top bar, every change is logged, and other tills pick it up within a minute. A sale or refund at an old rate is refused. LBP is rounded to a configurable step (up when collecting, down when paying out) |
| **Returns & exchanges** | Look up a receipt; partial returns; restock or write off; refunds go back the way the sale was paid (split payments in proportion), or to store credit; store credit and loyalty points always come back as credit and points, never cash; card refunds in cash need a manager; return window with manager override; refunds reconcile to the cent per tender |
| **Sales history** | Search by date/receipt/customer/product; reprint; void (manager only, restocks and reverses balances); CSV export |
| **Products** | Style + size × colour matrix with presets (XS–XXL, waist, shoe, kids…); auto SKU and in-store EAN-13 barcodes; per-variant price/cost overrides; brand, season, material, department |
| **Inventory** | Stock levels and valuation; low-stock highlighting; adjustments (damaged, lost, received…); physical stock counts; full movement ledger; Code 128 price labels (sheet or label printer) |
| **Purchasing** | Suppliers; purchase orders; "add supplier's low-stock items"; partial and full receiving into stock with cost updates |
| **Customers** | Profiles, purchase history, lifetime spend; loyalty points (earn and redeem); store credit |
| **Cash drawer** | Open shift with float; pay-ins/pay-outs; X report; count and close with over/short; Z report; shift history. Dollars and pounds are counted separately |
| **Reports** | Sales, net revenue, gross profit and margin, average basket; breakdowns by product, category, size, payment method, cashier and day; stock valuation; CSV export and printable summary |
| **Admin** | Users with roles (Cashier / Manager / Admin), PBKDF2-hashed passwords, forced password change; store, tax (inclusive or exclusive), receipt and loyalty settings; automatic, verified SQL Server backups (scheduled and at shift close) with a backup log |

## Solution layout

```
ClothingStorePOS.sln
├── src/ClothingStore.Core      Domain entities, pricing engine, barcodes, receipt formatting, security (no dependencies)
├── src/ClothingStore.Data      EF Core DbContext, migrations, business services, demo seed data
├── src/ClothingStore.Desktop   WPF app (MVVM with CommunityToolkit.Mvvm, Microsoft.Extensions.Hosting for DI)
└── tests/ClothingStore.Tests   xUnit tests for pricing, checkout, returns, inventory, purchasing, shifts and reports
```

All business rules (stock checks, discount limits, payment validation, refunds, shift cash) live in the
`ClothingStore.Data` services and are covered by tests. The UI only orchestrates them.

## Getting started

Requirements: Windows 10/11, the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) and SQL Server 2019 or
later. The free [SQL Server Express](https://www.microsoft.com/sql-server/sql-server-downloads) edition is enough for a shop.
The default connection string expects an Express instance named `SQLEXPRESS` on the same PC, signed in with Windows
authentication.

```powershell
cd ClothingStorePOS
dotnet run --project src/ClothingStore.Desktop
```

Or open `ClothingStorePOS.sln` in Visual Studio 2026 (or Rider / VS Code with C# Dev Kit) and set **ClothingStore.Desktop** as the startup project.

On first launch the app creates the `ClothingStorePOS` database and its tables. The login it uses needs permission to create
a database (or create the empty database yourself and make the login its `db_owner`). A sample catalogue is loaded too,
with 15 styles, 125 size/colour variants, customers and suppliers.

| User | Password | Role |
| --- | --- | --- |
| `admin` | `admin123` | Admin (you must change this password on first sign-in) |
| `manager` | `manager123` | Manager (demo data only) |
| `cashier` | `cashier123` | Cashier (demo data only) |

To start with an empty catalogue, set `"SeedDemoData": false` in `appsettings.json` **before** the first run. Then delete or
deactivate the demo users once real accounts exist.

### Configuration (`src/ClothingStore.Desktop/appsettings.json`)

```jsonc
{
  "ConnectionStrings": {
    "Pos": "Server=.\\SQLEXPRESS;Database=ClothingStorePOS;Trusted_Connection=True;TrustServerCertificate=True"
  },
  "Pos": {
    "SeedDemoData": true
  }
}
```

Settings can also be overridden with environment variables, e.g. `ConnectionStrings__Pos=...`.

### Several tills

1. Install SQL Server Express on one PC (the "server", which can also be a till). In SQL Server Configuration Manager, enable
   TCP/IP for the instance, start the SQL Server Browser service and allow SQL Server through Windows Firewall.
2. In a shop without a Windows domain, Windows authentication doesn't work between PCs, so enable mixed-mode authentication
   and create a SQL login for the app (for example `pos`) that owns the `ClothingStorePOS` database.
3. On every till, set `ConnectionStrings:Pos` in `appsettings.json` to point at that PC:
   `Server=SHOP-PC\\SQLEXPRESS;Database=ClothingStorePOS;User Id=pos;Password=...;TrustServerCertificate=True`.

Stock levels and customer balances use optimistic concurrency, so two tills selling the last unit at the same moment can't
both succeed. The second gets a "please try again" message.

Usernames, SKUs, and category and supplier names are always compared without regard to case. Product and customer searches
follow the database's collation, which is case-insensitive in a default SQL Server install.

### Publishing a self-contained build

```powershell
dotnet publish src/ClothingStore.Desktop -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```

Copy the `publish` folder to the till PC. It does not need .NET installed.

## Daily workflow

1. **Cash Drawer**: open a shift with your starting float (dollars and pounds). Check the rate in the top bar; a manager
   clicks it to change it.
2. **Register**: scan items, add the customer (F4), then **Pay** (F12). Print or skip the receipt.
3. **Returns**: scan the receipt number. The refund goes back to the original payment method. For an exchange, refund to
   store credit, then sell the new item and pay with store credit.
4. **Cash Drawer**: at closing time, count the drawer, close the shift and print the Z report.
5. **Backups** happen automatically: after every shift close, and whenever the last backup is older than the interval set in
   **Settings** (24 hours by default). See below.

### Cash in Lebanese pounds

* **Settings → Lebanese pounds** switches LBP on or off and sets the rounding step (1,000 by default). New and existing
  stores start with LBP on at 89,500.
* On the payment screen, type what the customer handed over in the **dollars** box, the **pounds** box, or both. Choose
  how to give change: **Dollars + LBP** (whole dollars, the rest in pounds), **USD** or **LBP**.
* The amount to pay in pounds is rounded **up** to the step; change and refunds in pounds are rounded **down**, so the
  drawer is never short because of rounding.
* Refunds pay pounds back in pounds at **today's** rate (or the cashier picks dollars or pounds for the cash part).
* The X/Z reports and the close-shift screen show what should be in the drawer in each currency.

### Backups

* SQL Server writes each backup on its own PC, into the folder set in **Settings** (empty = the server's default backup
  folder). The SQL Server service account must be able to write there. Point it at a cloud-synced or network folder so a
  copy survives if that PC fails.
* Automatic backups reuse one file per weekday (`ClothingStorePOS_auto_Mon.bak` ... `_Sun.bak`), so the last seven days are
  kept. **Back up now** writes a separate timestamped file that is never overwritten.
* Every backup is a full, copy-only backup with checksums, verified straight after it is written. Every till checks whether
  a backup is due, but a lock on the server makes sure only one backs up at a time. After a failure, the next automatic
  attempt waits an hour.
* **Settings** lists recent backups and any errors. If the backup after closing a shift fails, the cashier is told.
* To restore, close the app on every till and use **Restore Database** in SQL Server Management Studio.

## Hardware

* **Barcode scanners**: any USB/Bluetooth scanner in keyboard-wedge mode that sends Enter after each scan.
* **Receipt printers**: any Windows-installed printer. Set *Receipt width* to 42 for 80 mm or 32 for 58 mm paper.
* **Labels**: 3-across A4/Letter label sheets (labels about 33 mm tall), or one label per page on a label printer. Labels use Code 128.
* Cash drawers that open from the receipt printer work through the printer driver's "open drawer" setting.

## Development

```bash
dotnet build ClothingStorePOS.sln
dotnet test                                   # needs a SQL Server, see below
dotnet tool restore
dotnet ef migrations add <Name> --project src/ClothingStore.Data --startup-project src/ClothingStore.Data
```

Each test creates and drops its own database on a real SQL Server. On Windows the tests use LocalDB
(`(localdb)\MSSQLLocalDB`, installed with Visual Studio). Elsewhere, set `POS_TEST_SQLSERVER` to a connection string
without a database name, for example with SQL Server in Docker:

```bash
docker run -d -p 1433:1433 -e ACCEPT_EULA=Y -e 'MSSQL_SA_PASSWORD=Your_password1' mcr.microsoft.com/mssql/server:2022-latest
export POS_TEST_SQLSERVER='Server=localhost;User Id=sa;Password=Your_password1;TrustServerCertificate=True'
dotnet test
```

The solution sets `EnableWindowsTargeting=true`, so CI agents on Linux can compile the WPF project and run the tests.
Running the WPF app still needs Windows.

### Notes and limits

* Earlier builds stored data in a SQLite file (`pos.db`). That data is not imported into SQL Server automatically.
* Card payments are recorded, not processed: take the payment on your card terminal and enter the auth code as the reference.
