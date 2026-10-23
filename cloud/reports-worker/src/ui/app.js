// The reports website, in the browser. Everything shown is built with textContent (never innerHTML), so product and
// customer-facing names from the POS can't inject anything into the page.
"use strict";

// ---- Text ---------------------------------------------------------------------------------------------------------

const TEXT = {
  en: {
    signInTitle: "Shop reports",
    signInHint: "Sign in with your POS username and password.",
    username: "Username",
    password: "Password",
    signIn: "Sign in",
    signingIn: "Signing in…",
    wrong: "Wrong username or password.",
    wrongLeft: "Wrong username or password. {0} tries left before a 15-minute lock.",
    locked: "Too many wrong tries. Try again in {0} min.",
    offlineError: "No connection. Check the internet and try again.",
    serverError: "Something went wrong. Try again.",
    signOut: "Sign out",
    language: "العربية",
    refresh: "Refresh",
    menu: "Menu",
    signedInAs: "Signed in as {0}",
    tabToday: "Today",
    tabReports: "Reports",
    tabSales: "Sales",
    tabDrawers: "Drawers",
    tabStock: "Stock",
    updated: "Updated {0}",
    shopOffline: "Shop offline since {0}. These are the last numbers it sent.",
    neverUploaded: "The shop hasn't sent any reports yet.",
    loading: "Loading…",
    nothing: "Nothing yet.",
    noData: "No report for this period yet.",
    totalSales: "Sales",
    netRevenue: "Net revenue",
    profit: "Profit",
    margin: "{0}% margin",
    transactions: "Transactions",
    avgBasket: "Average basket",
    itemsSold: "Items sold",
    discounts: "Discounts",
    tax: "Tax",
    refunds: "Refunds",
    returns: "Returns",
    voided: "Voided",
    deliveryFees: "Delivery fees",
    costOfGoods: "Cost of goods",
    vsPrevious: "{0} vs previous",
    byHour: "Sales by hour",
    byDay: "Sales by day",
    payments: "Payments",
    categories: "Categories",
    topProducts: "Top products",
    cashiers: "Cashiers",
    sizes: "Sizes",
    channels: "Channels",
    returnsByReason: "Returns by reason",
    topReturned: "Most returned",
    refundsByMethod: "Refunds by method",
    settlements: "Delivery company payments",
    drawerCloses: "Drawer closes",
    summary: "Summary",
    openDrawers: "Open drawers",
    noOpenDrawers: "No drawer is open.",
    recentCloses: "Recent closes",
    lastSales: "Last sales",
    seeAll: "See all",
    lowStockWarn: "{0} items are low on stock.",
    period_today: "Today",
    period_yesterday: "Yesterday",
    period_week: "This week",
    period_lastweek: "Last week",
    period_month: "This month",
    period_lastmonth: "Last month",
    period_year: "This year",
    pickDay: "A day",
    pickMonth: "A month",
    choose: "Choose…",
    receipt: "Receipt",
    time: "Time",
    cashier: "Cashier",
    total: "Total",
    items: "Items",
    voidedTag: "Voided",
    till: "Till",
    since: "Since",
    expected: "Expected",
    expectedHint: "Amounts are the cash expected in each drawer now.",
    counted: "Counted",
    overShort: "Over / short",
    closed: "Closed",
    notCounted: "Not counted",
    salesCount: "{0} sales",
    stockValue: "Stock value (cost)",
    retailValue: "Retail value",
    units: "Units in stock",
    outOfStock: "Out of stock",
    lowStock: "Low stock",
    slowMovers: "Slow movers (no sales in {0} days)",
    product: "Product",
    inStock: "In stock",
    reorderAt: "Reorder at",
    value: "Value",
    lastSold: "Last sold",
    never: "Never",
    courier: "Company",
    orders: "Orders",
    received: "Received",
    difference: "Difference",
    byCategoryStock: "Stock by category",
    qty: "{0} pcs",
    count: "{0}×",
  },
  ar: {
    signInTitle: "تقارير المحل",
    signInHint: "سجّل الدخول باسم المستخدم وكلمة المرور الخاصة بنظام البيع.",
    username: "اسم المستخدم",
    password: "كلمة المرور",
    signIn: "تسجيل الدخول",
    signingIn: "جارٍ تسجيل الدخول…",
    wrong: "اسم المستخدم أو كلمة المرور غير صحيحة.",
    wrongLeft: "اسم المستخدم أو كلمة المرور غير صحيحة. بقي {0} محاولات قبل القفل لمدة 15 دقيقة.",
    locked: "محاولات خاطئة كثيرة. حاول مجددًا بعد {0} دقيقة.",
    offlineError: "لا يوجد اتصال. تحقّق من الإنترنت وحاول مجددًا.",
    serverError: "حدث خطأ. حاول مجددًا.",
    signOut: "تسجيل الخروج",
    language: "English",
    refresh: "تحديث",
    menu: "القائمة",
    signedInAs: "مسجّل باسم {0}",
    tabToday: "اليوم",
    tabReports: "التقارير",
    tabSales: "المبيعات",
    tabDrawers: "الصناديق",
    tabStock: "المخزون",
    updated: "آخر تحديث {0}",
    shopOffline: "المحل غير متصل منذ {0}. هذه آخر أرقام أرسلها.",
    neverUploaded: "لم يرسل المحل أي تقارير بعد.",
    loading: "جارٍ التحميل…",
    nothing: "لا شيء بعد.",
    noData: "لا يوجد تقرير لهذه الفترة بعد.",
    totalSales: "المبيعات",
    netRevenue: "صافي الإيرادات",
    profit: "الربح",
    margin: "هامش {0}%",
    transactions: "عدد العمليات",
    avgBasket: "متوسط الفاتورة",
    itemsSold: "القطع المباعة",
    discounts: "الخصومات",
    tax: "الضريبة",
    refunds: "المبالغ المستردّة",
    returns: "المرتجعات",
    voided: "الملغاة",
    deliveryFees: "رسوم التوصيل",
    costOfGoods: "كلفة البضاعة",
    vsPrevious: "{0} مقارنة بالفترة السابقة",
    byHour: "المبيعات حسب الساعة",
    byDay: "المبيعات حسب اليوم",
    payments: "طرق الدفع",
    categories: "الفئات",
    topProducts: "الأكثر مبيعًا",
    cashiers: "أمناء الصندوق",
    sizes: "المقاسات",
    channels: "قنوات البيع",
    returnsByReason: "المرتجعات حسب السبب",
    topReturned: "الأكثر إرجاعًا",
    refundsByMethod: "الاسترداد حسب الطريقة",
    settlements: "دفعات شركات التوصيل",
    drawerCloses: "إغلاقات الصندوق",
    summary: "الملخّص",
    openDrawers: "الصناديق المفتوحة",
    noOpenDrawers: "لا يوجد صندوق مفتوح.",
    recentCloses: "آخر الإغلاقات",
    lastSales: "آخر المبيعات",
    seeAll: "عرض الكل",
    lowStockWarn: "{0} أصناف مخزونها منخفض.",
    period_today: "اليوم",
    period_yesterday: "أمس",
    period_week: "هذا الأسبوع",
    period_lastweek: "الأسبوع الماضي",
    period_month: "هذا الشهر",
    period_lastmonth: "الشهر الماضي",
    period_year: "هذه السنة",
    pickDay: "يوم محدّد",
    pickMonth: "شهر محدّد",
    choose: "اختر…",
    receipt: "الفاتورة",
    time: "الوقت",
    cashier: "أمين الصندوق",
    total: "المجموع",
    items: "القطع",
    voidedTag: "ملغاة",
    till: "الصندوق",
    since: "منذ",
    expected: "المتوقّع",
    expectedHint: "المبالغ هي النقد المتوقّع في كل صندوق الآن.",
    counted: "المعدود",
    overShort: "زيادة / نقص",
    closed: "أُغلق",
    notCounted: "لم يُعدّ",
    salesCount: "{0} عملية بيع",
    stockValue: "قيمة المخزون (الكلفة)",
    retailValue: "قيمة البيع",
    units: "القطع في المخزون",
    outOfStock: "نفد من المخزون",
    lowStock: "مخزون منخفض",
    slowMovers: "بطيء البيع (لا مبيعات منذ {0} يومًا)",
    product: "المنتج",
    inStock: "المتوفّر",
    reorderAt: "حدّ الطلب",
    value: "القيمة",
    lastSold: "آخر بيع",
    never: "أبدًا",
    courier: "الشركة",
    orders: "الطلبات",
    received: "المستلم",
    difference: "الفرق",
    byCategoryStock: "المخزون حسب الفئة",
    qty: "{0} قطعة",
    count: "{0}×",
  },
};

