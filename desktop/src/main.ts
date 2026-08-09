import { app, BrowserWindow, dialog, globalShortcut, ipcMain, screen, shell } from "electron";
import { ChildProcess, spawn } from "node:child_process";
import { createHash, randomBytes } from "node:crypto";
import { existsSync } from "node:fs";
import { mkdir, readFile, writeFile } from "node:fs/promises";
import { createServer } from "node:net";
import path from "node:path";

// Keep the original data directory even though the public product name is friendlier.
// Changing this path would make existing local inventories appear to disappear.
app.setPath("userData", path.join(app.getPath("appData"), "warframe-tracker-desktop"));

const WARFRAME_GAME_ID = 8954;
const REQUIRED_FEATURES = ["game_info", "match_info"];
const HEALTH_TIMEOUT_MS = 60_000;
const INVENTORY_POLL_INTERVAL_MS = 2_500;
const bridgeKey = randomBytes(32).toString("base64url");
const DEFAULT_TOGGLE_HOTKEY = "CommandOrControl+Shift+T";
const ALLOWED_TOGGLE_HOTKEYS = new Set([
  "CommandOrControl+Shift+T",
  "CommandOrControl+Shift+Y",
  "CommandOrControl+Shift+U",
  "Alt+Shift+T",
  "Alt+Shift+Y"
]);

let backend: ChildProcess | undefined;
let mainWindow: BrowserWindow | undefined;
let backendUrl = "";
let quitting = false;
let currentToggleHotkey = DEFAULT_TOGGLE_HOTKEY;
let consoleOutputAvailable = true;
let inventoryPollTimer: NodeJS.Timeout | undefined;
let inventoryPollInFlight = false;
let lastInventoryDigest = "";
const pendingInventoryDigests = new Set<string>();

function errorMessage(value: unknown): string {
  return value instanceof Error ? value.message : String(value ?? "");
}

// ow-electron can reject an internal package-manager promise while its
// WebContents is being destroyed. Ignore only that known shutdown race and
// keep reporting every other unhandled rejection.
process.on("unhandledRejection", (reason) => {
  const message = errorMessage(reason);
  const expectedPackageShutdown = message.includes("package manager service destroyed");
  if (expectedPackageShutdown && (quitting || BrowserWindow.getAllWindows().length === 0))
    return;
  log("Promesa no controlada en el proceso principal", reason);
});

// In packaged apps stdout is normally absent. During local development the
// parent terminal can also disappear while Electron keeps running. Node emits
// EPIPE asynchronously in that situation, so a try/catch around console.log is
// not enough and the unhandled stream error would terminate the main process.
for (const stream of [process.stdout, process.stderr]) {
  stream?.on("error", (error: NodeJS.ErrnoException) => {
    if (error.code === "EPIPE")
      consoleOutputAvailable = false;
  });
}

function argumentValue(prefix: string): string | undefined {
  return process.argv.find((value) => value.startsWith(prefix))?.slice(prefix.length);
}

function requestedRoute(): string {
  const route = argumentValue("--qa-route=");
  return route?.startsWith("/") && !route.startsWith("//") ? route : "/welcome";
}

function requestedContentSize(): { width: number; height: number } | undefined {
  const raw = argumentValue("--qa-size=");
  const match = raw?.match(/^(\d{3,4})x(\d{3,4})$/i);
  if (!match)
    return undefined;
  return {
    width: Math.min(3840, Math.max(960, Number(match[1]))),
    height: Math.min(2160, Math.max(620, Number(match[2])))
  };
}

function requestedQaWait(): number {
  const raw = argumentValue("--qa-wait=");
  const milliseconds = Number(raw);
  return Number.isFinite(milliseconds)
    ? Math.min(30_000, Math.max(1_000, milliseconds))
    : 4_000;
}

function requestedQaLanguage(): "en" | "es" | undefined {
  const value = argumentValue("--qa-language=");
  return value === "en" || value === "es" ? value : undefined;
}

