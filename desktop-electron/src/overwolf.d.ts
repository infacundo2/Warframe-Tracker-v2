import type { OverwolfGameEventPackage } from "@overwolf/ow-electron-packages-types";

declare module "electron" {
  interface App {
    overwolf: {
      isCMPRequired(): Promise<boolean>;
      openCMPWindow(): Promise<void>;
      packages: NodeJS.EventEmitter & {
        gep?: OverwolfGameEventPackage;
      };
    };
  }
}
