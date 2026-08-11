import { contextBridge, ipcRenderer } from "electron";

contextBridge.exposeInMainWorld("warframeDesktop", {
  getToggleHotkey: (): Promise<string> =>
    ipcRenderer.invoke("warframe:get-toggle-hotkey"),
  setToggleHotkey: (accelerator: string): Promise<boolean> =>
    ipcRenderer.invoke("warframe:set-toggle-hotkey", accelerator)
});

