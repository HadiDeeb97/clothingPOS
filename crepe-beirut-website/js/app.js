(function () {
  "use strict";

  var CFG = window.CB_CONFIG;
  var MENU = window.CB_MENU;
  var STORE_KEY = "crepe-beirut-order-v1";
  var CUSTOMER_KEY = "crepe-beirut-customer-v1";

  var $ = function (sel, root) { return (root || document).querySelector(sel); };
  var $$ = function (sel, root) { return Array.prototype.slice.call((root || document).querySelectorAll(sel)); };

  function slug(s) { return s.toLowerCase().replace(/&/g, "and").replace(/[^a-z0-9]+/g, "-").replace(/^-|-$/g, ""); }
  function money(n) { return n.toLocaleString("en-US") + " L.L."; }
  function esc(s) {
    return String(s).replace(/[&<>"']/g, function (c) {
      return { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c];
    });
  }
  function load(key, fallback) {
    try { var v = localStorage.getItem(key); return v ? JSON.parse(v) : fallback; } catch (e) { return fallback; }
  }
  function save(key, value) {
    try { localStorage.setItem(key, JSON.stringify(value)); } catch (e) { /* storage unavailable */ }
  }

  /* ---------- Index the menu ---------- */
  var items = {};
  var extras = {};
  var itemCount = 0;
  MENU.categories.forEach(function (cat) {
    cat.items.forEach(function (it) {
      it.id = cat.id + "/" + slug(it.name);
      it.cat = cat;
      items[it.id] = it;
      itemCount += 1;
    });
  });
  MENU.extras.forEach(function (g) {
    g.items.forEach(function (x) { x.id = slug(x.name); extras[x.id] = x; });
  });

  function needsSheet(it) { return !!(it.options && it.options.length > 1) || !!it.cat.extras || !!it.cat.choice; }
  function basePrice(it, optLabel) {
    if (!it.options) return it.price;
    var o = it.options.filter(function (o) { return o.label === optLabel; })[0] || it.options[0];
    return o.price;
  }
  function unitPrice(line) {
    var it = items[line.itemId];
    if (!it) return 0;
    return basePrice(it, line.option) + (line.extras || []).reduce(function (s, id) { return s + (extras[id] ? extras[id].price : 0); }, 0);
  }

  /* ---------- Cart state ---------- */
  var cart = load(STORE_KEY, []).filter(function (l) { return l && items[l.itemId] && l.qty > 0; });
  var sentMessage = "";

  function lineKey(l) { return [l.itemId, l.option || "", l.choice || "", (l.extras || []).slice().sort().join("+"), l.note || ""].join("|"); }
  function addLine(line) {
    var key = lineKey(line);
    var found = cart.filter(function (l) { return lineKey(l) === key; })[0];
    if (found) found.qty += line.qty; else cart.push(line);
    commit();
    toast("Added " + line.qty + " × " + items[line.itemId].name);
    $$("[data-count]").forEach(function (el) { el.classList.remove("bump"); void el.offsetWidth; el.classList.add("bump"); });
  }
  function totals() {
    var sub = cart.reduce(function (s, l) { return s + unitPrice(l) * l.qty; }, 0);
    var delivery = orderType() === "Delivery" && cart.length ? (CFG.deliveryFee || 0) : 0;
    return { sub: sub, fee: delivery, total: sub + delivery, count: cart.reduce(function (s, l) { return s + l.qty; }, 0) };
  }
  function commit() { save(STORE_KEY, cart); renderCart(); renderBadges(); }

  /* ---------- Menu rendering ---------- */
  var plus = '<svg class="ico" aria-hidden="true"><use href="#i-plus"></use></svg>';
  function renderMenu() {
    var html = MENU.categories.map(function (cat) {
      var pic = cat.img
        ? '<div class="cat-pic"><img src="assets/img/' + cat.img + '.webp" alt=""></div>'
        : '<div class="cat-pic badge"><img src="assets/logo-badge.svg" alt=""></div>';
      var rows = cat.items.map(function (it) {
        var price, sizes = "";
        if (it.options && it.options.length > 1) {
          price = it.options.map(function (o) { return "<span><small>" + esc(o.label) + "</small> " + money(o.price) + "</span>"; }).join("");
        } else if (it.options) {
          sizes = '<span class="item-sizes">' + esc(it.options[0].label) + "</span>";
          price = money(it.options[0].price);
        } else {
          price = money(it.price);
        }
        return '<li class="item" data-item="' + it.id + '" data-search="' + esc((it.name + " " + cat.name).toLowerCase()) + '">' +
          '<span class="item-name">' + esc(it.name) + sizes + "</span>" +
          '<span class="item-price">' + price + "</span>" +
          '<button class="add" type="button" data-add="' + it.id + '" aria-label="Add ' + esc(it.name) + '">' + plus + "</button>" +
          "</li>";
      }).join("");
      var note = cat.extras ? '<p class="extras-note">Tap + to choose extras: Nutella, Lotus, Oreo, strawberry, ice cream and more.</p>' : "";
      return '<section class="cat" id="' + cat.id + '" data-cat="' + cat.id + '" aria-labelledby="h-' + cat.id + '">' +
        '<div class="cat-head">' + pic + "<div><h3 id=\"h-" + cat.id + '">' + esc(cat.name) + "</h3>" +
        (cat.blurb ? "<p>" + esc(cat.blurb) + "</p>" : "") + "</div></div>" +
        '<ul class="items">' + rows + "</ul>" + note + "</section>";
    }).join("");
    $("[data-menu]").innerHTML = html;
    $("[data-chips]").innerHTML = MENU.categories.map(function (c) {
      return '<a href="#' + c.id + '" data-chip="' + c.id + '">' + esc(c.name) + "</a>";
    }).join("");
  }

  function renderBadges() {
    var per = {};
    cart.forEach(function (l) { per[l.itemId] = (per[l.itemId] || 0) + l.qty; });
    $$("[data-item]").forEach(function (row) {
      var n = per[row.getAttribute("data-item")] || 0;
      row.classList.toggle("in", n > 0);
      var btn = $(".add", row);
      var badge = $(".n", btn);
      if (n > 0) {
        if (!badge) { badge = document.createElement("span"); badge.className = "n"; btn.appendChild(badge); }
        badge.textContent = n;
      } else if (badge) {
        badge.remove();
      }
    });
  }

  /* ---------- Search ---------- */
  var searchInput = $("#menu-search");
  searchInput.addEventListener("input", function () {
    var q = searchInput.value.trim().toLowerCase();
    var anyShown = false;
    $$("[data-cat]").forEach(function (sec) {
      var shown = 0;
      $$("[data-item]", sec).forEach(function (row) {
        var hit = !q || row.getAttribute("data-search").indexOf(q) !== -1;
        row.hidden = !hit;
        if (hit) shown += 1;
      });
      sec.hidden = shown === 0;
      $$('[data-chip="' + sec.id + '"]').forEach(function (c) { c.hidden = shown === 0; });
      if (shown) anyShown = true;
    });
    var none = $("[data-no-results]");
    none.hidden = anyShown;
    none.textContent = anyShown ? "" : "Nothing on the menu matches “" + searchInput.value.trim() + "”. Try another word.";
  });

  /* ---------- Customize sheet ---------- */
  var sheet = $("[data-sheet]");
  var sheetItem = null;
  var sheetQty = 1;

  function openSheet(it) {
    sheetItem = it;
    sheetQty = 1;
    $("[data-sheet-cat]").textContent = it.cat.name;
    $("[data-sheet-title]").textContent = it.name;
    var body = "";
    if (it.options) {
      body += '<fieldset class="opt-group"><legend>' + (it.cat.id === "mini-pancakes" ? "Box size" : "Size") + '</legend><div class="opts">' +
        it.options.map(function (o, i) {
          return '<label class="opt"><input type="radio" name="option" value="' + esc(o.label) + '"' + (i === 0 ? " checked" : "") + '>' +
            "<span>" + esc(o.label) + " <small>" + money(o.price) + "</small></span></label>";
        }).join("") + "</div></fieldset>";
    }
    if (it.cat.choice) {
      body += '<fieldset class="opt-group"><legend>' + esc(it.cat.choice.label) + '</legend><div class="opts">' +
        it.cat.choice.values.map(function (v, i) {
          return '<label class="opt"><input type="radio" name="choice" value="' + esc(v) + '"' + (i === 0 ? " checked" : "") + "><span>" + esc(v) + "</span></label>";
        }).join("") + "</div></fieldset>";
    }
    if (it.cat.extras) {
      body += MENU.extras.map(function (g) {
        return '<fieldset class="opt-group"><legend>' + esc(g.group) + (g === MENU.extras[0] ? ' <span class="hint">· optional</span>' : "") + '</legend><div class="opts">' +
          g.items.map(function (x) {
            return '<label class="opt"><input type="checkbox" name="extra" value="' + x.id + '"><span>' + esc(x.name) +
              " <small>+" + x.price.toLocaleString("en-US") + "</small></span></label>";
          }).join("") + "</div></fieldset>";
      }).join("");
    }
    body += '<div class="field"><label for="sheet-note">Note for this item <span class="muted">(optional)</span></label>' +
      '<input id="sheet-note" name="note" maxlength="120" placeholder="e.g. cut in half, less sauce"></div>';
    $("[data-sheet-body]").innerHTML = body;
    $("[data-sheet-body]").scrollTop = 0;
    updateSheet();
    if (typeof sheet.showModal === "function") sheet.showModal(); else sheet.setAttribute("open", "");
  }
  function sheetLine() {
    var form = $("[data-sheet-form]");
    var opt = $('input[name="option"]:checked', form);
    var ch = $('input[name="choice"]:checked', form);
    return {
      itemId: sheetItem.id,
      option: opt ? opt.value : (sheetItem.options ? sheetItem.options[0].label : ""),
      choice: ch ? ch.value : "",
      extras: $$('input[name="extra"]:checked', form).map(function (i) { return i.value; }),
      note: ($("#sheet-note") || { value: "" }).value.trim(),
      qty: sheetQty
    };
  }
  function updateSheet() {
    var l = sheetLine();
    $("[data-sheet-qty]").textContent = sheetQty;
    $("[data-sheet-add]").textContent = "Add · " + money(unitPrice(l) * sheetQty);
  }
  function closeSheet() { if (sheet.open) sheet.close(); }

  $("[data-sheet-body]").addEventListener("change", updateSheet);
  $("[data-sheet-minus]").addEventListener("click", function () { sheetQty = Math.max(1, sheetQty - 1); updateSheet(); });
  $("[data-sheet-plus]").addEventListener("click", function () { sheetQty = Math.min(50, sheetQty + 1); updateSheet(); });
  $("[data-sheet-close]").addEventListener("click", closeSheet);
  sheet.addEventListener("click", function (e) { if (e.target === sheet) closeSheet(); });
  $("[data-sheet-form]").addEventListener("submit", function (e) {
    e.preventDefault();
    if (sheetItem) addLine(sheetLine());
    closeSheet();
  });

  document.addEventListener("click", function (e) {
    var btn = e.target.closest("[data-add]");
    if (!btn) return;
    var it = items[btn.getAttribute("data-add")];
    if (!it) return;
    if (needsSheet(it)) openSheet(it);
    else addLine({ itemId: it.id, option: it.options ? it.options[0].label : "", choice: "", extras: [], note: "", qty: 1 });
  });

  /* ---------- Cart rendering ---------- */
  var form = $("[data-checkout]");
  function orderType() { var r = $('input[name="type"]:checked', form); return r ? r.value : "Delivery"; }

  function lineMeta(l) {
    var bits = [];
    if (l.option) bits.push(l.option);
    if (l.choice) bits.push(l.choice);
    if (l.extras && l.extras.length) bits.push("+ " + l.extras.map(function (id) { return extras[id] ? extras[id].name : id; }).join(", "));
    if (l.note) bits.push("“" + l.note + "”");
    return bits.join(" · ");
  }

  function renderCart() {
    var t = totals();
    var empty = cart.length === 0;
    var showingSent = !$("[data-sent]").hidden;
    $("[data-cart-empty]").hidden = !empty || showingSent;
    $("[data-totals]").hidden = empty || showingSent;
    form.hidden = empty || showingSent;
    $("[data-lines]").hidden = showingSent;

    $("[data-lines]").innerHTML = cart.map(function (l, i) {
      var it = items[l.itemId];
      var meta = lineMeta(l);
      return '<li class="line">' +
        '<span class="line-name">' + esc(it.name) + "</span>" +
        '<span class="line-total">' + money(unitPrice(l) * l.qty) + "</span>" +
        (meta ? '<span class="line-meta">' + esc(meta) + "</span>" : "") +
        '<div class="line-ctrl">' +
        '<div class="stepper" role="group" aria-label="Quantity of ' + esc(it.name) + '">' +
        '<button type="button" data-dec="' + i + '" aria-label="One less">−</button>' +
        "<output>" + l.qty + "</output>" +
        '<button type="button" data-inc="' + i + '" aria-label="One more">+</button></div>' +
        '<button class="line-remove" type="button" data-remove="' + i + '">Remove</button>' +
        "</div></li>";
    }).join("");

    $("[data-subtotal]").textContent = money(t.sub);
    $("[data-fee-row]").hidden = !t.fee;
    $("[data-fee]").textContent = money(t.fee);
    $("[data-total]").textContent = money(t.total);
    var usd = $("[data-usd]");
    if (CFG.usdRate > 0 && t.total) { usd.hidden = false; usd.textContent = "≈ $" + (t.total / CFG.usdRate).toFixed(2); }
    else if (!CFG.deliveryFee && orderType() === "Delivery" && t.total) { usd.hidden = false; usd.textContent = "Delivery fee, if any, is confirmed on WhatsApp."; }
    else usd.hidden = true;

    $$("[data-count]").forEach(function (el) { el.textContent = t.count; });
    $("[data-mobile-total]").textContent = money(t.total);
    $("[data-mobile-bar]").hidden = empty;
  }

  $("[data-lines]").addEventListener("click", function (e) {
    var b = e.target.closest("button");
    if (!b) return;
    var i;
    if (b.hasAttribute("data-inc")) { i = +b.getAttribute("data-inc"); cart[i].qty = Math.min(50, cart[i].qty + 1); }
    else if (b.hasAttribute("data-dec")) { i = +b.getAttribute("data-dec"); cart[i].qty -= 1; if (cart[i].qty < 1) cart.splice(i, 1); }
    else if (b.hasAttribute("data-remove")) { i = +b.getAttribute("data-remove"); cart.splice(i, 1); }
    else return;
    commit();
  });

  /* ---------- Checkout ---------- */
  var customer = load(CUSTOMER_KEY, {});
  ["name", "phone", "address"].forEach(function (k) { if (customer[k]) form.elements[k].value = customer[k]; });
  if (customer.type === "Pickup") $("#type-pickup").checked = true;

  function syncType() {
    var delivery = orderType() === "Delivery";
    $("[data-address-field]").hidden = !delivery;
    renderCart();
  }
  form.addEventListener("change", function (e) { if (e.target.name === "type") syncType(); });
  form.addEventListener("input", function (e) {
    var f = e.target.closest(".field");
    if (f) f.classList.remove("bad");
  });

  function buildMessage(c) {
    var t = totals();
    var out = [];
    out.push("*New order – " + CFG.shopName + "*");
    out.push("");
    out.push("*Name:* " + c.name);
    out.push("*Phone:* " + c.phone);
    out.push("*Order type:* " + c.type);
    if (c.type === "Delivery") out.push("*Address:* " + c.address);
    out.push("");
    cart.forEach(function (l, i) {
      var it = items[l.itemId];
      out.push((i + 1) + ". " + l.qty + " × " + it.name + (l.option ? " (" + l.option + ")" : "") + (l.choice ? " – " + l.choice : "") + " — " + money(unitPrice(l) * l.qty));
      if (l.extras && l.extras.length) out.push("    + " + l.extras.map(function (id) { return extras[id].name; }).join(", "));
      if (l.note) out.push("    Note: " + l.note);
    });
    out.push("");
    if (t.fee) {
      out.push("Subtotal: " + money(t.sub));
      out.push("Delivery: " + money(t.fee));
    }
    out.push("*Total: " + money(t.total) + "*");
    if (c.notes) { out.push(""); out.push("*Notes:* " + c.notes); }
    return out.join("\n");
  }
  function waUrl(text) { return "https://wa.me/" + CFG.whatsappNumber + "?text=" + encodeURIComponent(text); }

  form.addEventListener("submit", function (e) {
    e.preventDefault();
    var c = {
      type: orderType(),
      name: form.elements.name.value.trim(),
      phone: form.elements.phone.value.trim(),
      address: form.elements.address.value.trim(),
      notes: form.elements.notes.value.trim()
    };
    var missing = [];
    $$(".field", form).forEach(function (f) { f.classList.remove("bad"); });
    if (!c.name) { missing.push("your name"); form.elements.name.closest(".field").classList.add("bad"); }
    if (c.phone.replace(/\D/g, "").length < 7) { missing.push("a phone number we can call"); form.elements.phone.closest(".field").classList.add("bad"); }
    if (c.type === "Delivery" && !c.address) { missing.push("a delivery address"); form.elements.address.closest(".field").classList.add("bad"); }
    var err = $("[data-form-error]");
    if (missing.length) {
      err.hidden = false;
      err.textContent = "Please add " + missing.join(", ") + ".";
      var firstBad = $(".field.bad input, .field.bad textarea", form);
      if (firstBad) firstBad.focus();
      return;
    }
    err.hidden = true;
    save(CUSTOMER_KEY, { name: c.name, phone: c.phone, address: c.address, type: c.type });

    sentMessage = buildMessage(c);
    var url = waUrl(sentMessage);
    $("[data-sent-link]").href = url;
    $("[data-sent-text]").value = sentMessage;
    $("[data-sent]").hidden = false;
    renderCart();
    $("[data-ticket] .ticket-inner").scrollTop = 0;
    var w = null;
    try { w = window.open(url, "_blank", "noopener"); } catch (e2) { w = null; }
    if (!w) $("[data-sent-link]").focus();
  });

  $("[data-edit]").addEventListener("click", function () { $("[data-sent]").hidden = true; renderCart(); });
  $("[data-new-order]").addEventListener("click", function () {
    cart = [];
    form.elements.notes.value = "";
    $("[data-sent]").hidden = true;
    commit();
    toast("Order cleared");
  });
  $("[data-copy]").addEventListener("click", function () {
    var ta = $("[data-sent-text]");
    var done = function () { toast("Order copied"); };
    var fallback = function () { ta.focus(); ta.select(); toast("Text selected. Copy it with your keyboard or long-press."); };
    if (navigator.clipboard && navigator.clipboard.writeText) navigator.clipboard.writeText(sentMessage).then(done, fallback);
    else fallback();
  });

  /* ---------- Mobile order sheet ---------- */
  var ticket = $("[data-ticket]");
  var scrim = $("[data-scrim]");
  var mq = window.matchMedia("(max-width: 999px)");
  function openOrder() {
    if (mq.matches) {
      ticket.classList.add("open");
      scrim.hidden = false;
      document.body.style.overflow = "hidden";
      var c = $("[data-close-order]"); if (c) c.focus();
    } else {
      ticket.scrollIntoView({ behavior: "smooth", block: "start" });
    }
  }
  function closeOrder() {
    ticket.classList.remove("open");
    scrim.hidden = true;
    document.body.style.overflow = "";
  }
  $$("[data-open-order]").forEach(function (b) { b.addEventListener("click", openOrder); });
  $("[data-close-order]").addEventListener("click", closeOrder);
  scrim.addEventListener("click", closeOrder);
  document.addEventListener("keydown", function (e) { if (e.key === "Escape" && ticket.classList.contains("open")) closeOrder(); });
  (mq.addEventListener ? mq.addEventListener.bind(mq, "change") : mq.addListener.bind(mq))(function () { if (!mq.matches) closeOrder(); });

  /* ---------- Chips follow the scroll ---------- */
  function watchSections() {
    if (!("IntersectionObserver" in window)) return;
    var io = new IntersectionObserver(function (entries) {
      entries.forEach(function (en) {
        if (!en.isIntersecting) return;
        $$("[data-chip]").forEach(function (c) { c.classList.toggle("on", c.getAttribute("data-chip") === en.target.id); });
        var on = $('[data-chip="' + en.target.id + '"]');
        var bar = $("[data-chips]");
        if (on && bar) bar.scrollTo({ left: on.offsetLeft - bar.clientWidth / 2 + on.clientWidth / 2, behavior: "smooth" });
      });
    }, { rootMargin: "-30% 0px -65% 0px" });
    $$("[data-cat]").forEach(function (s) { io.observe(s); });
  }

  /* ---------- Toast ---------- */
  var toastEl = $("[data-toast]");
  var toastTimer;
  function toast(msg) {
    toastEl.textContent = msg;
    toastEl.classList.add("show");
    clearTimeout(toastTimer);
    toastTimer = setTimeout(function () { toastEl.classList.remove("show"); }, 1800);
  }

  /* ---------- Shop details from config ---------- */
  function applyConfig() {
    $$("[data-wa-display]").forEach(function (el) { el.textContent = CFG.whatsappDisplay; });
    $$("[data-wa-link]").forEach(function (el) { el.href = "https://wa.me/" + CFG.whatsappNumber; });
    ["address", "hours"].forEach(function (k) {
      if (!CFG[k]) return;
      $('[data-cfg-row="' + k + '"]').hidden = false;
      var dd = $('[data-cfg="' + k + '"]');
      if (k === "address" && CFG.mapsUrl) dd.innerHTML = '<a href="' + esc(CFG.mapsUrl) + '" target="_blank" rel="noopener">' + esc(CFG.address) + "</a>";
      else dd.textContent = CFG[k];
    });
    var socials = [["instagram", "Instagram", "i-ig"], ["facebook", "Facebook", "i-fb"], ["tiktok", "TikTok", "i-tt"]]
      .filter(function (s) { return CFG[s[0]]; })
      .map(function (s) {
        return '<a href="' + esc(CFG[s[0]]) + '" target="_blank" rel="noopener"><svg class="ico" aria-hidden="true"><use href="#' + s[2] + '"></use></svg>' + s[1] + "</a>";
      });
    if (socials.length) { $("[data-social-row]").hidden = false; $("[data-socials]").innerHTML = socials.join(""); }
    $("[data-stat-items]").textContent = itemCount;
    $("[data-year]").textContent = new Date().getFullYear();
  }

  renderMenu();
  applyConfig();
  syncType();
  renderBadges();
  watchSections();
})();