const PERIODS = ["today", "yesterday", "week", "lastweek", "month", "lastmonth", "year"];
const TABS = ["today", "reports", "sales", "drawers", "stock"];
const OFFLINE_AFTER_MS = 15 * 60_000;

const state = {
  lang: "en",
  me: null,
  tab: "today",
  report: "period:today",
  docs: {},
};

function t(key, ...args) {
  const text = TEXT[state.lang][key] ?? TEXT.en[key] ?? key;
  return text.replace(/\{(\d)\}/g, (_, i) => String(args[Number(i)] ?? ""));
}

function setLanguage(lang) {
  state.lang = lang === "ar" ? "ar" : "en";
  document.documentElement.lang = state.lang;
  document.documentElement.dir = state.lang === "ar" ? "rtl" : "ltr";
  try { localStorage.setItem("lang", state.lang); } catch { /* private mode */ }
}

function savedLanguage() {
  try { return localStorage.getItem("lang"); } catch { return null; }
}

// ---- Formatting ---------------------------------------------------------------------------------------------------

const locale = () => (state.lang === "ar" ? "ar-u-nu-latn" : "en-GB");

function money(value) {
  const symbol = state.me?.store?.currency ?? "$";
  const n = Number(value ?? 0);
  const text = Math.abs(n).toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  return (n < 0 ? "-" : "") + symbol + text;
}

