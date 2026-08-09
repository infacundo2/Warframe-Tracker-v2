import { readFile } from "node:fs/promises";
import Ajv from "ajv";

const schemaUrl = "https://raw.githubusercontent.com/overwolf/community-gists/master/overwolf-manifest-schema.json";
const response = await fetch(schemaUrl);
if (!response.ok) throw new Error(`Official manifest schema download failed (${response.status}).`);
const schema = await response.json();
const manifest = JSON.parse(await readFile(new URL("../dist/manifest.json", import.meta.url), "utf8"));
const ajv = new Ajv({ allErrors: true, strict: false });
const validate = ajv.compile(schema);
if (!validate(manifest)) {
  console.error(validate.errors);
  process.exit(1);
}
console.log("Official Overwolf manifest schema validation passed.");
