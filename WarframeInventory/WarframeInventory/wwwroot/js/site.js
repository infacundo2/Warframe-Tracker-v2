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
    let ambientNodes = [];
    let ambientTimer;
    let ambientOutput;
    let ambientDelay;
    let ambientStep = 0;
    let nextAmbientStep = 0;
    let ambientEnabled = localStorage.getItem("warframe-ambient-audio") === "1";
    let effectsEnabled = localStorage.getItem("warframe-interface-audio") !== "0";

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
        window.clearTimeout(ambientTimer);
        ambientTimer = null;
        ambientNodes.forEach(node => {
            try { node.stop?.(); } catch { }
            try { node.disconnect?.(); } catch { }
        });
        ambientNodes = [];
        ambientOutput = null;
        ambientDelay = null;
        ambientStep = 0;
    };

    const midiFrequency = note => 440 * Math.pow(2, (note - 69) / 12);

    const registerVoice = (oscillator, gain) => {
        ambientNodes.push(oscillator, gain);
        oscillator.onended = () => {
            oscillator.disconnect();
            gain.disconnect();
            ambientNodes = ambientNodes.filter(node => node !== oscillator && node !== gain);
        };
    };

    const playPad = (time, notes) => {
        notes.forEach((note, index) => {
            const oscillator = context.createOscillator();
            const gain = context.createGain();
            oscillator.type = index === 1 ? "triangle" : "sine";
            oscillator.frequency.value = midiFrequency(note);
            oscillator.detune.value = index * 4 - 4;
            gain.gain.setValueAtTime(0.0001, time);
            gain.gain.exponentialRampToValueAtTime(0.055, time + 1.3);
            gain.gain.setValueAtTime(0.055, time + 4.7);
            gain.gain.exponentialRampToValueAtTime(0.0001, time + 6.5);
            oscillator.connect(gain).connect(ambientOutput);
            oscillator.start(time);
            oscillator.stop(time + 6.6);
            registerVoice(oscillator, gain);
        });
    };

    const playSubPulse = (time, note) => {
        const oscillator = context.createOscillator();
        const gain = context.createGain();
        oscillator.type = "sine";
        oscillator.frequency.value = midiFrequency(note - 12);
        gain.gain.setValueAtTime(0.0001, time);
        gain.gain.exponentialRampToValueAtTime(0.065, time + 0.08);
        gain.gain.exponentialRampToValueAtTime(0.0001, time + 0.72);
        oscillator.connect(gain).connect(ambientOutput);
        oscillator.start(time);
        oscillator.stop(time + 0.75);
        registerVoice(oscillator, gain);
    };

    const playDigitalPulse = (time, note) => {
        const oscillator = context.createOscillator();
        const gain = context.createGain();
        oscillator.type = "triangle";
        oscillator.frequency.value = midiFrequency(note);
        gain.gain.setValueAtTime(0.0001, time);
        gain.gain.exponentialRampToValueAtTime(0.022, time + 0.012);
        gain.gain.exponentialRampToValueAtTime(0.0001, time + 0.22);
        oscillator.connect(gain);
        gain.connect(ambientOutput);
        gain.connect(ambientDelay);
        oscillator.start(time);
        oscillator.stop(time + 0.24);
        registerVoice(oscillator, gain);
    };

    const scheduleMusic = () => {
        if (!ambientEnabled || !context || !ambientOutput) return;
        const stepDuration = 60 / 72 / 2;
        const chords = [
            [50, 53, 57],
            [46, 50, 53],
            [41, 45, 48],
            [48, 52, 55]
        ];
        const melody = [74, 77, 81, 79, 77, 74, 72, 69];

        while (nextAmbientStep < context.currentTime + 0.35) {
            const chord = chords[Math.floor(ambientStep / 16) % chords.length];
            if (ambientStep % 16 === 0) playPad(nextAmbientStep, chord);
            if (ambientStep % 4 === 0) playSubPulse(nextAmbientStep, chord[0]);
            if (ambientStep % 2 === 1)
                playDigitalPulse(nextAmbientStep, melody[Math.floor(ambientStep / 2) % melody.length]);

            nextAmbientStep += stepDuration;
            ambientStep = (ambientStep + 1) % 64;
        }
        ambientTimer = window.setTimeout(scheduleMusic, 90);
    };

    const startAmbient = () => {
        const audio = ensureContext();
        if (!audio || ambientTimer) return;

        ambientOutput = audio.createGain();
        const filter = audio.createBiquadFilter();
        const delay = audio.createDelay(1);
        const feedback = audio.createGain();
        const delayWet = audio.createGain();
        ambientDelay = delay;

        ambientOutput.gain.value = 0.32;
        filter.type = "lowpass";
        filter.frequency.value = 1450;
        filter.Q.value = 0.7;
        delay.delayTime.value = 0.38;
        feedback.gain.value = 0.24;
        delayWet.gain.value = 0.26;

        ambientOutput.connect(filter).connect(audio.destination);
        delay.connect(feedback).connect(delay);
        delay.connect(delayWet).connect(filter);
        ambientNodes.push(ambientOutput, filter, delay, feedback, delayWet);

        ambientStep = 0;
        nextAmbientStep = audio.currentTime + 0.08;
        scheduleMusic();
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
    };

    return {
        toggleAmbient: () => {
            ambientEnabled = !ambientEnabled;
            localStorage.setItem("warframe-ambient-audio", ambientEnabled ? "1" : "0");
            ambientEnabled ? startAmbient() : stopAmbient();
            updateControls();
            interfacePulse(true, true);
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
