# Crepe Beirut website

The Crepe Beirut website: an introduction to the shop and the full menu. Customers can order
from it. They add items (with sizes, Hot/Iced and extras), fill in their name, phone and address,
and press **Send order on WhatsApp**. WhatsApp opens with the order already typed out for
**+961 81 908 914**, and the customer presses send.

It is plain HTML, CSS and JavaScript. There is no build step, server or database.

## Files

| File | What it holds |
| --- | --- |
| `index.html` | Page layout and text (hero, how ordering works, about, contact) |
| `js/config.js` | **Shop settings**: WhatsApp number, address, hours, social links, delivery fee, optional $ rate |
| `js/menu.js` | **The menu**: categories, items, prices (L.L.) and extras |
| `js/app.js` | Cart, item options, checkout form and the WhatsApp message |
| `css/styles.css` | Styling, using the brand colours (sky blue, butter, cream, cocoa) |
| `assets/` | Logo files and illustrations, taken from the brand pack and printed menu |

## Common edits

- **Change a price or item:** edit `js/menu.js`. Each item is `{ name: "Nutella Crepe", price: 350000 }`.
  Items that come in sizes use `options: [{ label: "12 pcs", price: 300000 }, ...]`.
- **Add the address and opening hours:** fill `address`, `hours` and `mapsUrl` in `js/config.js`.
  Empty values stay hidden on the page.
- **Charge a delivery fee:** set `deliveryFee` in `js/config.js` (in L.L.). It is added to delivery orders.
- **Show dollar totals:** set `usdRate` (L.L. per $1) in `js/config.js`.

## Preview locally

Open `index.html` in a browser, or run a small server from this folder:

```sh
npx serve .
```

## Put it online

Any static host works. Two free options:

- **GitHub Pages:** push this folder to its own repository, then in *Settings → Pages* choose the
  branch and the root folder.
- **Netlify / Cloudflare Pages:** drag and drop this folder, or connect the repository with
  no build command and this folder as the publish directory.

## Check before going live

- `Hazelnut Frappe` is listed at **50,000 L.L.** on the printed menu, while the other frappes are
  350,000. It was kept as printed. Fix it in `js/menu.js` if it is a typo.
- The Instagram link in `js/config.js` assumes the handle `crepe.beirut` from the menu. Facebook and
  TikTok links are empty until you add them.
- The illustrations come from the Canva menu design. Check that your Canva licence covers using them on
  a website. You can swap them for your own photos in `assets/img/` (same file names).
