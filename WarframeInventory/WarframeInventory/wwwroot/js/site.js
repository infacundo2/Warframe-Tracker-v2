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
    },
    copyText: async (content) => navigator.clipboard.writeText(content)
};

if (localStorage.getItem("warframe-reduced-energy") === "1") {
    document.documentElement.classList.add("reduced-energy");
}

document.addEventListener("pointermove", (event) => {
    if (document.documentElement.classList.contains("reduced-energy")) return;
    const x = (event.clientX / window.innerWidth - 0.5) * 2;
    const y = (event.clientY / window.innerHeight - 0.5) * 2;
    document.documentElement.style.setProperty("--pointer-x", x.toFixed(3));
    document.documentElement.style.setProperty("--pointer-y", y.toFixed(3));
}, { passive: true });
