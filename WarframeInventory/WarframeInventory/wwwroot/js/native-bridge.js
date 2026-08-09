(() => {
    "use strict";
    if (window.parent === window) return;

    const version = 1;
    const maxLength = 20 * 1024 * 1024;
    const nonceBytes = new Uint8Array(24);
    crypto.getRandomValues(nonceBytes);
    const nonce = Array.from(nonceBytes, value => value.toString(16).padStart(2, "0")).join("");

    const reply = (targetOrigin, success, message) => {
        window.parent.postMessage({
            type: "warframe-tracker-native-result",
            version,
            nonce,
            success,
            message
        }, targetOrigin || "*");
    };

    window.addEventListener("message", async event => {
        if (event.source !== window.parent) return;
        const data = event.data;
        if (!data || data.type !== "warframe-tracker-native-inventory"
            || data.version !== version || data.nonce !== nonce) return;
        if (typeof data.inventoryJson !== "string" || data.inventoryJson.length < 2
            || data.inventoryJson.length > maxLength) {
            reply(event.origin, false, "The capture is empty or exceeds the 20 MB safety limit.");
            return;
        }

        try {
            const response = await fetch("/api/native-inventory/capture", {
                method: "POST",
                credentials: "same-origin",
                headers: {
                    "Content-Type": "application/json",
                    "X-Warframe-Native-Bridge": "1"
                },
                body: JSON.stringify({ inventoryJson: data.inventoryJson })
            });
            let result = {};
            try { result = await response.json(); } catch { /* Safe generic message below. */ }
            if (!response.ok) {
                const message = response.status === 401
                    ? "Sign in to Warframe Tracker inside this window and try again."
                    : result.error || "The Tracker rejected the capture.";
                reply(event.origin, false, message);
                return;
            }
            reply(event.origin, true,
                `Capture received: ${Number(result.distinctItems || 0).toLocaleString()} detected entries. Review it before applying.`);
        } catch {
            reply(event.origin, false, "The Tracker server could not be reached. No inventory was applied.");
        }
    });

    window.parent.postMessage({
        type: "warframe-tracker-native-ready",
        version,
        nonce
    }, "*");
})();
