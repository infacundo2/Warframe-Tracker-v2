import fs from "node:fs";
import path from "node:path";

const root = path.resolve("WarframeInventory/WarframeInventory");
const pack = JSON.parse(fs.readFileSync(path.join(root, "wwwroot/i18n/en.json"), "utf8").replace(/^\uFEFF/u, ""));
const files = [];
const walk = directory => {
  for (const entry of fs.readdirSync(directory, { withFileTypes: true })) {
    const full = path.join(directory, entry.name);
    if (entry.isDirectory()) walk(full);
    else if (/\.(razor|cs|js)$/iu.test(entry.name)) files.push(full);
  }
};
walk(path.join(root, "Pages"));
files.push(path.join(root, "Shared/MainLayout.razor"), path.join(root, "wwwroot/js/site.js"));

const normalize = value => value.replace(/&quot;/giu, '"').replace(/\s+/gu, " ").trim();
const spanishCue = /[áéíóúñü¿¡]|\b(?:abre|abrir|activos|ajustes|analizando|arma|armas|buscar|cantidad|captura|completado|construibles|disponible|eliminar|equipamiento|estado|faltan|filtro|guardar|idioma|inicia|inventario|mostrar|necesito|objetivo|objetivos|pendiente|planificar|prioridad|recompensa|reliquia|reliquias|reintentar|selecciona|sesión|tengo|todavía|vestigios)\b/iu;
const candidates = new Map();
const add = (value, file) => {
  const text = normalize(value);
  if (text.length < 2 || text.length > 500 || !spanishCue.test(text)) return;
  if (/[{};]/u.test(text) || text.startsWith("@")) return;
  if (!candidates.has(text)) candidates.set(text, new Set());
  candidates.get(text).add(path.relative(root, file).replaceAll("\\", "/"));
};

for (const file of files) {
  const source = fs.readFileSync(file, "utf8");
  for (const match of source.matchAll(/>([^<>]+)</gu)) add(match[1], file);
  for (const match of source.matchAll(/(?:Label|Placeholder|Title|AriaLabel|aria-label|title)\s*=\s*"([^"]+)"/giu)) add(match[1], file);
  for (const match of source.matchAll(/"([^"\r\n]+)"/gu)) add(match[1], file);
}

const translate = value => {
  if (typeof pack.translations[value] === "string") return pack.translations[value];
  for (const rule of pack.patterns ?? []) {
    const expression = new RegExp(rule.pattern, "u");
    if (expression.test(value)) return value.replace(expression, rule.replacement);
  }
  let result = value;
  for (const segment of pack.segments ?? []) result = result.replaceAll(segment.source, segment.target);
  return result;
};

const unresolved = [...candidates.entries()]
  .filter(([text]) => translate(text) === text)
  .sort(([left], [right]) => left.localeCompare(right, "es"));

for (const [text, sources] of unresolved)
  console.log(`${text}\t${[...sources].join(", ")}`);
const samples = new Map([
  ["Paso 2 de 4", "Step 2 of 4"],
  ["Tengo 0 · Necesito 1 · Faltan 1", "Owned 0 · Required 1 · Missing 1"],
  ["5 componentes pendientes · 27 rutas detectadas · 4 disponibles en tu inventario", "5 missing components · 27 routes found · 4 available in your inventory"],
  ["No disponible en la web", "Available only in the desktop app"],
  ["Objetivos del operador", "Operator Objectives"],
  ["Reliquia Neo Z10", "Relic Neo Z10"],
  ["Ranura 3", "Slot 3"]
]);
const failedSamples = [...samples].filter(([source, expected]) => translate(source) !== expected);
for (const [source, expected] of failedSamples)
  console.error(`SAMPLE FAILED: ${source} => ${translate(source)} (expected ${expected})`);
console.log(`UNRESOLVED=${unresolved.length}`);
console.log(`FAILED_SAMPLES=${failedSamples.length}`);
process.exitCode = unresolved.length || failedSamples.length ? 1 : 0;