function lbp(value) {
  const text = Math.round(Number(value ?? 0)).toLocaleString("en-US");
  return state.lang === "ar" ? `${text} ل.ل.` : `${text} LBP`;
}

function number(value) {
  return Number(value ?? 0).toLocaleString("en-US");
}

function signed(value, format) {
  const n = Number(value ?? 0);
  return (n > 0 ? "+" : "") + format(n);
}

function percent(value) {
  if (value === null || value === undefined) return null;
  return (value > 0 ? "+" : "") + Number(value).toLocaleString("en-US", { maximumFractionDigits: 1 }) + "%";
}

/** The POS sends its own local times ("2026-10-23T16:42:00"); shown as they are, in the shop's time. */
function parseLocal(text) {
  const m = /^(\d{4})-(\d{2})-(\d{2})(?:T(\d{2}):(\d{2}))?/.exec(text ?? "");
  return m ? new Date(Number(m[1]), Number(m[2]) - 1, Number(m[3]), Number(m[4] ?? 0), Number(m[5] ?? 0)) : null;
}

function timeOf(text) {
  const d = parseLocal(text);
  return d ? d.toLocaleTimeString(locale(), { hour: "2-digit", minute: "2-digit", hour12: false }) : "";
}

function dayOf(text, withYear = false) {
  const d = parseLocal(text);
  if (!d) return "";
  return d.toLocaleDateString(locale(), { weekday: "short", day: "numeric", month: "short", ...(withYear ? { year: "numeric" } : {}) });
}

function dateTimeOf(text) {
  const d = parseLocal(text);
  if (!d) return "";
  const today = new Date();
  const sameDay = d.toDateString() === today.toDateString();
  return sameDay ? timeOf(text) : `${dayOf(text)} ${timeOf(text)}`;
}

function monthOf(key) {
  const [y, m] = key.split("-").map(Number);
  return new Date(y, m - 1, 1).toLocaleDateString(locale(), { month: "long", year: "numeric" });
}

/** A time sent by the server (milliseconds), in the phone's own clock. */
function clockOf(ms) {
  const d = new Date(ms);
  const sameDay = d.toDateString() === new Date().toDateString();
  const time = d.toLocaleTimeString(locale(), { hour: "2-digit", minute: "2-digit", hour12: false });
  return sameDay ? time : `${d.toLocaleDateString(locale(), { day: "numeric", month: "short" })} ${time}`;
}

// ---- DOM ----------------------------------------------------------------------------------------------------------

function h(tag, attrs, ...children) {
  const el = document.createElement(tag);
  for (const [key, value] of Object.entries(attrs ?? {})) {
    if (value === null || value === undefined || value === false) continue;
    if (key === "class") el.className = value;
    else if (key.startsWith("on")) el.addEventListener(key.slice(2), value);
    else if (key === "style") el.style.cssText = value; // through the style object: the page allows no inline style attributes
    else el.setAttribute(key, value === true ? "" : value);
  }
  for (const child of children.flat(Infinity)) {
    if (child === null || child === undefined || child === false) continue;
    el.append(child instanceof Node ? child : document.createTextNode(String(child)));
  }
  return el;
}

const $ = (id) => document.getElementById(id);

// ---- Server -------------------------------------------------------------------------------------------------------

class SignedOut extends Error {}

async function api(path, options) {
  const response = await fetch(path, { credentials: "same-origin", ...options });
  if (response.status === 401 && path !== "/api/login") throw new SignedOut();
  const body = await response.json().catch(() => ({}));
  return { status: response.status, body };
}

async function loadMe() {
  const { status, body } = await api("/api/me");
  if (status !== 200) throw new Error(String(status));
  state.me = body;
  return body;
}

