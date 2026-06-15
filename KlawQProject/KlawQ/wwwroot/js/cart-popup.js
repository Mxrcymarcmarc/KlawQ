document.addEventListener("DOMContentLoaded", function () {
  const cartLink = document.querySelector(".cart-button a");
  const cartCountEl = document.getElementById("cartCount");

  // create modal container
  const modal = document.createElement("div");
  modal.id = "cartModal";
  modal.style.cssText =
    "position:fixed;right:20px;top:70px;width:360px;max-height:70vh;background:#fff;border-radius:8px;box-shadow:0 12px 30px rgba(0,0,0,.25);overflow:hidden;display:none;z-index:9999;border:2px solid #fde6ea";
  modal.innerHTML = `
        <div style="padding:16px;border-bottom:1px solid #fde6ea;background:#f8f8f8;font-weight:700;color:#8b4b3b">Shopping cart <span id="cartItemCount" style="float:right;color:#888;font-weight:600"></span></div>
        <div id="cartItems" style="max-height:56vh;overflow-y:auto;padding:12px;display:flex;flex-direction:column;gap:8px"></div>
        <div style="padding:16px;border-top:1px solid #fde6ea;background:#fff;display:flex;align-items:center;justify-content:space-between;border-bottom:1px solid #fde6ea">
            <div style="font-weight:600;color:#8b4b3b">Total</div>
            <div id="cartTotal" style="color:#e2557a;font-weight:700">₱0.00</div>
        </div>
        <div style="padding:12px;background:#fff;text-align:center"><button id="cartCheckout" class="btn" style="background:#88b04b;color:#fff;text-decoration:none;display:inline-block;padding:10px 20px;border:0;border-radius:6px;cursor:pointer;font-weight:600;width:calc(100% - 24px)">Buy</button></div>
    `;
  document.body.appendChild(modal);

  function formatPeso(val) {
    return "₱" + Number(val).toFixed(2);
  }

  function updateTotal() {
    const selected = Array.from(document.querySelectorAll("#cartItems > div")).filter(
      (div) => div.getAttribute("data-selected") === "true"
    );
    const total = selected.reduce((sum, div) => {
      const price = parseFloat(div.getAttribute("data-price")) || 0;
      const qty = parseInt(div.getAttribute("data-qty")) || 1;
      return sum + (price * qty);
    }, 0);
    document.getElementById("cartTotal").textContent = formatPeso(total);
  }

  async function loadCart() {
    try {
      const res = await fetch("/Cart/items");
      if (res.status === 401) {
        window.location.href = "/Account/Login";
        return;
      }
      const json = await res.json();
      const itemsEl = document.getElementById("cartItems");
      itemsEl.innerHTML = "";
      if (!json.items || json.items.length === 0) {
        itemsEl.innerHTML =
          '<div style="padding:16px;text-align:center;color:#666">Your cart is empty.</div>';
      } else {
        json.items.forEach((it) => {
          const itemEl = document.createElement("div");
          itemEl.style.cssText =
            "position:relative;display:flex;gap:12px;align-items:flex-start;padding:12px;border-radius:8px;background:#fbfbfb";
          
          // Set data attributes for tracking
          itemEl.setAttribute("data-productid", it.productId);
          itemEl.setAttribute("data-cartitemid", it.id);
          itemEl.setAttribute("data-price", it.price);
          itemEl.setAttribute("data-qty", it.qty);
          itemEl.setAttribute("data-selected", "true");

          // Image container with checkbox overlay
          const imgContainer = document.createElement("div");
          imgContainer.style.cssText =
            "position:relative;width:96px;height:96px;flex:0 0 96px";

          const img = document.createElement("img");
          img.src = it.image || "/images/placeholder.png";
          img.style.cssText =
            "width:100%;height:100%;object-fit:cover;border-radius:6px;cursor:pointer;display:block";

          // Checkbox on the right side of image
          const checkbox = document.createElement("button");
          checkbox.className = "cart-include-toggle";
          checkbox.style.cssText =
            "position:absolute;bottom:4px;right:4px;width:28px;height:28px;border-radius:4px;border:2px solid #88b04b;background:#fff;display:flex;align-items:center;justify-content:center;cursor:pointer;font-size:18px;padding:0;font-weight:bold;color:#88b04b;transition:all 0.2s ease";
          checkbox.innerHTML = "✓";
          checkbox.style.boxShadow = "0 2px 6px rgba(0,0,0,0.15)";

          checkbox.addEventListener("click", (e) => {
            e.preventDefault();
            e.stopPropagation();
            const isSelected = itemEl.getAttribute("data-selected") === "true";
            itemEl.setAttribute("data-selected", isSelected ? "false" : "true");
            
            if (isSelected) {
              // Unchecked - show hollow box
              checkbox.innerHTML = "";
              checkbox.style.background = "#fff";
            } else {
              // Checked - show checkmark
              checkbox.innerHTML = "✓";
              checkbox.style.background = "#88b04b";
              checkbox.style.color = "#fff";
            }
            updateTotal();
          });

          img.addEventListener("click", () => {
            checkbox.click();
          });

          imgContainer.appendChild(img);
          imgContainer.appendChild(checkbox);

          const meta = document.createElement("div");
          meta.style.cssText = "flex:1;display:flex;flex-direction:column;justify-content:space-between;min-width:0";
          meta.innerHTML = `<div style="font-weight:700;color:#5c2b27;word-break:break-word">${it.name}</div><div style="font-size:13px;color:#666;margin:6px 0">Qty: ${it.qty} • ${formatPeso(it.price)}</div>`;
          
          const remove = document.createElement("button");
          remove.innerHTML = "✖";
          remove.style.cssText =
            "background:transparent;border:0;cursor:pointer;font-size:18px;color:#999;padding:0;width:24px;height:24px;flex:0 0 24px;display:flex;align-items:center;justify-content:center";
          remove.addEventListener("click", async () => {
            const tokenEl = document.querySelector(
              'input[name="__RequestVerificationToken"]',
            );
            const token = tokenEl ? tokenEl.value : null;
            await fetch(`/Cart/remove/${it.id}`, {
              method: "POST",
              headers: token ? { RequestVerificationToken: token } : {},
            });
            await refresh();
          });
          
          itemEl.appendChild(imgContainer);
          itemEl.appendChild(meta);
          itemEl.appendChild(remove);
          itemsEl.appendChild(itemEl);
        });
        document.getElementById("cartItemCount").textContent =
          `${json.count} items`;
        document.getElementById("cartCount").textContent = json.count;
        updateTotal();
      }
    } catch (e) {
      console.error(e);
    }
  }

  async function refresh() {
    await loadCart();
  }

  cartLink.addEventListener("click", function (e) {
    e.preventDefault();
    if (modal.style.display === "none") {
      modal.style.display = "block";
      loadCart();
    } else modal.style.display = "none";
  });

  // Checkout action: collect selected productIds and redirect to order start
  document.addEventListener("click", function (e) {
    if (e.target && e.target.id === "cartCheckout") {
      e.preventDefault();
      const selected = Array.from(
        document.querySelectorAll("#cartItems > div"),
      ).filter(
        (div) => div.getAttribute("data-selected") === "true"
      );
      const ids = selected
        .map((div) => div.getAttribute("data-productid"))
        .filter((x) => x)
        .join("&productIds=");
      if (!ids) {
        alert("Please select at least one item to purchase.");
        return;
      }
      // redirect to Order Start with selected ids
      const url = `/Order/Start?productIds=${ids}`;
      window.location.href = url;
    }
  });

  // expose refresh globally to allow updating count
  window.refreshCart = refresh;
});
