import { app, BrowserWindow, dialog, shell } from "electron";
import { ChildProcess, spawn } from "node:child_process";
import { randomBytes } from "node:crypto";
import { readFile } from "node:fs/promises";
import { createServer } from "node:net";
import path from "node:path";

const WARFRAME_GAME_ID = 8954;
const REQUIRED_FEATURES = ["game_info", "match_info"];
const HEALTH_TIMEOUT_MS = 60_000;
const bridgeKey = randomBytes(32).toString("base64url");

let backend: ChildProcess | undefined;
let mainWindow: BrowserWindow | undefined;
let backendUrl = "";
let quitting = false;

function log(message: string, error?: unknown): void {
  const suffix = error instanceof Error ? `: ${error.message}` : error ? `: ${String(error)}` : "";
  console.log(`[Warframe Tracker] ${message}${suffix}`);
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
  mainWindow = new BrowserWindow({
    width: 1500,
    height: 930,
    minWidth: 1040,
    minHeight: 700,
    show: false,
    backgroundColor: "#050b12",
    autoHideMenuBar: true,
    webPreferences: {
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
  await mainWindow.loadURL(`${backendUrl}/desktop-sync`);
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
  log(`Inventario recibido: ${receipt.distinctItems ?? 0} objetos distintos.`);
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
      } catch (error) {
        log("No se pudieron activar las funciones GEP", error);
      }
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
  await startBackend();
  await createWindow();
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
  if (backend && backend.exitCode === null)
    backend.kill();
});

app.on("window-all-closed", () => app.quit());