async function loadDocs(keys) {
  const missing = keys.filter(Boolean);
  if (missing.length === 0) return;
  const { status, body } = await api(`/api/docs?keys=${encodeURIComponent(missing.join(","))}`);
  if (status !== 200) throw new Error(String(status));
  for (const key of missing) state.docs[key] = body.docs[key]?.body ?? null;
}

// ---- Sign in ------------------------------------------------------------------------------------------------------

/**
 * The browser does the slow part of the password check (the same PBKDF2 the POS uses), so the server only compares a
 * hash: that fits the free plan, and the password itself never leaves the phone.
 */
async function passwordProof(username, password) {
  const { status, body } = await api(`/api/salt?u=${encodeURIComponent(username)}`);
  if (status !== 200) throw new Error("salt");
  const salt = Uint8Array.from(atob(body.salt), (c) => c.charCodeAt(0));
  const key = await crypto.subtle.importKey("raw", new TextEncoder().encode(password), "PBKDF2", false, ["deriveBits"]);
  const bits = await crypto.subtle.deriveBits({ name: "PBKDF2", hash: "SHA-256", salt, iterations: body.iterations }, key, 256);
  return btoa(String.fromCharCode(...new Uint8Array(bits)));
}

function showLogin(message) {
  state.me = null;
  state.docs = {};
  $("app").classList.add("hidden");
  const view = $("login");
  view.classList.remove("hidden");
  view.replaceChildren();

  const error = h("p", { class: "error", role: "alert" }, message ?? "");
  const username = h("input", { name: "username", autocomplete: "username", autocapitalize: "none", spellcheck: "false", required: true });
  const password = h("input", { name: "password", type: "password", autocomplete: "current-password", required: true });
  const submit = h("button", { class: "primary", type: "submit" }, t("signIn"));
  const lang = h("button", { class: "icon-btn", type: "button", onclick: () => { setLanguage(state.lang === "ar" ? "en" : "ar"); showLogin(); } }, t("language"));

  const form = h("form", {
    onsubmit: async (e) => {
      e.preventDefault();
      error.textContent = "";
      submit.disabled = true;
      submit.textContent = t("signingIn");
      try {
        const user = username.value.trim();
        const proof = await passwordProof(user, password.value);
        const { status, body } = await api("/api/login", {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ username: user, proof }),
        });
        if (status === 200) {
          password.value = "";
          await start();
          return;
        }
        error.textContent = status === 429 ? t("locked", body.minutes)
          : status === 401 ? (body.left > 0 && body.left < 5 ? t("wrongLeft", body.left) : t("wrong"))
          : t("serverError");
      } catch (err) {
        error.textContent = err instanceof TypeError ? t("offlineError") : t("serverError");
      }
      submit.disabled = false;
      submit.textContent = t("signIn");
    },
  },
    h("div", { style: "display:flex;justify-content:space-between;align-items:center;gap:8px" }, h("h1", null, t("signInTitle")), lang),
    h("p", { class: "muted", style: "margin:0" }, t("signInHint")),
    h("label", null, t("username"), username),
    h("label", null, t("password"), password),
    error,
    submit);
  view.append(form);
  username.focus();
}

async function signOut() {
  await fetch("/api/logout", { method: "POST" }).catch(() => {});
  showLogin();
}

// ---- Shell --------------------------------------------------------------------------------------------------------

async function start() {
  try {
    await loadMe();
  } catch (err) {
    if (err instanceof SignedOut) return showLogin();
    $("login").classList.add("hidden");
    $("app").classList.remove("hidden");
    $("content").replaceChildren(h("div", { class: "loading" }, err instanceof TypeError ? t("offlineError") : t("serverError")));
    return;
  }
  if (!savedLanguage() && state.me.store?.language) setLanguage(state.me.store.language);
  $("login").classList.add("hidden");
  $("app").classList.remove("hidden");
  readHash();
  renderShell();
  await show();
}

function readHash() {
  const [tab, report] = location.hash.slice(1).split("/");
  state.tab = TABS.includes(tab) ? tab : "today";
  if (tab === "reports" && report) state.report = decodeURIComponent(report);
}

function renderShell() {
  document.title = state.me.store?.name ? `${state.me.store.name} · ${t("signInTitle")}` : t("signInTitle");
  $("store-name").textContent = state.me.store?.name ?? t("signInTitle");
  renderStatus();
  $("menu-who").textContent = t("signedInAs", state.me.user.name);
  $("menu-lang").textContent = t("language");
  $("menu-refresh").textContent = t("refresh");
  $("menu-signout").textContent = t("signOut");
  $("menu-button").textContent = t("menu");
  $("menu-button").setAttribute("aria-label", t("menu"));
  const tabs = $("tabs");
  tabs.replaceChildren(...TABS.map((tab) =>
    h("a", { href: `#${tab}`, class: tab === state.tab ? "active" : null }, t(`tab${tab[0].toUpperCase()}${tab.slice(1)}`))));
}

