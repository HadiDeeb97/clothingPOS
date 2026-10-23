# Online reports website

The shop's reports on any phone or computer, at no cost. The POS sends its reports here every 5 minutes. Owners and
managers sign in with their **POS username and password** and see them, even when the shop PC is off.

It runs on the shop's own **free Cloudflare account**: a Worker (the website) and a D1 database (the reports). Normally
the POS sets all of this up by itself (**Settings → Online reports → Set up**, see INSTALL.md). This folder is the
website's source code. The POS carries a copy and publishes it.

## How it works

- **The POS works out the reports** with the same code as its Reports page and uploads them as small JSON documents
  (`POST /api/upload`, with the shop's secret upload key). The website only stores and displays them, so its numbers
  are the POS's numbers.
- **Nothing on the website can change the shop's data.** It never connects to the shop.
- **Sign-in uses the POS accounts.** Only active managers and admins can sign in; cashiers can't.
  - **No passwords are uploaded.** For each account the POS sends its password salt and a SHA-256 of its stored PBKDF2
    key.
  - **The phone does the slow PBKDF2 step** (100,000 rounds, the same as the POS). The Worker only compares hashes, so
    it stays within the free plan's CPU limit.
  - **Lockout:** 5 wrong passwords lock an account for 15 minutes.
  - **Sessions:** last 30 days. They end when the password changes or the account is removed in the POS, and the POS
    can sign out all devices at once.
- **Pages:**
  - Today
  - Reports (standard periods, any of the last 13 months' days, any month)
  - Sales
  - Drawers
  - Stock

  All pages are in English or Arabic.

## Documents the POS uploads

| Key | Contents |
|---|---|
| `store` | Name, currency symbol, LBP on/off, language |
| `live` | Open drawers, last 20 sales, recent drawer closes, low-stock count |
| `period:today`, `period:yesterday`, `period:week`, `period:lastweek`, `period:month`, `period:lastmonth`, `period:year` | A full report for that period |
| `day:YYYY-MM-DD` | A finished day's report (kept 13 months) |
| `month:YYYY-MM` | A finished month's report (kept) |
| `stock` | Stock value, low stock, slow movers |

The exact shapes are in `test/sample.js`, and the POS builds them in `OnlineReportsBuilder.cs`.

## Trying it locally

You need Node.js 20 or later.

```
cd cloud/reports-worker
npm install
echo UPLOAD_KEY="local-dev-key" > .dev.vars
npx wrangler dev          # serves http://localhost:8787 with a local database
npm run seed              # in a second window: sample reports + account demo / demo-pass
```

Then open http://localhost:8787 and sign in as **demo / demo-pass**.

## Tests

```
npm test
```

The tests start the Worker locally (workerd with a local D1) and check:
- uploads and the upload key
- sign-in, lockout and sessions
- password changes, removed accounts and sign out all devices
- old-day cleanup

## Deploying by hand (for testing with your own account)

The POS does this for each shop. To test it by hand:

```
npx wrangler login
npx wrangler d1 create pos-reports            # copy the database_id it prints into wrangler.toml
npx wrangler secret put UPLOAD_KEY            # type a long random key
npx wrangler deploy                           # prints https://pos-reports.<your-subdomain>.workers.dev
npm run seed -- https://pos-reports.<your-subdomain>.workers.dev <the same key>
```

## Free plan limits

One shop uses well under 1% of them:
- 100,000 requests a day
- 10 ms of CPU per request
- 5 GB of D1 storage
- 100,000 database writes a day
- 50 queries per request

Because of the 50-query limit, the POS sends at most 30 documents per upload.
