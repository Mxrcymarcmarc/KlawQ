document.addEventListener("DOMContentLoaded", function () {
  const favoriteButtons = document.querySelectorAll(".favorite-btn");

  const tokenEl = document.querySelector(
    'input[name="__RequestVerificationToken"]',
  );
  const token = tokenEl ? tokenEl.value : null;

  favoriteButtons.forEach((button) => {
    button.addEventListener("click", function (e) {
      e.preventDefault();

      const btn = this;
      const itemId = btn.getAttribute("data-id");
      if (!itemId) return;

      const wasFavorited = btn.classList.contains("favorited");
      // optimistic UI toggle
      btn.classList.toggle("favorited");

      fetch(`/Gallery/toggle-favorite/${itemId}`, {
        method: "POST",
        headers: token ? { RequestVerificationToken: token } : {},
      })
        .then((res) => res.json())
        .then((data) => {
          if (!data || !data.success) {
            // revert UI on failure
            if (wasFavorited) btn.classList.add("favorited");
            else btn.classList.remove("favorited");
            console.error("Failed to toggle favorite", data);
          } else {
            // ensure UI matches server state
            if (data.favorited) btn.classList.add("favorited");
            else btn.classList.remove("favorited");
          }
        })
        .catch((err) => {
          if (wasFavorited) btn.classList.add("favorited");
          else btn.classList.remove("favorited");
          console.error(err);
        });
    });
  });
});
