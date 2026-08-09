namespace WarframeNativeCore {
  export const MAX_INVENTORY_BYTES = 20 * 1024 * 1024;
  export const CAPTURE_LIFETIME_MS = 30 * 60 * 1000;

  export function inventoryFromUpdate(input: unknown): unknown | undefined {
    if (!input || typeof input !== "object") return undefined;
    const value = input as Record<string, unknown>;
    if (value.key === "inventory") return value.value ?? value.data;
    const directMatch = value.match_info as Record<string, unknown> | undefined;
    if (directMatch?.inventory !== undefined) return directMatch.inventory;
    const info = value.info as Record<string, unknown> | undefined;
    const matchInfo = info?.match_info as Record<string, unknown> | undefined;
    if (matchInfo?.inventory !== undefined) return matchInfo.inventory;
    const nestedInfo = (value.res as Record<string, unknown> | undefined)?.info as Record<string, unknown> | undefined;
    const nestedMatch = nestedInfo?.match_info as Record<string, unknown> | undefined;
    return nestedMatch?.inventory;
  }

  export function normalizeInventory(payload: unknown): string {
    const json = typeof payload === "string" ? payload : JSON.stringify(payload);
    const bytes = new TextEncoder().encode(json).byteLength;
    if (bytes < 2) throw new Error("The inventory capture is empty.");
    if (bytes > MAX_INVENTORY_BYTES) throw new Error("The inventory capture exceeds the 20 MB safety limit.");
    JSON.parse(json);
    return json;
  }

  export function safeTrackerUrl(raw: string): URL | null {
    try {
      const url = new URL(raw);
      const local = url.hostname === "127.0.0.1" || url.hostname === "localhost";
      if (url.protocol !== "https:" && !(local && url.protocol === "http:")) return null;
      url.username = "";
      url.password = "";
      url.hash = "";
      return url;
    } catch {
      return null;
    }
  }

  export function countDistinctItems(json: string): number {
    const matches = json.match(/"ItemType"\s*:/g);
    return matches?.length ?? 0;
  }
}
