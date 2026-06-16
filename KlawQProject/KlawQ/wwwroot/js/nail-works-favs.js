/**
 * nail-works-favs.js
 * Handles favorite-heart toggling via AJAX on both the Gallery grid
 * and the Gallery-Details page.
 *
 * Uses a two-icon approach: each button contains both a hollow heart
 * (.heart-hollow) and a filled heart (.heart-filled). CSS controls
 * which is visible based on the .favorited class on the button.
 * The JS only needs to toggle .favorited — no icon class manipulation.
 *
 * Uses event delegation so it works regardless of DOM order.
 * Stores per-product state in localStorage for cross-page sync.
 */
(function () {
  'use strict';

  /* ── helpers ───────────────────────────────────────────── */

  /** Grab the antiforgery token from any form on the page. */
  function getToken() {
    var el = document.querySelector('input[name="__RequestVerificationToken"]');
    return el ? el.value : null;
  }

  /** Read the full favorites map from localStorage.  { "3": true, "7": false, … } */
  function readFavMap() {
    try {
      return JSON.parse(localStorage.getItem('fav_map') || '{}');
    } catch (_) { return {}; }
  }

  /** Persist one entry into the map AND the legacy single-item key. */
  function writeFavMap(id, favorited) {
    try {
      var map = readFavMap();
      map[String(id)] = !!favorited;
      localStorage.setItem('fav_map', JSON.stringify(map));
      localStorage.setItem('fav_change', JSON.stringify({ id: String(id), favorited: !!favorited, ts: Date.now() }));
    } catch (_) { /* storage full / private mode */ }
  }

  /* ── UI helpers ────────────────────────────────────────── */

  /**
   * Apply favourited / un-favourited state to a single button.
   * With the two-icon approach, we only toggle .favorited on the button.
   * CSS handles showing/hiding .heart-hollow vs .heart-filled.
   */
  function setButtonState(btn, favorited) {
    if (!btn) return;
    if (favorited) {
      btn.classList.add('favorited');
    } else {
      btn.classList.remove('favorited');
    }
  }

  /** Find every fav button for a given product id and update them. */
  function applyToAll(id, favorited) {
    var sel = '.favorite-btn[data-id="' + id + '"], .btn-heart[data-id="' + id + '"]';
    var btns = document.querySelectorAll(sel);
    for (var i = 0; i < btns.length; i++) setButtonState(btns[i], favorited);
  }

  /** Kick a tiny pop animation on the button for tactile feedback. */
  function animatePop(btn) {
    btn.classList.add('fav-pop');
    setTimeout(function () { btn.classList.remove('fav-pop'); }, 350);
  }

  /* ── init: apply stored state on page load ─────────────── */

  function initFromStorage() {
    var map = readFavMap();
    Object.keys(map).forEach(function (id) {
      applyToAll(id, map[id]);
    });
  }

  /* ── BroadcastChannel (cross-tab) ──────────────────────── */

  var bc = (typeof BroadcastChannel !== 'undefined') ? new BroadcastChannel('fav_channel') : null;

  if (bc) {
    bc.onmessage = function (ev) {
      if (ev.data && ev.data.id != null) {
        applyToAll(ev.data.id, ev.data.favorited);
      }
    };
  }

  // storage event (fires in OTHER tabs, not the current one)
  window.addEventListener('storage', function (e) {
    if (e.key === 'fav_change') {
      try {
        var d = JSON.parse(e.newValue || '{}');
        if (d.id != null) applyToAll(d.id, d.favorited);
      } catch (_) { /* ignore */ }
    }
  });

  /* ── click handler (delegated) ─────────────────────────── */

  function handleFavClick(e) {
    // Walk up from the click target to find the actual button
    var btn = e.target.closest('.favorite-btn[data-id], .btn-heart[data-id]');
    if (!btn) return; // click wasn't on a fav button

    // Prevent form submission & link navigation
    e.preventDefault();
    e.stopPropagation();

    var itemId = btn.getAttribute('data-id');
    if (!itemId) return;

    var wasFavorited = btn.classList.contains('favorited');
    var nowFavorited = !wasFavorited;

    // ── Optimistic UI toggle ──
    setButtonState(btn, nowFavorited);
    animatePop(btn);

    // ── Server sync ──
    var token = getToken();
    var headers = { 'Content-Type': 'application/json' };
    if (token) headers['RequestVerificationToken'] = token;

    fetch('/Gallery/toggle-favorite/' + itemId, {
      method: 'POST',
      headers: headers
    })
      .then(function (res) { return res.json(); })
      .then(function (data) {
        if (!data || !data.success) {
          // server rejected – revert
          setButtonState(btn, wasFavorited);
          console.error('Favorite toggle rejected by server', data);
          return;
        }
        // Use the server's authoritative state
        var serverFav = !!data.favorited;
        setButtonState(btn, serverFav);
        writeFavMap(itemId, serverFav);
        if (bc) try { bc.postMessage({ id: itemId, favorited: serverFav }); } catch (_) { }
      })
      .catch(function (err) {
        // Network error – revert
        setButtonState(btn, wasFavorited);
        console.error('Favorite toggle network error', err);
      });
  }

  /* ── bootstrap ─────────────────────────────────────────── */

  function init() {
    // Apply stored favourite states so navigating back shows up-to-date hearts
    initFromStorage();

    // Single delegated listener on the document
    document.addEventListener('click', handleFavClick);
  }

  // Attach when DOM is ready
  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init);
  } else {
    init();
  }
})();