function renderStatus() {
  const status = $("status");
  const { lastUpload, now } = state.me;
  status.classList.remove("offline");
  if (!lastUpload) {
    status.textContent = t("neverUploaded");
  } else if (now - lastUpload > OFFLINE_AFTER_MS) {
    status.textContent = t("shopOffline", clockOf(lastUpload));
    status.classList.add("offline");
  } else {
    status.textContent = t("updated", clockOf(lastUpload));
  }
}

async function show() {
  const content = $("content");
  const views = { today: viewToday, reports: viewReports, sales: viewSales, drawers: viewDrawers, stock: viewStock };
  const view = views[state.tab];
  const keys = view.keys();
  if (keys.some((k) => !(k in state.docs))) content.replaceChildren(h("div", { class: "loading" }, t("loading")));
  try {
    await loadDocs(keys.filter((k) => !(k in state.docs)));
  } catch (err) {
    if (err instanceof SignedOut) return showLogin();
    content.replaceChildren(h("div", { class: "loading" }, err instanceof TypeError ? t("offlineError") : t("serverError")));
    return;
  }
  content.replaceChildren(...view.render());
  window.scrollTo({ top: 0 });
}

async function refresh() {
  state.docs = {};
  try {
    await loadMe();
  } catch (err) {
    if (err instanceof SignedOut) return showLogin();
  }
  renderShell();
  await show();
}

// ---- Building blocks ----------------------------------------------------------------------------------------------

function card(title, ...children) {
  return h("section", { class: "card" }, title ? h("h2", null, title) : null, ...children);
}

function kpi(label, value, sub, subClass) {
  return h("div", { class: "kpi" },
    h("div", { class: "label" }, label),
    h("div", { class: "value num" }, value),
    sub ? h("div", { class: `sub ${subClass ?? ""}` }, sub) : null);
}

function change(value) {
  const text = percent(value);
  if (text === null) return [null, null];
  return [t("vsPrevious", text), value >= 0 ? "good" : "bad"];
}

/** Name … amount rows with a share bar, for payments, categories, products… */
function rows(list, { amount = (r) => money(r.amount), meta, limit } = {}) {
  if (!list?.length) return h("div", { class: "empty" }, t("nothing"));
  const shown = limit ? list.slice(0, limit) : list;
  const max = Math.max(...shown.map((r) => Math.abs(Number(r.amount ?? 0))), 0);
  return h("div", { class: "rows" }, shown.map((r) =>
    h("div", { class: "row" },
      h("span", { class: "name", title: r.name }, h("bdi", null, r.name)),
      h("span", { class: "num" }, amount(r)),
      meta ? h("span", { class: "meta" }, meta(r)) : null,
      h("div", { class: "bar" }, h("span", { style: `width:${max ? Math.round(Math.abs(r.amount) / max * 100) : 0}%` })))));
}

function columns(points, label) {
  if (!points?.length) return h("div", { class: "empty" }, t("nothing"));
  const max = Math.max(...points.map((p) => p.value), 0);
  return h("div", null,
    h("div", { class: "columns" }, points.map((p) =>
      h("div", { title: `${p.label}: ${money(p.value)}` }, h("span", { style: `height:${max ? Math.max(1, Math.round(p.value / max * 100)) : 0}%` })))),
    h("div", { class: "columns-axis" }, points.map((p, i) => h("span", null, label(p, i, points.length)))));
}

/** Every hour from the first to the last with sales, so quiet hours show as gaps. */
function hourPoints(byHour) {
  if (!byHour?.length) return [];
  const first = Math.min(...byHour.map((x) => x.hour));
  const last = Math.max(...byHour.map((x) => x.hour));
  const points = [];
  for (let hour = first; hour <= last; hour++) {
    const x = byHour.find((p) => p.hour === hour);
    points.push({ label: `${String(hour).padStart(2, "0")}:00`, value: x ? Number(x.total) : 0, hour });
  }
  return points;
}

function table(headers, data) {
  return h("div", { class: "table-wrap" }, h("table", null,
    h("thead", null, h("tr", null, headers.map(([text, end]) => h("th", { class: end ? "end" : null }, text)))),
    h("tbody", null, data)));
}

function td(value, end, cls) {
  // <bdi>: a name in English on the Arabic page (or the other way round) keeps its own direction and punctuation.
  return h("td", { class: [end ? "end" : "", cls ?? ""].join(" ").trim() || null }, typeof value === "string" ? h("bdi", null, value) : value);
}

