/// <reference path="overwolf.d.ts" />
/// <reference path="core.ts" />

type SignalState = "waiting" | "connecting" | "ready" | "captured" | "error";
interface StoredCapture { json: string; digest: string; receivedUtc: string; source: string; distinctItems: number; }

const WARFRAME_GAME_ID = 8954;
const REQUIRED_FEATURES = ["game_info", "match_info"];
const FEATURE_RETRIES = 10;
const POLL_INTERVAL_MS = 2500;
const DB_NAME = "warframe-tracker-native";
const DB_STORE = "captures";
const CAPTURE_KEY = "latest";

let currentWindowId = "main";
// `null` means Overwolf has not reported the initial game state yet. Without
// this third state, an initial "game not running" response is treated as an
// unchanged value and the UI remains stuck on INITIALIZING.
let running: boolean | null = null;
let pollTimer: number | undefined;
let polling = false;
let lastDigest = "";
let currentCapture: StoredCapture | null = null;
let trackerOrigin = "";
let bridgeNonce = "";
let bridgeReady = false;

const byId = <T extends HTMLElement>(id: string): T => {
  const element = document.getElementById(id);
  if (!element) throw new Error(`Missing element #${id}`);
  return element as T;
};

function setSignal(state: SignalState, title: string, detail: string): void {
  const signal = byId("signal");
  signal.dataset.state = state;
  byId("signal-title").textContent = title;
  byId("signal-detail").textContent = detail;
}

function setCaptureUi(capture: StoredCapture | null): void {
  currentCapture = capture;
  byId("capture-empty").hidden = capture !== null;
  byId("capture-data").hidden = capture === null;
  (byId<HTMLButtonElement>("send-capture")).disabled = capture === null || !bridgeReady;
  if (!capture) return;
  byId("capture-items").textContent = capture.distinctItems.toLocaleString();
  byId("capture-time").textContent = new Date(capture.receivedUtc).toLocaleString();
  byId("capture-source").textContent = capture.source;
}

function openDatabase(): Promise<IDBDatabase> {
  return new Promise((resolve, reject) => {
    const request = indexedDB.open(DB_NAME, 1);
    request.onupgradeneeded = () => {
      if (!request.result.objectStoreNames.contains(DB_STORE))
        request.result.createObjectStore(DB_STORE);
    };
    request.onsuccess = () => resolve(request.result);
    request.onerror = () => reject(request.error ?? new Error("IndexedDB could not be opened."));
  });
}

async function saveCapture(capture: StoredCapture): Promise<void> {
  const database = await openDatabase();
  await new Promise<void>((resolve, reject) => {
    const transaction = database.transaction(DB_STORE, "readwrite");
    transaction.objectStore(DB_STORE).put(capture, CAPTURE_KEY);
    transaction.oncomplete = () => resolve();
    transaction.onabort = transaction.onerror = () => reject(transaction.error ?? new Error("Capture transaction failed."));
  });
  database.close();
}

async function loadCapture(): Promise<StoredCapture | null> {
  const database = await openDatabase();
  const capture = await new Promise<StoredCapture | undefined>((resolve, reject) => {
    const request = database.transaction(DB_STORE, "readonly").objectStore(DB_STORE).get(CAPTURE_KEY);
    request.onsuccess = () => resolve(request.result as StoredCapture | undefined);
    request.onerror = () => reject(request.error ?? new Error("Capture could not be read."));
  });
  database.close();
  if (!capture) return null;
  if (Date.now() - Date.parse(capture.receivedUtc) > WarframeNativeCore.CAPTURE_LIFETIME_MS) {
    await clearCapture();
    return null;
  }
  return capture;
}

async function clearCapture(): Promise<void> {
  const database = await openDatabase();
  await new Promise<void>((resolve, reject) => {
    const transaction = database.transaction(DB_STORE, "readwrite");
    transaction.objectStore(DB_STORE).delete(CAPTURE_KEY);
    transaction.oncomplete = () => resolve();
    transaction.onabort = transaction.onerror = () => reject(transaction.error ?? new Error("Capture could not be deleted."));
  });
  database.close();
  lastDigest = "";
  setCaptureUi(null);
}

