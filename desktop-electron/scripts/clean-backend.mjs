import { rm } from "node:fs/promises";

await rm(new URL("../../out/desktop-backend", import.meta.url), {
  recursive: true,
  force: true
});
