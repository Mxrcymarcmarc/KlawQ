document.addEventListener("DOMContentLoaded", function () {
    // 1. Hook into navigation brand link clicks
    const brandLinks = document.querySelectorAll("a.nav-brand");
    brandLinks.forEach(link => {
        link.addEventListener("click", function (e) {
            const currentPath = window.location.pathname.toLowerCase();
            const targetHref = this.getAttribute("href") || "";
            
            // Clean paths for matching
            const isToUserHome = targetHref === "/" || targetHref.toLowerCase() === "/home" || targetHref.toLowerCase() === "/home/index" || targetHref === "";
            const isToAdminHome = targetHref.toLowerCase() === "/admin";
            
            const isOnUserHome = currentPath === "/" || currentPath === "/home" || currentPath === "/home/index";
            const isOnAdminHome = currentPath === "/admin";
            
            if ((isToUserHome && isOnUserHome) || (isToAdminHome && isOnAdminHome)) {
                // If already on the corresponding home page, scroll up to top smoothly
                e.preventDefault();
                window.scrollTo({ top: 0, behavior: "smooth" });
            } else if (isToUserHome && !isOnUserHome) {
                // If navigating to home from another page, trigger the transition flag
                sessionStorage.setItem("brandTransition", "true");
            }
        });
    });

    // 2. Play transition animation if the user came from another page via the brand logo
    if (sessionStorage.getItem("brandTransition") === "true") {
        sessionStorage.removeItem("brandTransition");
        const homeWrapper = document.querySelector(".home-wrapper");
        if (homeWrapper) {
            homeWrapper.classList.add("brand-transition-in");
        }
    }
});