function log(message: string, error?: unknown): void {
  const suffix = error instanceof Error ? `: ${error.message}` : error ? `: ${String(error)}` : "";
  if (!consoleOutputAvailable || !process.stdout?.writable || process.stdout.destroyed)
    return;
  try {
    process.stdout.write(`[Warframe Tracker] ${message}${suffix}\n`);
  } catch {
    consoleOutputAvailable = false;
  }
}

function toggleMainWindow(): void {
  if (!mainWindow)
    return;
  if (mainWindow.isVisible() && mainWindow.isFocused()) {
    mainWindow.hide();
    return;
  }
  if (mainWindow.isMinimized())
    mainWindow.restore();
  mainWindow.show();
  mainWindow.focus();
}

function hotkeySettingsPath(): string {
  return path.join(app.getPath("userData"), "desktop-settings.json");
}

async function registerToggleHotkey(accelerator: string): Promise<boolean> {
  if (!ALLOWED_TOGGLE_HOTKEYS.has(accelerator))
    return false;
  globalShortcut.unregister(currentToggleHotkey);
  if (!globalShortcut.register(accelerator, toggleMainWindow)) {
    globalShortcut.register(currentToggleHotkey, toggleMainWindow);
    return false;
  }
  currentToggleHotkey = accelerator;
  await writeFile(
    hotkeySettingsPath(),
    JSON.stringify({ toggleHotkey: currentToggleHotkey }, null, 2),
    "utf8");
  return true;
}

async function initializeHotkey(): Promise<void> {
  try {
    const raw = await readFile(hotkeySettingsPath(), "utf8");
    const saved = JSON.parse(raw) as { toggleHotkey?: string };
    if (saved.toggleHotkey && ALLOWED_TOGGLE_HOTKEYS.has(saved.toggleHotkey))
      currentToggleHotkey = saved.toggleHotkey;
  } catch {
    // First launch: use the safe default.
  }
  if (!globalShortcut.register(currentToggleHotkey, toggleMainWindow))
    log(`El atajo ${currentToggleHotkey} está ocupado por otra aplicación.`);
}

function initializeDesktopIpc(): void {
  ipcMain.handle("warframe:get-toggle-hotkey", () => currentToggleHotkey);
  ipcMain.handle("warframe:set-toggle-hotkey", (_event, accelerator: string) =>
    registerToggleHotkey(accelerator));
}

async function findLoopbackPort(): Promise<number> {
  return await new Promise((resolve, reject) => {
    const server = createServer();
    server.once("error", reject);
    server.listen(0, "127.0.0.1", () => {
      const address = server.address();
      if (!address || typeof address === "string") {
        server.close();
        reject(new Error("No se pudo reservar un puerto local."));
        return;
      }
      const port = address.port;
      server.close((error) => error ? reject(error) : resolve(port));
    });
  });
}

function backendCommand(): { executable: string; args: string[]; workingDirectory: string } {
  const portableBackend = process.env.WARFRAME_TRACKER_BACKEND_EXE;
  if (portableBackend && existsSync(portableBackend)) {
    return {
      executable: portableBackend,
      args: [],
      workingDirectory: path.dirname(portableBackend)
    };
  }
  if (app.isPackaged) {
    const executable = path.join(process.resourcesPath, "backend", "WarframeInventory.exe");
    return {
      executable,
      args: [],
      workingDirectory: path.dirname(executable)
    };
  }

  const repositoryRoot = path.resolve(__dirname, "..", "..");
  const project = path.join(
    repositoryRoot,
    "WarframeInventory",
    "WarframeInventory",
    "WarframeInventory.csproj");
  return {
    executable: "dotnet",
    args: ["run", "--project", project, "-c", "Release", "--no-launch-profile"],
    workingDirectory: repositoryRoot
  };
}

