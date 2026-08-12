import { readFile, writeFile, mkdir } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { Resvg } from "@resvg/resvg-js";

const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(scriptDir, "../..");
const source = path.join(root, "docs/publishing/store-assets/src");
const target = path.join(root, "docs/publishing/store-assets");
await mkdir(target, { recursive: true });

for (const [input, output, format] of [
  ["hero.svg", "hero-258x198.png", "png"],
  ["creator-title.svg", "creator-title-400x320.png", "png"]
]) {
  const svg = await readFile(path.join(source, input), "utf8");
  const png = new Resvg(svg, { fitTo: { mode: "original" } }).render().asPng();
  await writeFile(path.join(target, output), png);
  console.log(`Created ${output} (${png.length} bytes, ${format})`);
}
