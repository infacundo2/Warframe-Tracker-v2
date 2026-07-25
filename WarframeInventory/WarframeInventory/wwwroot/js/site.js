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
    let ambientGain;
    let ambientNodes = [];
    let ambientEnabled = localStorage.getItem("warframe-ambient-audio") === "1";
    let effectsEnabled = localStorage.getItem("warframe-interface-audio") === "1";

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
        ambientNodes.forEach(node => {
            try { node.stop?.(); } catch { }
            try { node.disconnect?.(); } catch { }
        });
        ambientNodes = [];
        ambientGain?.disconnect();
        ambientGain = null;
    };

    const startAmbient = () => {
        const audio = ensureContext();
        if (!audio || ambientNodes.length) return;

        ambientGain = audio.createGain();
        ambientGain.gain.setValueAtTime(0.0001, audio.currentTime);
        ambientGain.gain.exponentialRampToValueAtTime(0.035, audio.currentTime + 2.5);
        ambientGain.connect(audio.destination);

        const filter = audio.createBiquadFilter();
        filter.type = "lowpass";
        filter.frequency.value = 720;
        filter.Q.value = 4;
        filter.connect(ambientGain);

        const frequencies = [55, 82.41, 110];
        frequencies.forEach((frequency, index) => {
            const oscillator = audio.createOscillator();
            const gain = audio.createGain();
            oscillator.type = index === 1 ? "triangle" : "sine";
            oscillator.frequency.value = frequency;
            oscillator.detune.value = index * 3 - 3;
            gain.gain.value = index === 1 ? 0.16 : 0.11;
            oscillator.connect(gain).connect(filter);
            oscillator.start();
            ambientNodes.push(oscillator, gain);
        });

        const lfo = audio.createOscillator();
        const lfoGain = audio.createGain();
        lfo.frequency.value = 0.065;
        lfoGain.gain.value = 280;
        lfo.connect(lfoGain).connect(filter.frequency);
        lfo.start();
        ambientNodes.push(lfo, lfoGain, filter);
    };

    const interfacePulse = (strong = false) => {
        if (!effectsEnabled) return;
        const audio = ensureContext();
        if (!audio) return;

        const now = audio.currentTime;
        const oscillator = audio.createOscillator();
        const gain = audio.createGain();
        const filter = audio.createBiquadFilter();
        oscillator.type = "sine";
        oscillator.frequency.setValueAtTime(strong ? 920 : 680, now);
        oscillator.frequency.exponentialRampToValueAtTime(strong ? 420 : 310, now + 0.09);
        filter.type = "bandpass";
        filter.frequency.value = 1100;
        filter.Q.value = 2.5;
        gain.gain.setValueAtTime(0.0001, now);
        gain.gain.exponentialRampToValueAtTime(strong ? 0.075 : 0.04, now + 0.008);
        gain.gain.exponentialRampToValueAtTime(0.0001, now + 0.12);
        oscillator.connect(filter).connect(gain).connect(audio.destination);
        oscillator.start(now);
        oscillator.stop(now + 0.13);
    };

    const updateControls = () => {
        const ambientButton = document.getElementById("ambient-audio-toggle");
        const effectsButton = document.getElementById("interface-audio-toggle");
        ambientButton?.classList.toggle("audio-active", ambientEnabled);
        effectsButton?.classList.toggle("audio-active", effectsEnabled);
        ambientButton?.setAttribute("aria-pressed", ambientEnabled ? "true" : "false");
        effectsButton?.setAttribute("aria-pressed", effectsEnabled ? "true" : "false");
    };

    return {
        toggleAmbient: () => {
            ambientEnabled = !ambientEnabled;
            localStorage.setItem("warframe-ambient-audio", ambientEnabled ? "1" : "0");
            ambientEnabled ? startAmbient() : stopAmbient();
            updateControls();
            interfacePulse(true);
        },
        toggleEffects: () => {
            effectsEnabled = !effectsEnabled;
            localStorage.setItem("warframe-interface-audio", effectsEnabled ? "1" : "0");
            updateControls();
            interfacePulse(true);
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

document.addEventListener("pointerdown", (event) => {
    const isControl = event.target.closest("#ambient-audio-toggle, #interface-audio-toggle");
    if (!isControl) tennoAudio.pulse(Boolean(event.target.closest("button, a, [role='button']")));
}, { passive: true });

window.setTimeout(() => tennoAudio.restore(), 100);

document.addEventListener("visibilitychange", () => {
    if (document.visibilityState === "visible") tennoAudio.restore();
});