async function startBackend(): Promise<void> {
  const port = await findLoopbackPort();
  backendUrl = `http://127.0.0.1:${port}`;
  const command = backendCommand();
  backend = spawn(command.executable, command.args, {
    cwd: command.workingDirectory,
    windowsHide: true,
    stdio: ["ignore", "pipe", "pipe"],
    env: {
      ...process.env,
      ASPNETCORE_ENVIRONMENT: app.isPackaged ? "Production" : "Development",
      WARFRAME_TRACKER_DESKTOP: "1",
      WARFRAME_TRACKER_URL: backendUrl,
      WARFRAME_TRACKER_DATA_DIR: path.join(app.getPath("userData"), "data"),
      WARFRAME_DESKTOP_BRIDGE_KEY: bridgeKey
    }
  });
  backend.stdout?.on("data", (chunk) => process.stdout.write(`[backend] ${chunk}`));
  backend.stderr?.on("data", (chunk) => process.stderr.write(`[backend] ${chunk}`));
  backend.once("exit", (code) => {
    if (quitting)
      log("El backend local se detuvo durante el cierre de la aplicación.");
    else
      log(`El backend local terminó con código ${code ?? "desconocido"}.`);
    backend = undefined;
    if (!quitting && mainWindow) {
      void dialog.showMessageBox(mainWindow, {
        type: "error",
        title: "Servidor local detenido",
        message: "Warframe Tracker perdió la conexión con su servidor local.",
        detail: "Cierra y abre la aplicación para recuperar la sesión."
      });
    }
  });

  const deadline = Date.now() + HEALTH_TIMEOUT_MS;
  while (Date.now() < deadline) {
    if (backend.exitCode !== null)
      throw new Error(`El backend terminó antes de iniciar (${backend.exitCode}).`);
    try {
      const response = await fetch(`${backendUrl}/api/desktop-bridge/health`);
      if (response.ok)
        return;
    } catch {
      // The local server still is starting.
    }
    await new Promise((resolve) => setTimeout(resolve, 350));
  }
  throw new Error("El servidor local no respondió dentro de 60 segundos.");
}

async function createWindow(): Promise<void> {
  const workArea = screen.getPrimaryDisplay().workAreaSize;
  const qaSize = requestedContentSize();
  const width = qaSize?.width ?? Math.max(960, Math.min(1500, workArea.width - 48));
  const height = qaSize?.height ?? Math.max(620, Math.min(930, workArea.height - 48));
  mainWindow = new BrowserWindow({
    width,
    height,
    minWidth: 960,
    minHeight: 620,
    show: false,
    backgroundColor: "#050b12",
    autoHideMenuBar: true,
    webPreferences: {
      preload: path.join(__dirname, "preload.js"),
      nodeIntegration: false,
      contextIsolation: true,
      sandbox: true,
      devTools: !app.isPackaged
    }
  });
  mainWindow.removeMenu();
  mainWindow.once("ready-to-show", () => mainWindow?.show());
  mainWindow.webContents.setWindowOpenHandler(({ url }) => {
    if (url.startsWith(backendUrl))
      return { action: "allow" };
    void shell.openExternal(url);
    return { action: "deny" };
  });
  mainWindow.webContents.on("will-navigate", (event, url) => {
    if (!url.startsWith(backendUrl)) {
      event.preventDefault();
      void shell.openExternal(url);
    }
  });
  await mainWindow.loadURL(`${backendUrl}${requestedRoute()}`);
  if (qaSize)
    mainWindow.setContentSize(qaSize.width, qaSize.height);

  const qaLanguage = requestedQaLanguage();
  if (qaLanguage) {
    await mainWindow.webContents.executeJavaScript(
      `window.warframeI18n?.setLanguage(${JSON.stringify(qaLanguage)})`);
  }

  const screenshotPath = argumentValue("--qa-screenshot=");
  const layoutReportPath = argumentValue("--qa-layout-report=");
  if (screenshotPath || layoutReportPath) {
    await new Promise((resolve) => setTimeout(resolve, requestedQaWait()));
    if (layoutReportPath) {
      const metrics = await mainWindow.webContents.executeJavaScript(`(() => ({
        route: location.pathname + location.search,
        language: document.documentElement.lang,
        viewportWidth: document.documentElement.clientWidth,
        viewportHeight: document.documentElement.clientHeight,
        contentWidth: document.documentElement.scrollWidth,
        contentHeight: document.documentElement.scrollHeight,
        horizontalOverflow: document.documentElement.scrollWidth > document.documentElement.clientWidth + 1,
        spanishTextSamples: [...new Set((document.body.innerText || "").split(/\\r?\\n/)
          .map(value => value.trim()).filter(value => value.length > 2 && value.toUpperCase() !== "LANGUAGE / IDIOMA")
          .filter(value => /[¿¡áéíóúñ]|\\b(ajustes|armas|buscar|cantidad|captura|componentes|disponible|idioma|inventario|objetivos|privacidad|recursos|reliquias|soporte)\\b/i.test(value)))]
          .slice(0, 30)
      }))()`);
      const reportTarget = path.resolve(layoutReportPath);
      await mkdir(path.dirname(reportTarget), { recursive: true });
      await writeFile(reportTarget, JSON.stringify(metrics, null, 2), "utf8");
      log(`Informe de diseño QA guardado en ${reportTarget}.`);
    }
    if (screenshotPath) {
      const target = path.resolve(screenshotPath);
      await mkdir(path.dirname(target), { recursive: true });
      const captured = await mainWindow.webContents.capturePage();
      const image = qaSize
        ? captured.resize({ width: qaSize.width, height: qaSize.height, quality: "best" })
        : captured;
      const bytes = /\.jpe?g$/i.test(target) ? image.toJPEG(78) : image.toPNG();
      await writeFile(target, bytes);
      log(`Captura QA guardada en ${target}.`);
    }
    app.quit();
  }
}

