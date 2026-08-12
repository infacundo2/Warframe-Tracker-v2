import WebSocket from "../../overwolf-native/node_modules/ws/wrapper.mjs";

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
    const { resolve, reject } = pending.get(message.id);
    pending.delete(message.id);
    if (message.error) reject(new Error(message.error.message)); else resolve(message.result);
  }
  if (message.method === "Runtime.executionContextCreated") {
    const context = message.params.context;
    contexts.set(context.id, context);
  }
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
await new Promise(resolve => setTimeout(resolve, 500));
for (const [id, context] of contexts) {
  try {
    const result = await send("Runtime.evaluate", {
      contextId: id,
      expression: `JSON.stringify({title:document.title,url:location.href,lang:document.documentElement.lang,text:document.body.innerText.slice(0,2500),links:Array.from(document.querySelectorAll('a')).slice(0,80).map(a=>({text:a.innerText.trim(),href:a.href}))})`,
      returnByValue: true
    });
    console.log(JSON.stringify({ contextId: id, contextName: context.name, auxData: context.auxData, document: JSON.parse(result.result.value) }, null, 2));
  } catch (error) {
    console.error(`Context ${id}: ${error.message}`);
  }
}
socket.close();
