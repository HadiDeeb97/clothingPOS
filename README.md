# Clothing Store POS

A Windows desktop point-of-sale system for clothing and fashion retail, built with **C# / .NET 10 (LTS) / WPF**, using MVVM with
**Entity Framework Core + SQLite** for local storage.

## Features

| Area | What it does |
| --- | --- |
| **Register** | Scan barcodes or search by name/SKU/colour/size; size & colour variants; quantity +/-; line and cart discounts (% or amount); cashier discount limit with manager override; hold & resume sales (fitting room); keyboard shortcuts (F2 search, F4 customer, F6 qty, F7/F8 discounts, F9/F10 hold/resume, F12 pay) |
| **Payments** | Split tender across cash, card, mobile wallet, store credit and loyalty points; quick-cash buttons; change calculation; printable receipts (auto-fits 58/80 mm thermal or A4) |
| **Returns & exchanges** | Look up a receipt; partial returns; restock or write off; refund to cash, card or store credit; return window with manager override; refunds reconcile to the cent |
| **Sales history** | Search by date/receipt/customer/product; reprint; void (manager only, restocks and reverses balances); CSV export |
| **Products** | Style + size × colour matrix with presets (XS–XXL, waist, shoe, kids…); auto SKU and in-store EAN-13 barcodes; per-variant price/cost overrides; brand, season, material, department |
| **Inventory** | Stock levels and valuation; low-stock highlighting; adjustments (damaged, lost, received…); physical stock counts; full movement ledger; Code 128 price labels (sheet or label printer) |
| **Purchasing** | Suppliers; purchase orders; "add supplier's low-stock items"; partial and full receiving into stock with cost updates |
| **Customers** | Profiles, purchase history, lifetime spend; loyalty points (earn and redeem); store credit |
| **Cash drawer** | Open shift with float; pay-ins/pay-outs; X report; count and close with over/short; Z report; shift history |
| **Reports** | Sales, net revenue, gross profit and margin, average basket; breakdowns by product, category, size, payment method, cashier and day; stock valuation; CSV export and printable summary |
| **Admin** | Users with roles (Cashier / Manager / Admin), PBKDF2-hashed passwords, forced password change; store, tax (inclusive or exclusive), receipt and loyalty settings; online database backup |

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

Requirements: Windows 10/11 and the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```powershell
cd ClothingStorePOS
dotnet run --project src/ClothingStore.Desktop
```

Or open `ClothingStorePOS.sln` in Visual Studio 2026 (or Rider / VS Code with C# Dev Kit) and set **ClothingStore.Desktop** as the startup project.

On first launch the database is created at `%LocalAppData%\ClothingStorePOS\pos.db`. A sample catalogue is loaded too,
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
  "Pos": {
    "DatabasePath": "",      // empty = %LocalAppData%\ClothingStorePOS\pos.db; may use %ENV_VARS%
    "SeedDemoData": true
  }
}
```

Settings can also be overridden with environment variables, e.g. `Pos__DatabasePath=D:\POS\store.db`.

### Publishing a self-contained build

```powershell
dotnet publish src/ClothingStore.Desktop -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```

Copy the `publish` folder to the till PC. It does not need .NET installed.

## Daily workflow

1. **Cash Drawer**: open a shift with your starting float.
2. **Register**: scan items, add the customer (F4), then **Pay** (F12). Print or skip the receipt.
3. **Returns**: scan the receipt number. For an exchange, refund to store credit, then sell the new item and pay with store credit.
4. **Cash Drawer**: at closing time, count the drawer, close the shift and print the Z report.
5. **Settings**: run **Back up now** regularly, to a USB drive or a cloud-synced folder.

## Hardware

* **Barcode scanners**: any USB/Bluetooth scanner in keyboard-wedge mode that sends Enter after each scan.
* **Receipt printers**: any Windows-installed printer. Set *Receipt width* to 42 for 80 mm or 32 for 58 mm paper.
* **Labels**: 3-across A4/Letter label sheets (labels about 33 mm tall), or one label per page on a label printer. Labels use Code 128.
* Cash drawers that open from the receipt printer work through the printer driver's "open drawer" setting.

## Development

```bash
dotnet build ClothingStorePOS.sln
dotnet test                                   # runs on Windows, Linux or macOS
dotnet tool restore
dotnet ef migrations add <Name> --project src/ClothingStore.Data --startup-project src/ClothingStore.Data
```

The solution sets `EnableWindowsTargeting=true`, so CI agents on Linux can compile the WPF project and run the tests.
Running the WPF app still needs Windows.

### Notes and limits

* SQLite suits a single till, or a single PC that several staff share. For several tills on a network, switch the EF Core
  provider in `ServiceCollectionExtensions.AddPosData` to SQL Server or PostgreSQL. Optimistic concurrency tokens on stock
  and customer balances are already in place. You will need to regenerate the migrations.
* Card payments are recorded, not processed: take the payment on your card terminal and enter the auth code as the reference.
