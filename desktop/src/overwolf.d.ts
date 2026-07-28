import type { OverwolfGameEventPackage } from "@overwolf/ow-electron-packages-types";

declare module "electron" {
  interface App {
    overwolf: {
      packages: NodeJS.EventEmitter & {
        gep?: OverwolfGameEventPackage;
      };
    };
  }
}
