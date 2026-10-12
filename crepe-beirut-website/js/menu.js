/*
 * Crepe Beirut menu.
 * Prices are in Lebanese pounds (L.L.), copied from the printed menu.
 * To change a price, edit the number here. To hide an item, delete its line.
 *
 * Item shapes:
 *   { name, price }                      one fixed price
 *   { name, options: [{ label, price }] } the customer picks a size
 * Category flags:
 *   extras: true   the customer can add toppings from EXTRAS below
 *   choice         an extra pick that does not change the price (e.g. Hot / Iced)
 */
window.CB_MENU = {
  categories: [
    {
      id: "crepes", name: "Crepes", img: "crepe", extras: true,
      blurb: "Folded to order on the hot plate.",
      items: [
        { name: "Nutella Crepe", price: 350000 },
        { name: "White Crepe", price: 350000 },
        { name: "Lotus Crepe", price: 350000 },
        { name: "Kinder Crepe", options: [
          { label: "6 fingers", price: 450000 },
          { label: "8 fingers", price: 500000 }
        ] },
        { name: "Dark Crepe", price: 350000 },
        { name: "Pistachio Crepe", price: 500000 },
        { name: "Dubai Crepe", price: 600000 },
        { name: "Belgian Crepe", price: 550000 },
        { name: "Bueno Crepe", price: 450000 },
        { name: "Triple Choc. Crepe", price: 550000 },
        { name: "Trio Crepe", price: 500000 },
        { name: "Cinnamon Crepe", price: 550000 },
        { name: "Oreo Crepe", price: 550000 },
        { name: "Brownies Crepe", price: 550000 }
      ]
    },
    {
      id: "mini-pancakes", name: "Mini Pancakes", img: "pancakes", extras: true,
      blurb: "By the box: 12 or 25 pieces.",
      items: [
        { name: "Mini Pancakes White", options: [{ label: "12 pcs", price: 300000 }, { label: "25 pcs", price: 500000 }] },
        { name: "Mini Pancakes Lotus", options: [{ label: "12 pcs", price: 300000 }, { label: "25 pcs", price: 500000 }] },
        { name: "Mini Pancakes Nutella", options: [{ label: "12 pcs", price: 350000 }, { label: "25 pcs", price: 550000 }] },
        { name: "Mini Pancakes Pistachio", options: [{ label: "12 pcs", price: 400000 }, { label: "25 pcs", price: 600000 }] },
        { name: "Mini Pancakes Belgian & White", options: [{ label: "12 pcs", price: 350000 }, { label: "25 pcs", price: 550000 }] },
        { name: "Mini Pancakes Special", options: [{ label: "12 pcs", price: 450000 }, { label: "25 pcs", price: 650000 }] },
        { name: "Mix 4 Flavors", options: [{ label: "12 pcs", price: 350000 }] },
        { name: "Mix 5 Flavors", options: [{ label: "25 pcs", price: 550000 }] },
        { name: "Mix 5 Flavors + Fruits", options: [{ label: "25 pcs", price: 650000 }] }
      ]
    },
    {
      id: "waffles", name: "Waffles", img: "waffle", extras: true,
      items: [
        { name: "White Waffle", price: 300000 },
        { name: "Lotus Waffle", price: 350000 },
        { name: "Nutella Waffle", price: 350000 },
        { name: "Belgian & White Waffle", price: 350000 },
        { name: "Pistachio Waffle", price: 400000 },
        { name: "Special Waffle", price: 450000 }
      ]
    },
    {
      id: "specials", name: "Specials", img: "spreader", extras: true,
      items: [
        { name: "Fettuccine", price: 550000 },
        { name: "Banana Roll", price: 500000 },
        { name: "Brownies Roll", price: 500000 },
        { name: "Belgian Roll", price: 500000 },
        { name: "Pancakes", price: 500000 },
        { name: "Crazy Pancakes", price: 550000 }
      ]
    },
    {
      id: "cups", name: "Cups", img: "iced-cup",
      items: [
        { name: "Dubai Cup", price: 600000 },
        { name: "Brownies Cup", price: 400000 },
        { name: "Lotus Cup", price: 400000 },
        { name: "Oreo Cup", price: 400000 },
        { name: "Red Cup", price: 400000 },
        { name: "Lotus Brownies", price: 400000 },
        { name: "Strawberry Cup", price: 350000 }
      ]
    },
    {
      id: "iced-cups", name: "Iced Cups", img: "iced-cup",
      items: [
        { name: "Iced Brownies Cup", price: 400000 },
        { name: "Iced Lotus Cup", price: 400000 },
        { name: "Iced Crunchy Cup", price: 400000 },
        { name: "Ghazle Cup", price: 400000 },
        { name: "Chocola Mou", price: 500000 },
        { name: "Kinder Mou", price: 550000 },
        { name: "Sweet Box", price: 600000 }
      ]
    },
    {
      id: "cheesecake", name: "Cheesecake",
      items: [
        { name: "Plain Cheesecake", price: 225000 },
        { name: "Nutella Cheesecake", price: 300000 },
        { name: "Lotus Cheesecake", price: 300000 },
        { name: "Kinder Cheesecake", price: 315000 },
        { name: "Bueno Cheesecake", price: 325000 },
        { name: "Oreo Cheesecake", price: 325000 },
        { name: "Strawberry Cheesecake", price: 300000 },
        { name: "Mango Cheesecake", price: 300000 },
        { name: "Blueberry Cheesecake", price: 300000 },
        { name: "San Sebastian", price: 300000 }
      ]
    },
    {
      id: "tiramisu", name: "Tiramisu", img: "tiramisu",
      items: [
        { name: "Classic Tiramisu", price: 500000 },
        { name: "Lotus Tiramisu", price: 500000 },
        { name: "Oreo Tiramisu", price: 500000 },
        { name: "Kinder Tiramisu", price: 500000 },
        { name: "Zamel Tiramisu", price: 500000 }
      ]
    },
    {
      id: "cakes", name: "Cakes",
      items: [
        { name: "Lazy Cake", price: 225000 },
        { name: "Fudge Cake", price: 225000 },
        { name: "Fudge Nutella Cake", price: 225000 },
        { name: "Cinnamon Tart", price: 200000 },
        { name: "Fondant + Ice Cream", price: 450000 }
      ]
    },
    {
      id: "shakes", name: "Shakes", img: "shake",
      items: [
        { name: "Nutella Shake", price: 400000 },
        { name: "Strawberry Shake", price: 350000 },
        { name: "Caramel Shake", price: 350000 },
        { name: "Kinder Shake", price: 400000 },
        { name: "Oreo Shake", price: 400000 },
        { name: "Brownies Shake", price: 400000 },
        { name: "Snickers Shake", price: 400000 },
        { name: "Lotus Shake", price: 400000 },
        { name: "Bueno Shake", price: 400000 },
        { name: "Pistachio Shake", price: 450000 }
      ]
    },
    {
      id: "frappe", name: "Frappe", img: "latte",
      items: [
        { name: "Classic Frappe", price: 350000 },
        { name: "Coffee Frappe", price: 400000 },
        { name: "Caramel Frappe", price: 350000 },
        { name: "Mocha Frappe", price: 350000 },
        { name: "Vanilla Frappe", price: 350000 },
        // The printed menu lists 50,000 L.L. here; check whether it should be 350,000.
        { name: "Hazelnut Frappe", price: 50000 },
        { name: "Toffee Frappe", price: 350000 },
        { name: "Vanilla Sugar Free Frappe", price: 350000 },
        { name: "Caramel Sugar Free Frappe", price: 350000 }
      ]
    },
    {
      id: "smoothies", name: "Smoothies", img: "smoothies",
      items: [
        { name: "Mango Smoothie", price: 350000 },
        { name: "Strawberry Smoothie", price: 350000 },
        { name: "Peach Smoothie", price: 350000 },
        { name: "Fruits Smoothie", price: 350000 },
        { name: "Passion Smoothie", price: 350000 },
        { name: "Blueberry Smoothie", price: 350000 },
        { name: "Tropical Smoothie", price: 350000 }
      ]
    },
    {
      id: "matcha", name: "Matcha", img: "leaf",
      items: [
        { name: "Classic Matcha", price: 350000 },
        { name: "Caramel Matcha", price: 350000 },
        { name: "Vanilla Matcha", price: 350000 },
        { name: "Hazelnut Matcha", price: 350000 },
        { name: "Strawberry Matcha", price: 350000 },
        { name: "Blueberry Matcha", price: 350000 },
        { name: "Mango Matcha", price: 350000 },
        { name: "Vanilla Sugar Free Matcha", price: 350000 },
        { name: "Caramel Sugar Free Matcha", price: 350000 }
      ]
    },
    {
      id: "latte", name: "Latte", img: "latte",
      blurb: "Served hot or iced.",
      choice: { label: "Serve it", values: ["Hot", "Iced"] },
      items: [
        { name: "Classic Latte", price: 350000 },
        { name: "Spanish Latte", price: 350000 },
        { name: "Caramel Latte", price: 350000 },
        { name: "Vanilla Latte", price: 350000 },
        { name: "Hazelnut Latte", price: 350000 },
        { name: "Toffee Latte", price: 350000 },
        { name: "Oreo Latte", price: 400000 },
        { name: "Lotus Latte", price: 400000 },
        { name: "Nutella Latte", price: 400000 },
        { name: "Vanilla Sugar Free Latte", price: 350000 },
        { name: "Caramel Sugar Free Latte", price: 350000 }
      ]
    },
    {
      id: "macchiato", name: "Macchiato", img: "latte",
      items: [
        { name: "Caramel Macchiato", price: 375000 },
        { name: "Mocha Macchiato", price: 375000 }
      ]
    },
    {
      id: "mojito", name: "Mojito", img: "mojito",
      items: [
        { name: "Classic Mojito", price: 350000 },
        { name: "Strawberry Mojito", price: 350000 },
        { name: "Passion Mojito", price: 350000 },
        { name: "Peach Mojito", price: 350000 },
        { name: "Mango Mojito", price: 350000 },
        { name: "Tropical Mojito", price: 350000 },
        { name: "Blueberry Mojito", price: 350000 },
        { name: "Blue Curacao Mojito", price: 350000 },
        { name: "Power Mojito", price: 350000 }
      ]
    },
    {
      id: "iced-tea", name: "Iced Tea",
      items: [
        { name: "Iced Tea Peach", price: 200000 },
        { name: "Iced Tea Mango", price: 200000 },
        { name: "Iced Tea Raspberry", price: 200000 }
      ]
    },
    {
      id: "beverages", name: "Beverages",
      items: [
        { name: "Water", price: 30000 },
        { name: "Pepsi", price: 80000 },
        { name: "Miranda", price: 80000 },
        { name: "7UP", price: 80000 },
        { name: "Lipton Ice Tea", price: 80000 },
        { name: "Milkshake", price: 70000 },
        { name: "Makkaw Juice", price: 70000 },
        { name: "Rim Sparkling Water", price: 70000 },
        { name: "Dark Blue", price: 80000 }
      ]
    }
  ],

  // Add-ons from the printed menu, offered on categories with `extras: true`.
  extras: [
    { group: "Spreads", items: [
      { name: "Nutella", price: 100000 },
      { name: "Lotus", price: 100000 },
      { name: "Kinder", price: 120000 },
      { name: "White", price: 100000 },
      { name: "Dark", price: 100000 },
      { name: "Belgian Chocolate", price: 150000 },
      { name: "Pistachio", price: 150000 },
      { name: "Peanut Butter", price: 100000 }
    ] },
    { group: "Crunch & candy", items: [
      { name: "Oreo", price: 60000 },
      { name: "Brownies", price: 60000 },
      { name: "Lotus Biscuits", price: 50000 },
      { name: "Hersheys", price: 120000 },
      { name: "Snickers", price: 90000 },
      { name: "Galaxy", price: 100000 },
      { name: "Kinder Bueno", price: 135000 },
      { name: "Digestive", price: 90000 },
      { name: "Nuts", price: 90000 },
      { name: "Fustuq Halabi", price: 100000 },
      { name: "Marshmallow", price: 100000 },
      { name: "Smarties", price: 70000 },
      { name: "Gelleys", price: 70000 },
      { name: "Flakes", price: 50000 }
    ] },
    { group: "Fruit", items: [
      { name: "Strawberry", price: 100000 },
      { name: "Banana", price: 50000 },
      { name: "Ananas", price: 90000 }
    ] },
    { group: "Ice cream & syrups", items: [
      { name: "Scoop Vanilla", price: 90000 },
      { name: "Scoop Chocolate", price: 90000 },
      { name: "Dark Syrup", price: 20000 },
      { name: "Strawberry Syrup", price: 20000 },
      { name: "Caramel Syrup", price: 20000 }
    ] }
  ]
};
