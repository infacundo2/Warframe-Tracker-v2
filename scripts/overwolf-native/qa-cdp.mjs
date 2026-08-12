import WebSocket from "../../overwolf-native/node_modules/ws/wrapper.mjs";
import { writeFile } from "node:fs/promises";

const [command, value = ""] = process.argv.slice(2);
const pages = await fetch("http://127.0.0.1:54284/json/list").then(response => response.json());
const page = pages.find(item => item.title === "Warframe Tracker");
if (!page) throw new Error("Warframe Tracker debug page was not found.");
const socket = new WebSocket(page.webSocketDebuggerUrl);
await new Promise((resolve, reject) => {
  socket.addEventListener("open", resolve, { once: true });
  socket.addEventListener("error", reject, { once: true });
});
let nextId = 1;
const pending = new Map();
const contexts = new Map();
socket.addEventListener("message", event => {
  const message = JSON.parse(event.data);
  if (message.id && pending.has(message.id)) {
    const pair = pending.get(message.id);
    pending.delete(message.id);
    message.error ? pair.reject(new Error(message.error.message)) : pair.resolve(message.result);
  }
  if (message.method === "Runtime.executionContextCreated") contexts.set(message.params.context.id, message.params.context);
  if (message.method === "Runtime.executionContextDestroyed") contexts.delete(message.params.executionContextId);
});
function send(method, params = {}) {
  const id = nextId++;
  return new Promise((resolve, reject) => {
    pending.set(id, { resolve, reject });
    socket.send(JSON.stringify({ id, method, params }));
  });
}
await send("Runtime.enable");
await send("Page.enable");
await new Promise(resolve => setTimeout(resolve, 350));
const trackerContext = () => [...contexts.entries()].find(([, context]) => context.auxData?.isDefault && context.auxData?.frameId !== page.id)?.[0];
const mainContext = () => [...contexts.entries()].find(([, context]) => context.auxData?.isDefault && context.auxData?.frameId === page.id)?.[0];

if (command === "eval" || command === "eval-main") {
  const contextId = command === "eval-main" ? mainContext() : trackerContext();
  if (!contextId) throw new Error("Tracker iframe context was not found.");
  const result = await send("Runtime.evaluate", { contextId, expression: value, awaitPromise: true, returnByValue: true });
  if (result.exceptionDetails) throw new Error(result.exceptionDetails.text);
  console.log(JSON.stringify(result.result.value));
} else if (command === "navigate") {
  const contextId = trackerContext();
  if (!contextId) throw new Error("Tracker iframe context was not found.");
  await send("Runtime.evaluate", { contextId, expression: `location.href=${JSON.stringify(value)}` });
  await new Promise(resolve => setTimeout(resolve, 2500));
  console.log(value);
} else if (command === "screenshot") {
  const result = await send("Page.captureScreenshot", { format: "png", fromSurface: true });
  await writeFile(value, Buffer.from(result.data, "base64"));
  console.log(value);
} else {
  throw new Error("Use: eval <expression>, navigate <url>, or screenshot <path>.");
}
socket.close();
