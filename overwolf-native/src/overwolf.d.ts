interface OverwolfResult { success?: boolean; status?: string; error?: string; }
interface OverwolfEvent<T> {
  addListener(listener: (value: T) => void): void;
  removeListener?(listener: (value: T) => void): void;
}
interface OverwolfWindowResult extends OverwolfResult {
  window?: { id: string; name?: string; isVisible?: boolean };
}
interface OverwolfGameInfo {
  isRunning?: boolean;
  classId?: number;
  id?: number;
  title?: string;
}
interface OverwolfGameInfoUpdate { gameInfo?: OverwolfGameInfo; }
interface OverwolfInfoUpdate { info?: unknown; feature?: string; category?: string; key?: string; value?: unknown; data?: unknown; }
interface OverwolfGetInfoResult extends OverwolfResult { res?: unknown; info?: unknown; }
interface Window {
  WarframeTrackerNativeConfig?: { trackerUrl?: string; allowedTrackerOrigin?: string };
}
declare const overwolf: {
  games: {
    getRunningGameInfo(callback: (result: OverwolfGameInfo | null) => void): void;
    onGameInfoUpdated: OverwolfEvent<OverwolfGameInfoUpdate>;
    events: {
      setRequiredFeatures(features: string[], callback: (result: OverwolfResult & { supportedFeatures?: string[] }) => void): void;
      getInfo(callback: (result: OverwolfGetInfoResult) => void): void;
      onInfoUpdates2: OverwolfEvent<OverwolfInfoUpdate>;
      onError: OverwolfEvent<unknown>;
    };
  };
  settings: {
    hotkeys: { onPressed: OverwolfEvent<{ name?: string }> };
  };
  windows: {
    getCurrentWindow(callback: (result: OverwolfWindowResult) => void): void;
    restore(windowId: string, callback?: (result: OverwolfResult) => void): void;
    hide(windowId: string, callback?: (result: OverwolfResult) => void): void;
    getWindowState(windowId: string, callback: (result: OverwolfResult & { window_state?: string }) => void): void;
  };
};
