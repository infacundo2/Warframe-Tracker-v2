import { cp, mkdir, readFile, writeFile } from "node:fs/promises";
import { Resvg } from "@resvg/resvg-js";

const root = new URL("../", import.meta.url);
const dist = new URL("../dist/", import.meta.url);
await cp(new URL("../public/", import.meta.url), dist, { recursive: true });
await cp(new URL("../manifest.json", import.meta.url), new URL("manifest.json", dist));
await mkdir(new URL("assets/", dist), { recursive: true });

const svg = await readFile(new URL("../assets/icon-native.svg", import.meta.url), "utf8");
const grayscaleSvg = svg
  .replaceAll("#67e7ff", "#d4dadd")
  .replaceAll("#dbb85c", "#929a9e")
  .replaceAll("#1d5365", "#5d686d");
const render = (width, grayscale = false) => new Resvg(grayscale ? grayscaleSvg : svg, {
  fitTo: { mode: "width", value: width },
  background: "rgba(0,0,0,0)"
}).render().asPng();

function createPngIco(images) {
  const directorySize = 6 + images.length * 16;
  const totalSize = directorySize + images.reduce((sum, image) => sum + image.data.length, 0);
  const output = new Uint8Array(totalSize);
  const view = new DataView(output.buffer);
  view.setUint16(0, 0, true);
  view.setUint16(2, 1, true);
  view.setUint16(4, images.length, true);
  let offset = directorySize;
  images.forEach((image, index) => {
    const entry = 6 + index * 16;
    output[entry] = image.width === 256 ? 0 : image.width;
    output[entry + 1] = image.width === 256 ? 0 : image.width;
    view.setUint16(entry + 4, 1, true);
    view.setUint16(entry + 6, 32, true);
    view.setUint32(entry + 8, image.data.length, true);
    view.setUint32(entry + 12, offset, true);
    output.set(image.data, offset);
    offset += image.data.length;
  });
  return output;
}

const color256 = render(256);
const gray256 = render(256, true);
const icon16 = render(16);
const icon32 = render(32);
const icon48 = render(48);
const icon16Path = new URL("assets/icon-16.png", dist);
const icon32Path = new URL("assets/icon-32.png", dist);
const icon48Path = new URL("assets/icon-48.png", dist);
await writeFile(new URL("assets/IconMouseOver.png", dist), color256);
await writeFile(new URL("assets/IconMouseNormal.png", dist), gray256);
await writeFile(new URL("assets/WindowIcon.png", dist), color256);
await writeFile(icon16Path, icon16);
await writeFile(icon32Path, icon32);
await writeFile(icon48Path, icon48);
await writeFile(new URL("assets/launcher_icon.ico", dist), createPngIco([
  { width: 16, data: icon16 }, { width: 32, data: icon32 },
  { width: 48, data: icon48 }, { width: 256, data: color256 }
]));