async function offerCmpIfRequired(): Promise<void> {
  try {
    if (!await app.overwolf.isCMPRequired() || !mainWindow)
      return;
    const result = await dialog.showMessageBox(mainWindow, {
      type: "info",
      title: "Privacidad de Overwolf",
      message: "Overwolf necesita que revises sus preferencias de privacidad.",
      detail: "Warframe Tracker abrirá la plataforma de consentimiento oficial de Overwolf.",
      buttons: ["Configurar ahora", "Continuar"],
      defaultId: 0,
      cancelId: 1,
      noLink: true
    });
    if (result.response === 0)
      await app.overwolf.openCMPWindow();
  } catch (error) {
    log("No se pudo consultar la configuración CMP de Overwolf", error);
  }
}

function inventoryFromUpdate(data: unknown): unknown | undefined {
  if (!data || typeof data !== "object")
    return undefined;
  const value = data as Record<string, unknown>;
  if (value.key === "inventory")
    return value.value ?? value.data;
  const info = value.info as Record<string, unknown> | undefined;
  const matchInfo = info?.match_info as Record<string, unknown> | undefined;
  return matchInfo?.inventory;
}

async function submitInventory(payload: unknown, source: string): Promise<void> {
  if (payload === undefined || payload === null)
    return;
  const inventoryJson = typeof payload === "string" ? payload : JSON.stringify(payload);
  const digest = createHash("sha256").update(inventoryJson).digest("hex");
  if (digest === lastInventoryDigest || pendingInventoryDigests.has(digest))
    return;
  pendingInventoryDigests.add(digest);
  try {
    const response = await fetch(`${backendUrl}/api/desktop-bridge/inventory`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        "X-Warframe-Bridge-Key": bridgeKey
      },
      body: JSON.stringify({ inventoryJson, source })
    });
    if (!response.ok) {
      const detail = await response.text();
      throw new Error(`El puente rechazó la captura (${response.status}): ${detail}`);
    }
    const receipt = await response.json() as { distinctItems?: number };
    lastInventoryDigest = digest;
    log(`Inventario recibido: ${receipt.distinctItems ?? 0} objetos distintos.`);
  } finally {
    pendingInventoryDigests.delete(digest);
  }
}

async function inspectCurrentInfo(gep: any): Promise<void> {
  try {
    const current = await gep.getInfo(WARFRAME_GAME_ID);
    const inventory = inventoryFromUpdate(current);
    if (inventory !== undefined)
      await submitInventory(inventory, "overwolf-gep-current");
  } catch (error) {
    log("No se pudo consultar el estado GEP actual", error);
  }
}