async function digest(value: string): Promise<string> {
  const bytes = new TextEncoder().encode(value);
  if (crypto.subtle) {
    const hash = await crypto.subtle.digest("SHA-256", bytes);
    return Array.from(new Uint8Array(hash), byte => byte.toString(16).padStart(2, "0")).join("");
  }
  // Older CEF builds may expose getRandomValues without SubtleCrypto. This
  // non-cryptographic fallback is sufficient for local duplicate detection.
  let hash = 2166136261;
  for (const byte of bytes) {
    hash ^= byte;
    hash = Math.imul(hash, 16777619);
  }
  return `fnv1a-${(hash >>> 0).toString(16).padStart(8, "0")}-${bytes.length}`;
}

async function acceptInventory(payload: unknown, source: string): Promise<void> {
  try {
    const json = WarframeNativeCore.normalizeInventory(payload);
    const fingerprint = await digest(json);
    if (fingerprint === lastDigest) return;
    const capture: StoredCapture = {
      json,
      digest: fingerprint,
      receivedUtc: new Date().toISOString(),
      source,
      distinctItems: WarframeNativeCore.countDistinctItems(json)
    };
    await saveCapture(capture);
    lastDigest = fingerprint;
    setCaptureUi(capture);
    setSignal("captured", "INVENTORY CAPTURED", "Review it and explicitly send it to the Tracker.");
  } catch (error) {
    setSignal("error", "CAPTURE REJECTED", error instanceof Error ? error.message : "Invalid inventory data.");
  }
}

function getInfo(): Promise<OverwolfGetInfoResult> {
  return new Promise(resolve => overwolf.games.events.getInfo(resolve));
}

async function inspectCurrentInfo(): Promise<void> {
  if (!running || polling) return;
  polling = true;
  try {
    const result = await getInfo();
    const inventory = WarframeNativeCore.inventoryFromUpdate(result);
    if (inventory !== undefined) await acceptInventory(inventory, "overwolf-native-getInfo");
  } finally {
    polling = false;
  }
}

function setRequiredFeatures(): Promise<OverwolfResult & { supportedFeatures?: string[] }> {
  return new Promise(resolve => overwolf.games.events.setRequiredFeatures(REQUIRED_FEATURES, resolve));
}

async function activateGep(): Promise<void> {
  setSignal("connecting", "CONNECTING TO GEP", "Registering Warframe inventory features…");
  for (let attempt = 1; attempt <= FEATURE_RETRIES; attempt++) {
    const result = await setRequiredFeatures();
    if (result.success && result.supportedFeatures?.includes("match_info")) {
      setSignal("ready", "GEP READY", "Open the Warframe inventory or trigger a loading screen.");
      await inspectCurrentInfo();
      if (pollTimer === undefined)
        pollTimer = window.setInterval(() => void inspectCurrentInfo(), POLL_INTERVAL_MS);
      return;
    }
    await new Promise(resolve => setTimeout(resolve, 3000));
  }
  setSignal("error", "GEP UNAVAILABLE", "Could not register match_info after 10 attempts. Restart Overwolf and Warframe.");
}

function stopGep(): void {
  if (pollTimer !== undefined) window.clearInterval(pollTimer);
  pollTimer = undefined;
  setSignal("waiting", "WARFRAME NOT DETECTED", "Start Warframe to enable automatic inventory capture.");
}

function updateGame(game: OverwolfGameInfo | null | undefined): void {
  const classId = game?.classId ?? (game?.id ? Math.floor(game.id / 10) : 0);
  const nowRunning = Boolean(game?.isRunning && classId === WARFRAME_GAME_ID);
  if (nowRunning === running) return;
  running = nowRunning;
  if (running) void activateGep(); else stopGep();
}

function configureTracker(rawUrl: string): void {
  const url = WarframeNativeCore.safeTrackerUrl(rawUrl);
  const frame = byId<HTMLIFrameElement>("tracker-frame");
  const setup = byId("tracker-setup");
  if (!url) {
    trackerOrigin = "";
    frame.hidden = true;
    setup.hidden = false;
    bridgeReady = false;
    setCaptureUi(currentCapture);
    return;
  }
  localStorage.setItem("trackerUrl", url.toString());
  trackerOrigin = url.origin;
  bridgeReady = false;
  bridgeNonce = "";
  frame.src = url.toString();
  frame.hidden = false;
  setup.hidden = true;
  setCaptureUi(currentCapture);
}

