# Installing Clothing Store POS for a customer

## 1. Build a release (on your PC)

**Once:** open **License Maker**, click **Copy public key for the app** and paste it into
`src/ClothingStore.Desktop/licensing.json`, with your name, phone and email (customers see them on the license screens):

```json
{
  "publicKey": "-----BEGIN PUBLIC KEY-----\n...\n-----END PUBLIC KEY-----",
  "vendorName": "Your name",
  "vendorPhone": "+961 ...",
  "vendorEmail": "you@example.com"
}
```

Keep notes out of this file (put commands in this guide instead).

**Every release:** pull `main`, then in a terminal in the project folder (Visual Studio: **View → Terminal**):

```powershell
dotnet publish src/ClothingStore.Desktop -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```

The `publish` folder holds `ClothingStore.Desktop.exe` and `appsettings.json`. The customer's PC doesn't need .NET.

For a **new customer (empty database)** open `publish\appsettings.json` and set `"SeedDemoData": false`.

## 2. Install on the customer's PC

1. Install **SQL Server 2022 Express** (free; 2016 or newer is required, 2008/2012/2014 don't work). Choose **Basic**,
   keep the instance name `SQLEXPRESS`.
2. Copy the `publish` folder to e.g. `C:\ClothingStorePOS\` and make a desktop shortcut to `ClothingStore.Desktop.exe`.
3. Start it. The first start creates the empty `ClothingStorePOS` database with one user: `admin` / `admin123`
   (a new password is required at first sign-in).
4. **License:** without a key the app runs a **7-day free trial** (orange banner with the days left and your contact).
   To license it, the customer clicks **Enter new license key**, copies **this PC's ID** and sends it to you; make the
   key in License Maker and send it back.
5. Set up the shop: **Settings** (store name, address, phone, tax number, tax rate 11% or 0, currency, LBP rate,
   receipt and label printers, backup folder), **Users** (cashiers, managers), then categories, suppliers, products
   and delivery companies.

**Several tills:** install SQL Server Express on one PC only and point the other tills' `appsettings.json` at it
(see "Several tills" in README.md).

## 3. Updating a customer

Build a new `publish` folder and replace every file on the customer's PC **except `appsettings.json`**. On the next
start the app backs up the database and upgrades it. The license stays (it's kept in `C:\ProgramData\ClothingStorePOS`).