function variance(expected, counted, format) {
  if (counted === null || counted === undefined) return td(t("notCounted"), true, "muted");
  const diff = Number(counted) - Number(expected);
  return td(signed(diff, format), true, `num ${diff < 0 ? "bad" : diff > 0 ? "good" : ""}`);
}

// ---- Report sections (Today and Reports) --------------------------------------------------------------------------

function reportKpis(doc) {
  const s = doc.totals ?? {};
  const [salesChange, salesClass] = change(doc.change?.sales);
  const [profitChange, profitClass] = change(doc.change?.profit);
  const [countChange, countClass] = change(doc.change?.transactions);
  return h("div", { class: "kpis" },
    kpi(t("totalSales"), money(s.totalSales), salesChange, salesClass),
    kpi(t("profit"), money(s.grossProfit), profitChange ?? t("margin", s.marginPercent ?? 0), profitChange ? profitClass : null),
    kpi(t("transactions"), number(s.transactions), countChange, countClass),
    kpi(t("avgBasket"), money(s.averageBasket), t("qty", number(s.itemsSold))));
}

function summaryCard(doc) {
  const s = doc.totals ?? {};
  const line = (label, value, cls) => h("tr", null, h("td", null, label), td(value, true, `num ${cls ?? ""}`));
  return card(t("summary"), h("table", null, h("tbody", null,
    line(t("totalSales"), money(s.totalSales)),
    line(t("discounts"), money(s.discounts)),
    line(t("tax"), money(s.tax)),
    line(`${t("refunds")} (${number(s.returnsCount)})`, money(s.refunds)),
    line(t("netRevenue"), money(s.netRevenue)),
    line(t("costOfGoods"), money(s.costOfGoods)),
    line(t("profit"), `${money(s.grossProfit)} · ${s.marginPercent ?? 0}%`, s.grossProfit < 0 ? "bad" : ""),
    s.deliveryFees ? line(t("deliveryFees"), money(s.deliveryFees)) : null,
    s.voidedCount ? line(t("voided"), number(s.voidedCount)) : null)));
}

const qtyMeta = (r) => [r.qty ? t("qty", number(r.qty)) : null, r.share ? `${r.share}%` : null].filter(Boolean).join(" · ");
const countMeta = (r) => [r.count ? t("count", number(r.count)) : null, r.share ? `${r.share}%` : null].filter(Boolean).join(" · ");
const profitMeta = (r) => [t("qty", number(r.qty)), `${t("profit")} ${money(r.profit)}`].join(" · ");

function fullReport(doc) {
  const multiDay = doc.from !== doc.to;
  const days = (doc.byDay ?? []).map((d) => ({ label: dayOf(d.date), value: Number(d.total), date: d.date }));
  const parts = [
    reportKpis(doc),
    card(multiDay ? t("byDay") : t("byHour"), multiDay
      ? columns(days, (p, i, n) => (n <= 10 || i % Math.ceil(n / 8) === 0 ? String(parseLocal(p.date).getDate()) : ""))
      : columns(hourPoints(doc.byHour), (p, i, n) => (n <= 8 || i % 2 === 0 ? String(p.hour) : ""))),
    h("div", { class: "grid2" },
      summaryCard(doc),
      card(t("payments"), rows(doc.payments, { meta: countMeta }))),
    h("div", { class: "grid2" },
      card(t("categories"), rows(doc.categories, { meta: qtyMeta })),
      card(t("topProducts"), rows(doc.products, { meta: profitMeta, limit: 20 }))),
    h("div", { class: "grid2" },
      card(t("cashiers"), rows(doc.cashiers, { meta: countMeta })),
      card(t("channels"), rows(doc.channels, { meta: countMeta }))),
    h("div", { class: "grid2" },
      card(t("sizes"), rows(doc.sizes, { meta: qtyMeta, limit: 15 })),
      card(t("returnsByReason"), rows(doc.returnsByReason, { meta: countMeta }))),
  ];
  if (doc.topReturned?.length || doc.refundsByMethod?.length) {
    parts.push(h("div", { class: "grid2" },
      card(t("topReturned"), rows(doc.topReturned, { meta: qtyMeta, limit: 10 })),
      card(t("refundsByMethod"), rows(doc.refundsByMethod, { meta: countMeta }))));
  }
  if (doc.settlements?.length) {
    parts.push(card(t("settlements"), table(
      [[t("time")], [t("courier")], [t("orders"), true], [t("expected"), true], [t("received"), true], [t("difference"), true]],
      doc.settlements.map((s) => h("tr", null,
        td(dateTimeOf(s.date)), td(s.courier ?? s.method ?? ""), td(number(s.orders), true, "num"),
        td(money(s.expected), true, "num"), td(money(s.received), true, "num"),
        variance(s.expected, s.received, money))))));
  }
  if (doc.drawerCloses?.length) parts.push(card(t("drawerCloses"), closesTable(doc.drawerCloses)));
  return parts;
}

