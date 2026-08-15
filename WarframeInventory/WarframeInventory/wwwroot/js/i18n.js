(() => {
    const STORAGE_KEY = "warframe-language";
    const SUPPORTED = new Set(["en", "es"]);
    const originalText = new WeakMap();
    const lastText = new WeakMap();
    const originalAttributes = new WeakMap();
    const translatedAttributes = new WeakMap();
    const attributes = ["placeholder", "title", "aria-label", "data-label"];
    let language = SUPPORTED.has(localStorage.getItem(STORAGE_KEY))
        ? localStorage.getItem(STORAGE_KEY)
        : "en";
    let pack;
    let loadSequence = 0;

    document.documentElement.lang = language;
    const normalize = (value) => value.replace(/\s+/g, " ").trim();
    const isIgnored = (node) => node.parentElement?.closest(
        "script, style, code, pre, [data-i18n-ignore='true']"
    );

    const translateNormalized = (value) => {
        if (!pack || !value) return value;
        const exact = pack.translations?.[value];
        if (typeof exact === "string") return exact;
        for (const rule of pack.patterns ?? []) {
            const expression = new RegExp(rule.pattern, "u");
            if (expression.test(value)) return value.replace(expression, rule.replacement);
        }
        let result = value;
        for (const segment of pack.segments ?? [])
            result = result.replaceAll(segment.source, segment.target);
        return result;
    };

    const preserveSpacing = (source, translated) => {
        const leading = source.match(/^\s*/u)?.[0] ?? "";
        const trailing = source.match(/\s*$/u)?.[0] ?? "";
        return `${leading}${translated}${trailing}`;
    };

    const applyTextNode = (node) => {
        if (isIgnored(node)) return;
        const current = node.nodeValue ?? "";
        if (!current.trim()) return;
        // Blazor can replace an already translated text node with fresh
        // Spanish content. Translate that current value first so dynamic
        // counters, goals and planner cards never remain partially Spanish.
        const direct = preserveSpacing(current, translateNormalized(normalize(current)));
        if (direct !== current) {
            originalText.set(node, current);
            node.nodeValue = direct;
            lastText.set(node, direct);
            return;
        }
        if (!originalText.has(node)) originalText.set(node, current);
        else if (lastText.get(node) !== current) originalText.set(node, current);
        const source = originalText.get(node);
        const next = preserveSpacing(source, translateNormalized(normalize(source)));
        if (current !== next) node.nodeValue = next;
        lastText.set(node, next);
    };

    const applyElement = (element) => {
        if (!(element instanceof Element) || element.closest("[data-i18n-ignore='true']")) return;
        let originals = originalAttributes.get(element);
        let translated = translatedAttributes.get(element);
        if (!originals) {
            originals = new Map();
            translated = new Map();
            originalAttributes.set(element, originals);
            translatedAttributes.set(element, translated);
        }
        for (const attribute of attributes) {
            if (!element.hasAttribute(attribute)) continue;
            const current = element.getAttribute(attribute) ?? "";
            const direct = translateNormalized(normalize(current));
            if (direct !== current) {
                originals.set(attribute, current);
                element.setAttribute(attribute, direct);
                translated.set(attribute, direct);
                continue;
            }
            if (!originals.has(attribute) || translated.get(attribute) !== current)
                originals.set(attribute, current);
            const next = translateNormalized(normalize(originals.get(attribute)));
            if (next !== current) element.setAttribute(attribute, next);
            translated.set(attribute, next);
        }
    };

    const applyTree = (root = document.documentElement) => {
        if (!root || !pack) return;
        if (root.nodeType === Node.TEXT_NODE) return applyTextNode(root);
        if (root.nodeType !== Node.ELEMENT_NODE && root.nodeType !== Node.DOCUMENT_NODE) return;
        if (root instanceof Element) applyElement(root);
        const walker = document.createTreeWalker(root, NodeFilter.SHOW_ELEMENT | NodeFilter.SHOW_TEXT);
        let node;
        while ((node = walker.nextNode())) {
            if (node.nodeType === Node.TEXT_NODE) applyTextNode(node);
            else applyElement(node);
        }
        document.querySelectorAll("[data-language-selector]").forEach((selector) => {
            selector.value = language;
            selector.setAttribute("aria-label", language === "en" ? "Language" : "Idioma");
        });
    };

    const load = async (requested) => {
        const next = SUPPORTED.has(requested) ? requested : "en";
        const sequence = ++loadSequence;
        const response = await fetch(`/i18n/${next}.json`, { cache: "no-cache" });
        if (!response.ok) throw new Error(`Language pack ${next} returned ${response.status}.`);
        const nextPack = await response.json();
        if (sequence !== loadSequence) return;
        language = next;
        pack = nextPack;
        localStorage.setItem(STORAGE_KEY, language);
        document.documentElement.lang = language;
        applyTree();
        window.dispatchEvent(new CustomEvent("warframe-language-changed", { detail: language }));
    };

    window.warframeI18n = {
        getLanguage: () => language,
        setLanguage: (next) => load(next),
        apply: () => applyTree(),
        ready: null
    };

    const observer = new MutationObserver((mutations) => {
        if (!pack) return;
        for (const mutation of mutations) {
            if (mutation.type === "characterData") applyTextNode(mutation.target);
            for (const node of mutation.addedNodes) applyTree(node);
        }
    });
    const start = () => {
        observer.observe(document.documentElement, { childList: true, subtree: true, characterData: true });
        window.warframeI18n.ready = load(language).catch((error) =>
            console.error("Warframe Tracker localization failed", error));
    };
    if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", start, { once: true });
    else start();
})();
