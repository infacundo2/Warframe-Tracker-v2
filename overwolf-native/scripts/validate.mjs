import { access, readFile, stat } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

const dist = fileURLToPath(new URL("../dist/", import.meta.url));
const manifest = JSON.parse(await readFile(path.join(dist, "manifest.json"), "utf8"));
const errors = [];
const required = ["main.html", "js/main.js", "js/core.js", "css/native.css", "runtime-config.js",
  "assets/IconMouseOver.png", "assets/IconMouseNormal.png", "assets/WindowIcon.png", "assets/launcher_icon.ico"];
for (const file of required) {
  try { await access(path.join(dist, file)); } catch { errors.push(`Missing ${file}`); }
}
if (manifest.manifest_version !== 1 || manifest.type !== "WebApp") errors.push("Invalid manifest header.");
if (manifest.data?.start_window !== "main") errors.push("The visible main window must be the root window.");
if (!manifest.data?.game_events?.includes(8954)) errors.push("Warframe game_events ID 8954 is missing.");
if (!manifest.data?.game_targeting?.game_ids?.includes(8954)) errors.push("Warframe targeting ID 8954 is missing.");
if (!manifest.permissions?.includes("GameInfo")) errors.push("GameInfo permission is missing.");
for (const file of ["IconMouseOver.png", "IconMouseNormal.png", "WindowIcon.png"]) {
  const size = (await stat(path.join(dist, "assets", file))).size;
  if (size >= 30 * 1024) errors.push(`${file} is ${size} bytes; it must be under 30 KB.`);
}
const icoSize = (await stat(path.join(dist, "assets", "launcher_icon.ico"))).size;
if (icoSize >= 150 * 1024) errors.push(`launcher_icon.ico is ${icoSize} bytes; it must be under 150 KB.`);
const config = await readFile(path.join(dist, "runtime-config.js"), "utf8");
if (/OW_DEV_KEY|DB_PASS|password\s*[:=]/i.test(config)) errors.push("A credential-like value was found in runtime-config.js.");
if (errors.length) {
  console.error(errors.map(error => `- ${error}`).join("\n"));
  process.exit(1);
}
console.log("Overwolf Native package validation passed.");
