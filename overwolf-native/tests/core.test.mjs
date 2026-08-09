import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import vm from "node:vm";
import test from "node:test";

const source = await readFile(new URL("../dist/js/core.js", import.meta.url), "utf8");
const context = { URL, TextEncoder };
vm.createContext(context);
vm.runInContext(source, context);
const core = context.WarframeNativeCore;

test("extracts inventory from native info updates and getInfo responses", () => {
  assert.deepEqual(core.inventoryFromUpdate({ key: "inventory", value: { Slots: 8 } }), { Slots: 8 });
  assert.deepEqual(core.inventoryFromUpdate({ info: { match_info: { inventory: { Credits: 12 } } } }), { Credits: 12 });
  assert.deepEqual(core.inventoryFromUpdate({ res: { info: { match_info: { inventory: { Aya: 2 } } } } }), { Aya: 2 });
});

test("accepts HTTPS and development loopback URLs only", () => {
  assert.equal(core.safeTrackerUrl("https://tracker.example.com/native-sync").origin, "https://tracker.example.com");
  assert.equal(core.safeTrackerUrl("http://127.0.0.1:43127").origin, "http://127.0.0.1:43127");
  assert.equal(core.safeTrackerUrl("http://tracker.example.com"), null);
  assert.equal(core.safeTrackerUrl("javascript:alert(1)"), null);
});

test("normalizes valid JSON and rejects malformed captures", () => {
  assert.equal(core.normalizeInventory({ MiscItems: [] }), '{"MiscItems":[]}');
  assert.throws(() => core.normalizeInventory("not-json"));
});