function closesTable(closes) {
  const showLbp = state.me.store?.lbp || closes.some((c) => c.expectedLbp || c.countedLbp);
  return table(
    [[t("closed")], [t("cashier")], [t("expected"), true], [t("overShort"), true], ...(showLbp ? [[`${t("overShort")} LBP`, true]] : [])],
    closes.map((c) => h("tr", null,
      td(dateTimeOf(c.closedAt)),
      td(c.till ? `${c.cashier} · ${c.till}` : c.cashier),
      td(money(c.expected), true, "num"),
      variance(c.expected, c.counted, money),
      showLbp ? variance(c.expectedLbp, c.countedLbp, lbp) : null)));
}

// ---- Pages --------------------------------------------------------------------------------------------------------

const viewToday = {
  keys: () => ["period:today", "live"],
  render() {
    const doc = state.docs["period:today"];
    const live = state.docs.live;
    const parts = [];
    if (live?.lowStockCount) {
      parts.push(h("a", { href: "#stock", class: "banner", style: "text-decoration:none;display:block" }, t("lowStockWarn", number(live.lowStockCount))));
    }
    if (!doc) return [...parts, card(null, h("div", { class: "empty" }, t("noData")))];
    parts.push(reportKpis(doc));
    parts.push(card(t("byHour"), columns(hourPoints(doc.byHour), (p, i, n) => (n <= 8 || i % 2 === 0 ? String(p.hour) : ""))));
    parts.push(h("div", { class: "grid2" },
      card(t("payments"), rows(doc.payments, { meta: countMeta })),
      card(t("openDrawers"), drawersList(live))));
    parts.push(h("div", { class: "grid2" },
      card(t("lastSales"), salesTable((live?.recentSales ?? []).slice(0, 5)), h("p", { style: "margin:10px 0 0" }, h("a", { href: "#sales" }, t("seeAll")))),
      card(t("topProducts"), rows(doc.products, { meta: profitMeta, limit: 5 }))));
    return parts;
  },
};

const viewReports = {
  keys: () => [state.report],
  render() {
    const pick = (key) => { location.hash = `reports/${encodeURIComponent(key)}`; };
    const days = state.me.days ?? [];
    const months = state.me.months ?? [];
    const chips = h("div", { class: "chips" }, PERIODS.map((p) =>
      h("button", { type: "button", class: state.report === `period:${p}` ? "active" : null, onclick: () => pick(`period:${p}`) }, t(`period_${p}`))));
    const daySelect = h("select", { onchange: (e) => e.target.value && pick(`day:${e.target.value}`) },
      h("option", { value: "" }, t("choose")),
      days.map((d) => h("option", { value: d, selected: state.report === `day:${d}` }, dayOf(d, true))));
    const monthSelect = h("select", { onchange: (e) => e.target.value && pick(`month:${e.target.value}`) },
      h("option", { value: "" }, t("choose")),
      months.map((m) => h("option", { value: m, selected: state.report === `month:${m}` }, monthOf(m))));
    const pickers = h("div", { class: "pickers" },
      days.length ? h("label", null, t("pickDay"), daySelect) : null,
      months.length ? h("label", null, t("pickMonth"), monthSelect) : null);

    const doc = state.docs[state.report];
    const head = [chips, pickers];
    if (!doc) return [...head, card(null, h("div", { class: "empty" }, t("noData")))];
    const range = doc.from === doc.to ? dayOf(doc.from, true) : `${dayOf(doc.from, true)} – ${dayOf(doc.to, true)}`;
    return [...head, h("div", { class: "range" }, range), ...fullReport(doc)];
  },
};

function salesTable(sales) {
  if (!sales.length) return h("div", { class: "empty" }, t("nothing"));
  return table(
    [[t("receipt")], [t("time")], [t("cashier")], [t("total"), true]],
    sales.map((s) => h("tr", null,
      td([h("bdi", null, s.receipt), s.status === "voided" ? [" ", h("span", { class: "tag bad" }, t("voidedTag"))] : null, s.channel ? h("div", { class: "muted", style: "font-size:12px" }, s.channel) : null]),
      td(dateTimeOf(s.time), false, "num"),
      td(s.cashier ?? ""),
      td(money(s.total), true, `num ${s.status === "voided" ? "muted" : ""}`))));
}