function postCapture(): void {
  const frame = byId<HTMLIFrameElement>("tracker-frame");
  if (!currentCapture || !bridgeReady || !bridgeNonce || !trackerOrigin || !frame.contentWindow) return;
  frame.contentWindow.postMessage({
    type: "warframe-tracker-native-inventory",
    version: 1,
    nonce: bridgeNonce,
    inventoryJson: currentCapture.json,
    source: currentCapture.source
  }, trackerOrigin);
  setSignal("connecting", "SENDING PREVIEW", "Waiting for the authenticated Tracker page…");
}

function onBridgeMessage(event: MessageEvent): void {
  if (!trackerOrigin || event.origin !== trackerOrigin || event.source !== byId<HTMLIFrameElement>("tracker-frame").contentWindow)
    return;
  const data = event.data as Record<string, unknown> | null;
  if (!data || data.version !== 1) return;
  if (data.type === "warframe-tracker-native-ready" && typeof data.nonce === "string") {
    bridgeNonce = data.nonce;
    bridgeReady = true;
    setCaptureUi(currentCapture);
  } else if (data.type === "warframe-tracker-native-result" && data.nonce === bridgeNonce) {
    const ok = data.success === true;
    setSignal(ok ? "ready" : "error", ok ? "CAPTURE DELIVERED" : "DELIVERY FAILED",
      typeof data.message === "string" ? data.message : ok ? "Open Account Sync to review it." : "The Tracker rejected the capture.");
  }
}

function toggleWindow(): void {
  overwolf.windows.getWindowState(currentWindowId, result => {
    if (result.window_state === "normal" || result.window_state === "maximized")
      overwolf.windows.hide(currentWindowId);
    else
      overwolf.windows.restore(currentWindowId);
  });
}

async function initialize(): Promise<void> {
  overwolf.windows.getCurrentWindow(result => { if (result.window?.id) currentWindowId = result.window.id; });
  overwolf.games.events.onInfoUpdates2.addListener(update => {
    const inventory = WarframeNativeCore.inventoryFromUpdate(update);
    if (inventory !== undefined) void acceptInventory(inventory, "overwolf-native-info-update");
  });
  overwolf.games.events.onError.addListener(() => {
    setSignal("error", "GEP REPORTED AN ERROR", "No inventory data was written to logs. Retry with a new loading screen.");
  });
  overwolf.games.onGameInfoUpdated.addListener(update => updateGame(update.gameInfo));
  overwolf.settings.hotkeys.onPressed.addListener(event => { if (event.name === "show_tracker") toggleWindow(); });
  window.addEventListener("message", onBridgeMessage);

  byId("send-capture").addEventListener("click", postCapture);
  byId("discard-capture").addEventListener("click", () => void clearCapture());
  byId("save-url").addEventListener("click", () => configureTracker(byId<HTMLInputElement>("tracker-url").value));
  byId("reload-frame").addEventListener("click", () => {
    const frame = byId<HTMLIFrameElement>("tracker-frame");
    if (frame.src) frame.src = frame.src;
  });

  const configUrl = window.WarframeTrackerNativeConfig?.trackerUrl ?? "";
  // A URL shipped by the validated package is authoritative. Local storage is
  // only a development fallback when no runtime URL was configured, otherwise
  // an old localhost value can silently override the production backend.
  const trackerUrl = configUrl || localStorage.getItem("trackerUrl") || "";
  byId<HTMLInputElement>("tracker-url").value = trackerUrl;
  configureTracker(trackerUrl);
  const restored = await loadCapture();
  if (restored) lastDigest = restored.digest;
  setCaptureUi(restored);
  overwolf.games.getRunningGameInfo(updateGame);
}

window.addEventListener("DOMContentLoaded", () => void initialize().catch(error => {
  setSignal("error", "STARTUP ERROR", error instanceof Error ? error.message : "Native initialization failed.");
}));
