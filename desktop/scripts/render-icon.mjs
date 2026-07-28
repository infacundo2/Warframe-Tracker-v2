import { mkdir, readFile, writeFile } from "node:fs/promises";
import { Resvg } from "@resvg/resvg-js";
import pngToIco from "png-to-ico";

const source = new URL("../assets/icon.svg", import.meta.url);
const outputDirectory = new URL("../build/", import.meta.url);
const pngPath = new URL("icon.png", outputDirectory);
const icoPath = new URL("icon.ico", outputDirectory);

await mkdir(outputDirectory, { recursive: true });
const svg = await readFile(source);
const renderer = new Resvg(svg, {
  fitTo: { mode: "width", value: 512 },
  background: "rgba(0, 0, 0, 0)"
});
await writeFile(pngPath, renderer.render().asPng());
await writeFile(icoPath, await pngToIco(pngPath));
