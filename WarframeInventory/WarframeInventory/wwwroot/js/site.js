document.addEventListener("keydown", (event) => {
    if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === "k") {
        event.preventDefault();
        if (window.location.pathname !== "/search") {
            window.location.href = "/search";
        } else {
            document.getElementById("universal-search-input")?.focus();
        }
    }
    if (event.key === "Escape" && window.location.pathname === "/search") {
        window.history.back();
    }
});

if (window.location.pathname === "/search") {
    window.setTimeout(() => document.getElementById("universal-search-input")?.focus(), 150);
}