const viewSales = {
  keys: () => ["live"],
  render() {
    return [card(t("lastSales"), salesTable(state.docs.live?.recentSales ?? []))];
  },
};

function drawersList(live) {
  const drawers = live?.openDrawers ?? [];
  if (!drawers.length) return h("div", { class: "empty" }, t("noOpenDrawers"));
  const showLbp = state.me.store?.lbp;
  return h("div", { class: "rows" }, drawers.map((d) => h("div", { class: "row" },
    h("span", { class: "name" }, h("bdi", null, d.till ? `${d.till} · ${d.cashier}` : d.cashier)),
    h("span", { class: "num" }, money(d.expectedCash)),
    h("span", { class: "meta" }, `${t("since")} ${dateTimeOf(d.openedAt)} · ${t("salesCount", number(d.sales))}`),
    showLbp ? h("span", { class: "meta num" }, lbp(d.expectedCashLbp)) : null)));
}

const viewDrawers = {
  keys: () => ["live"],
  render() {
    const live = state.docs.live;
    const closes = live?.recentCloses ?? [];
    return [
      card(t("openDrawers"), drawersList(live), live?.openDrawers?.length ? h("p", { class: "muted", style: "margin:10px 0 0;font-size:12.5px" }, t("expectedHint")) : null),
      card(t("recentCloses"), closes.length ? closesTable(closes) : h("div", { class: "empty" }, t("nothing"))),
    ];
  },
};

const viewStock = {
  keys: () => ["stock"],
  render() {
    const s = state.docs.stock;
    if (!s) return [card(null, h("div", { class: "empty" }, t("noData")))];
    return [
      h("div", { class: "kpis" },
        kpi(t("stockValue"), money(s.costValue)),
        kpi(t("retailValue"), money(s.retailValue)),
        kpi(t("units"), number(s.units)),
        kpi(t("outOfStock"), number(s.outOfStockCount), `${t("lowStock")}: ${number(s.lowStockCount)}`)),
      card(t("lowStock"), s.lowStock?.length
        ? table([[t("product")], [t("inStock"), true], [t("reorderAt"), true]],
          s.lowStock.map((r) => h("tr", null,
            td([h("bdi", null, r.product), r.variant ? h("div", { class: "muted", style: "font-size:12px" }, r.variant) : null]),
            td(number(r.stock), true, `num ${r.stock <= 0 ? "bad" : ""}`),
            td(number(r.reorder), true, "num"))))
        : h("div", { class: "empty" }, t("nothing"))),
      card(t("slowMovers", s.slowSinceDays ?? 60), s.slowMovers?.length
        ? table([[t("product")], [t("inStock"), true], [t("value"), true], [t("lastSold"), true]],
          s.slowMovers.map((r) => h("tr", null,
            td([h("bdi", null, r.product), r.variant ? h("div", { class: "muted", style: "font-size:12px" }, r.variant) : null]),
            td(number(r.stock), true, "num"),
            td(money(r.value), true, "num"),
            td(r.lastSold ? dayOf(r.lastSold, true) : t("never"), true, "muted"))))
        : h("div", { class: "empty" }, t("nothing"))),
      card(t("byCategoryStock"), rows(s.byCategory, { meta: qtyMeta })),
    ];
  },
};

// ---- Start --------------------------------------------------------------------------------------------------------

function wireShell() {
  const menu = $("menu");
  $("menu-button").addEventListener("click", (e) => { e.stopPropagation(); menu.classList.toggle("hidden"); });
  document.addEventListener("click", (e) => { if (!menu.contains(e.target)) menu.classList.add("hidden"); });
  $("menu-lang").addEventListener("click", () => { setLanguage(state.lang === "ar" ? "en" : "ar"); menu.classList.add("hidden"); renderShell(); show(); });
  $("menu-refresh").addEventListener("click", () => { menu.classList.add("hidden"); refresh(); });
  $("menu-signout").addEventListener("click", () => { menu.classList.add("hidden"); signOut(); });
  window.addEventListener("hashchange", () => {
    if (!state.me) return;
    readHash();
    renderShell();
    show();
  });
  // New numbers arrive about every 5 minutes; check again when the phone comes back to the page, and every 2 minutes.
  document.addEventListener("visibilitychange", () => { if (document.visibilityState === "visible" && state.me) refresh(); });
  setInterval(() => { if (document.visibilityState === "visible" && state.me) refresh(); }, 120_000);
}

setLanguage(savedLanguage() ?? (navigator.language?.startsWith("ar") ? "ar" : "en"));
wireShell();
start();