function stopInventoryPolling(): void {
  if (inventoryPollTimer)
    clearInterval(inventoryPollTimer);
  inventoryPollTimer = undefined;
  inventoryPollInFlight = false;
}

function startInventoryPolling(gep: any): void {
  stopInventoryPolling();
  inventoryPollTimer = setInterval(() => {
    if (inventoryPollInFlight)
      return;
    inventoryPollInFlight = true;
    void inspectCurrentInfo(gep).finally(() => {
      inventoryPollInFlight = false;
    });
  }, INVENTORY_POLL_INTERVAL_MS);
}

function initializeGep(): void {
  app.overwolf.packages.on("ready", async (_event: unknown, name: string, version: string) => {
    if (name !== "gep")
      return;
    const gep: any = app.overwolf.packages.gep;
    if (!gep) {
      log("El paquete GEP informó ready, pero su API no está disponible.");
      return;
    }
    log(`GEP ${version} listo.`);
    gep.removeAllListeners();
    gep.on("game-detected", async (event: { enable(): void }, gameId: number, name: string) => {
      if (gameId !== WARFRAME_GAME_ID)
        return;
      log(`Warframe detectado (${name}).`);
      event.enable();
      try {
        await gep.setRequiredFeatures(gameId, REQUIRED_FEATURES);
        await inspectCurrentInfo(gep);
        startInventoryPolling(gep);
      } catch (error) {
        log("No se pudieron activar las funciones GEP", error);
      }
    });
    gep.on("game-exit", (_event: unknown, gameId: number) => {
      if (gameId !== WARFRAME_GAME_ID)
        return;
      stopInventoryPolling();
      log("Warframe se cerró; sondeo de inventario detenido.");
    });
    gep.on("new-info-update", (_event: unknown, gameId: number, data: unknown) => {
      if (gameId !== WARFRAME_GAME_ID)
        return;
      const inventory = inventoryFromUpdate(data);
      if (inventory !== undefined)
        void submitInventory(inventory, "overwolf-gep-update")
          .catch((error) => log("Error procesando inventario GEP", error));
    });
    gep.on("error", (_event: unknown, gameId: number, error: unknown) => {
      if (gameId === WARFRAME_GAME_ID)
        log("GEP informó un error para Warframe", error);
    });
    gep.on("elevated-privileges-required",
      (_event: unknown, gameId: number) => {
        if (gameId === WARFRAME_GAME_ID)
          log("Warframe requiere que la app se ejecute con el mismo nivel de privilegios.");
      });
  });
}

async function importSimulationIfRequested(): Promise<void> {
  const prefix = "--simulate-inventory=";
  const argument = process.argv.find((value) => value.startsWith(prefix));
  if (!argument)
    return;
  const filePath = path.resolve(argument.slice(prefix.length));
  const payload = await readFile(filePath, "utf8");
  await submitInventory(payload, "development-simulator");
}

async function bootstrap(): Promise<void> {
  initializeDesktopIpc();
  await initializeHotkey();
  await startBackend();
  await createWindow();
  await offerCmpIfRequired();
  await importSimulationIfRequested();
}

if (!app.requestSingleInstanceLock()) {
  app.quit();
} else {
  app.on("second-instance", () => {
    if (!mainWindow)
      return;
    if (mainWindow.isMinimized())
      mainWindow.restore();
    mainWindow.show();
    mainWindow.focus();
  });
  initializeGep();
  app.whenReady().then(() => bootstrap()).catch(async (error) => {
    log("No se pudo iniciar la aplicación", error);
    await dialog.showMessageBox({
      type: "error",
      title: "Warframe Tracker no pudo iniciar",
      message: "No fue posible iniciar la aplicación de escritorio.",
      detail: error instanceof Error ? error.message : String(error)
    });
    app.quit();
  });
}

app.on("before-quit", () => {
  quitting = true;
  stopInventoryPolling();
  globalShortcut.unregisterAll();
  if (backend && backend.exitCode === null)
    backend.kill();
});

app.on("will-quit", () => {
  quitting = true;
  stopInventoryPolling();
});

app.on("window-all-closed", () => app.quit());
