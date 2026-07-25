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

window.warframeTracker = {
    downloadText: (filename, content, contentType = "application/json;charset=utf-8") => {
        const blob = new Blob([content], { type: contentType });
        const url = URL.createObjectURL(blob);
        const anchor = document.createElement("a");
        anchor.href = url;
        anchor.download = filename;
        anchor.click();
        URL.revokeObjectURL(url);
    },
    toggleEnergyMode: () => {
        const enabled = !document.documentElement.classList.contains("reduced-energy");
        document.documentElement.classList.toggle("reduced-energy", enabled);
        localStorage.setItem("warframe-reduced-energy", enabled ? "1" : "0");
    }
};

if (localStorage.getItem("warframe-reduced-energy") === "1") {
    document.documentElement.classList.add("reduced-energy");
}
