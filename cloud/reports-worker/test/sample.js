// Made-up reports in the format the POS uploads, for trying the site locally (npm run seed) and for the tests.
// The real ones are built by ClothingStore.Desktop's OnlineReportsBuilder.

const pad = (n) => String(n).padStart(2, "0");
const isoDate = (d) => `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
const isoTime = (d) => `${isoDate(d)}T${pad(d.getHours())}:${pad(d.getMinutes())}:00`;

function rowsWithShare(list) {
  const total = list.reduce((s, r) => s + r.amount, 0);
  return list.map((r) => ({ ...r, share: total ? Math.round(r.amount / total * 1000) / 10 : 0 }));
}

function period(from, to, scale) {
  const days = [];
  for (let d = new Date(from); d <= to; d.setDate(d.getDate() + 1)) {
    days.push({ date: isoDate(d), count: Math.round(20 * scale), total: Math.round(900 * scale + (d.getDate() % 7) * 60) });
  }
  const totalSales = days.reduce((s, d) => s + d.total, 0);
  const transactions = days.reduce((s, d) => s + d.count, 0);
  const cost = totalSales * 0.52;
  return {
    from: isoDate(from),
    to: isoDate(to),
    generatedAt: isoTime(new Date()),
    totals: {
      totalSales, grossSales: totalSales * 1.05, discounts: totalSales * 0.05, tax: 0, netRevenue: totalSales * 0.98,
      costOfGoods: cost, grossProfit: totalSales * 0.98 - cost, marginPercent: 46.9, transactions, itemsSold: transactions * 2,
      averageBasket: transactions ? Math.round(totalSales / transactions * 100) / 100 : 0, refunds: totalSales * 0.02,
      returnsCount: Math.round(transactions * 0.03), voidedCount: 1, deliveryFees: 24,
    },
    change: { sales: 12.4, transactions: -3.1, profit: 8 },
    byDay: days,
    byHour: [10, 11, 12, 13, 15, 16, 17, 18, 19, 20].map((hour, i) => ({ hour, count: 2 + (i % 4), total: 60 + ((i * 37) % 140) * scale })),
    payments: rowsWithShare([
      { name: "Cash", amount: totalSales * 0.55, count: Math.round(transactions * 0.6) },
      { name: "Card", amount: totalSales * 0.3, count: Math.round(transactions * 0.3) },
      { name: "Whish", amount: totalSales * 0.15, count: Math.round(transactions * 0.1) },
    ]),
    categories: rowsWithShare([
      { name: "Jeans", qty: 40, amount: totalSales * 0.35, profit: totalSales * 0.17 },
      { name: "Dresses", qty: 22, amount: totalSales * 0.3, profit: totalSales * 0.14 },
      { name: "T-shirts", qty: 60, amount: totalSales * 0.2, profit: totalSales * 0.1 },
      { name: "Abayas", qty: 8, amount: totalSales * 0.15, profit: totalSales * 0.07 },
    ]),
    products: [
      ["Slim jeans <b>blue</b>", 18, 810], ["Summer dress", 11, 660], ["Basic tee", 30, 300], ["Linen shirt", 9, 405], ["Abaya black", 4, 360],
    ].map(([name, qty, amount]) => ({ name, qty, amount: amount * scale, profit: amount * scale * 0.45 })),
    cashiers: rowsWithShare([{ name: "Maya", amount: totalSales * 0.6, count: 40, qty: 80 }, { name: "Karim", amount: totalSales * 0.4, count: 25, qty: 50 }]),
    sizes: rowsWithShare([{ name: "M", qty: 40, amount: 1200 }, { name: "L", qty: 30, amount: 900 }, { name: "S", qty: 20, amount: 600 }]),
    channels: rowsWithShare([{ name: "In store", amount: totalSales * 0.8, count: 50 }, { name: "WhatsApp", amount: totalSales * 0.2, count: 9 }]),
    returnsByReason: [{ name: "Wrong size", amount: 45, count: 2 }],
    topReturned: [{ name: "Slim jeans", qty: 1, amount: 45, count: 1 }],
    refundsByMethod: [{ name: "Cash", amount: 45, count: 2 }],
    settlements: [{ date: isoTime(to), courier: "Toters", method: "Cash", orders: 4, expected: 120, received: 115 }],
    drawerCloses: [{ closedAt: isoTime(to), cashier: "Maya", till: "Till 1", expected: 640, counted: 635, expectedLbp: 4500000, countedLbp: 4500000 }],
  };
}

export function sampleDocs(now = new Date()) {
  const today = new Date(now.getFullYear(), now.getMonth(), now.getDate());
  const days = (n) => new Date(today.getFullYear(), today.getMonth(), today.getDate() + n);
  const monday = days(-((7 + today.getDay() - 1) % 7));
  const monthStart = new Date(today.getFullYear(), today.getMonth(), 1);
  const lastMonthStart = new Date(today.getFullYear(), today.getMonth() - 1, 1);
  const lastMonthEnd = new Date(today.getFullYear(), today.getMonth(), 0);

  const docs = {
    store: { name: "Rami Boutique", currency: "$", lbp: true, lbpRate: 89500, language: "en" },
    live: {
      generatedAt: isoTime(now),
      openDrawers: [{ till: "Till 1", cashier: "Maya", openedAt: isoTime(new Date(today.getTime() + 9.5 * 3600_000)), sales: 23, expectedCash: 412.5, expectedCashLbp: 2300000 }],
      recentSales: Array.from({ length: 20 }, (_, i) => ({
        receipt: `R-${String(10450 - i).padStart(6, "0")}`, time: isoTime(new Date(now.getTime() - i * 17 * 60_000)),
        cashier: i % 3 ? "Maya" : "Karim", total: 25 + ((i * 13) % 90), items: 1 + (i % 3), status: i === 4 ? "voided" : "completed",
        channel: i % 5 === 0 ? "WhatsApp" : null,
      })),
      recentCloses: [{ closedAt: isoTime(days(-1)), cashier: "Karim", till: "Till 1", expected: 980, counted: 970, expectedLbp: 9000000, countedLbp: 9100000 }],
      lowStockCount: 7,
      outOfStockCount: 3,
    },
    stock: {
      generatedAt: isoTime(now), skus: 840, units: 3120, costValue: 41250.5, retailValue: 88900, lowStockCount: 7, outOfStockCount: 3,
      slowSinceDays: 60,
      byCategory: rowsWithShare([{ name: "Jeans", qty: 900, amount: 15000 }, { name: "Dresses", qty: 600, amount: 12000 }]),
      lowStock: [{ product: "Slim jeans", variant: "M / Blue", sku: "SJ-M-BL", stock: 1, reorder: 3 }, { product: "Basic tee", variant: "S / White", sku: "BT-S-WH", stock: 0, reorder: 5 }],
      slowMovers: [{ product: "Winter coat", variant: "L / Grey", stock: 6, value: 330, lastSold: isoTime(days(-120)) }, { product: "Scarf", variant: "", stock: 12, value: 60, lastSold: null }],
    },
    "period:today": period(today, today, 1),
    "period:yesterday": period(days(-1), days(-1), 1),
    "period:week": period(monday, today, 1),
    "period:lastweek": period(new Date(monday.getTime() - 7 * 86_400_000), new Date(monday.getTime() - 86_400_000), 1),
    "period:month": period(monthStart, today, 1),
    "period:lastmonth": period(lastMonthStart, lastMonthEnd, 1),
    "period:year": period(new Date(today.getFullYear(), 0, 1), today, 1),
  };
  for (let i = 1; i <= 5; i++) docs[`day:${isoDate(days(-i))}`] = period(days(-i), days(-i), 1);
  docs[`month:${isoDate(lastMonthStart).slice(0, 7)}`] = period(lastMonthStart, lastMonthEnd, 1);
  return docs;
}
