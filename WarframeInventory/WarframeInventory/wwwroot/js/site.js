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

const tennoAudio = (() => {
    let context;
    let ambientEnabled = localStorage.getItem("warframe-ambient-audio") === "1";
    let effectsEnabled = localStorage.getItem("warframe-interface-audio") !== "0";
    let ambientVolume = Number.parseFloat(
        localStorage.getItem("warframe-ambient-volume") ?? "0.35"
    );
    if (!Number.isFinite(ambientVolume))
        ambientVolume = 0.35;
    ambientVolume = Math.min(1, Math.max(0, ambientVolume));

    const ensureContext = () => {
        if (!context) {
            const AudioContext = window.AudioContext || window.webkitAudioContext;
            if (!AudioContext) return null;
            context = new AudioContext();
        }
        if (context.state === "suspended") context.resume();
        return context;
    };

    const stopAmbient = () => {
        const player = document.getElementById("ambient-soundtrack");
        player?.pause();
    };

    const startAmbient = async () => {
        const player = document.getElementById("ambient-soundtrack");
        if (!player) return false;
        player.volume = ambientVolume;
        try {
            await player.play();
            return true;
        } catch {
            return false;
        }
    };

    const interfacePulse = (strong = false, force = false) => {
        if (!effectsEnabled && !force) return;
        const audio = ensureContext();
        if (!audio) return;

        const now = audio.currentTime;
        const pulses = strong ? 2 : 1;
        for (let index = 0; index < pulses; index++) {
            const oscillator = audio.createOscillator();
            const gain = audio.createGain();
            const start = now + index * 0.055;
            oscillator.type = "square";
            oscillator.frequency.value = strong ? 1120 + index * 240 : 860;
            gain.gain.setValueAtTime(0.0001, start);
            gain.gain.exponentialRampToValueAtTime(strong ? 0.055 : 0.026, start + 0.003);
            gain.gain.exponentialRampToValueAtTime(0.0001, start + 0.024);
            oscillator.connect(gain).connect(audio.destination);
            oscillator.start(start);
            oscillator.stop(start + 0.028);
        }
    };

    const updateControls = () => {
        const ambientButton = document.getElementById("ambient-audio-toggle");
        const effectsButton = document.getElementById("interface-audio-toggle");
        ambientButton?.classList.toggle("audio-active", ambientEnabled);
        effectsButton?.classList.toggle("audio-active", effectsEnabled);
        ambientButton?.setAttribute("aria-pressed", ambientEnabled ? "true" : "false");
        effectsButton?.setAttribute("aria-pressed", effectsEnabled ? "true" : "false");
        const volume = document.getElementById("ambient-volume");
        const volumeReadout = document.getElementById("ambient-volume-value");
        if (volume) volume.value = String(Math.round(ambientVolume * 100));
        if (volumeReadout) volumeReadout.textContent = `${Math.round(ambientVolume * 100)}%`;
    };

    return {
        toggleAmbient: async () => {
            ambientEnabled = !ambientEnabled;
            localStorage.setItem("warframe-ambient-audio", ambientEnabled ? "1" : "0");
            if (ambientEnabled && !(await startAmbient())) {
                ambientEnabled = false;
                localStorage.setItem("warframe-ambient-audio", "0");
            } else if (!ambientEnabled) {
                stopAmbient();
            }
            updateControls();
            interfacePulse(true, true);
        },
        setAmbientVolume: value => {
            ambientVolume = Math.min(1, Math.max(0, Number(value) / 100));
            localStorage.setItem("warframe-ambient-volume", ambientVolume.toFixed(2));
            const player = document.getElementById("ambient-soundtrack");
            if (player) player.volume = ambientVolume;
            updateControls();
        },
        toggleEffects: () => {
            effectsEnabled = !effectsEnabled;
            localStorage.setItem("warframe-interface-audio", effectsEnabled ? "1" : "0");
            updateControls();
            interfacePulse(true, true);
        },
        pulse: interfacePulse,
        restore: () => {
            updateControls();
            if (ambientEnabled) startAmbient();
        }
    };
})();

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
    copyText: async (content) => navigator.clipboard.writeText(content),
    audio: tennoAudio
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

let tennoCursor;
if (window.matchMedia("(pointer: fine)").matches) {
    tennoCursor = document.createElement("div");
    tennoCursor.id = "tenno-cursor";
    tennoCursor.setAttribute("aria-hidden", "true");
    tennoCursor.innerHTML = '<span class="tenno-cursor-core"></span><span class="tenno-cursor-ring"></span>';
    document.body.appendChild(tennoCursor);
    document.body.classList.add("tenno-cursor-ready");
    document.addEventListener("pointermove", (event) => {
        tennoCursor.style.transform = `translate3d(${event.clientX}px, ${event.clientY}px, 0)`;
        tennoCursor.classList.toggle(
            "tenno-cursor-hidden",
            Boolean(event.target.closest("input, textarea, select, [contenteditable='true']"))
        );
    }, { passive: true });
    document.addEventListener("pointerdown", () => tennoCursor.classList.add("tenno-cursor-pressed"), { passive: true });
    document.addEventListener("pointerup", () => tennoCursor.classList.remove("tenno-cursor-pressed"), { passive: true });
    document.addEventListener("pointerleave", () => tennoCursor.classList.add("tenno-cursor-offscreen"), { passive: true });
    document.addEventListener("pointerenter", () => tennoCursor.classList.remove("tenno-cursor-offscreen"), { passive: true });
}

document.addEventListener("pointerdown", (event) => {
    const isControl = event.target.closest("#ambient-audio-toggle, #interface-audio-toggle");
    if (!isControl) tennoAudio.pulse(Boolean(event.target.closest("button, a, [role='button']")));
}, { passive: true });

window.setTimeout(() => tennoAudio.restore(), 100);

document.addEventListener("visibilitychange", () => {
    if (document.visibilityState === "visible") tennoAudio.restore();
});
