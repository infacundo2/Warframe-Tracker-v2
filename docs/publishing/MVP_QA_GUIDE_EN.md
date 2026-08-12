# Warframe Tracker Native 0.1.2 — MVP reviewer guide

Warframe Tracker is an English-first Overwolf Native desktop companion with an
optional persistent Spanish language. It always exposes a visible root window
while running and uses Overwolf GEP—not process memory or network interception—
to receive Warframe inventory information.

## 1. Install and launch

1. Install the supplied `Warframe-Tracker-Native-0.1.2.opk` using the reviewer
   account authorized by Overwolf.
2. Launch **Warframe Tracker** from Overwolf or its desktop shortcut.
3. Confirm that the visible resizable window opens immediately. A loader is
   acceptable while the remote Tracker connects.
4. Confirm that the initial language is English and Spanish is selectable.
5. Sign in or create a Tracker profile. These are not Warframe credentials.

## 2. Visible Native shell

- The status at the top reports INITIALIZING, WARFRAME NOT DETECTED, CONNECTING
  TO GEP, GEP READY, INVENTORY CAPTURED or a visible error.
- Press **GEP** to expand/collapse Automatic Inventory. Capture must continue
  while collapsed.
- Test minimize, maximize/restore, header drag, header double-click and close.
- Press `Ctrl+Shift+T` twice to hide and restore the window.
- Closing the root window must end the app; no invisible app window should
  remain at `http://localhost:54284`.

## 3. Catalog and planning tour

1. **Command Center:** collection progress, goals and nearly completed sets.
2. **Warframes / Weapons / Mods:** type searches, apply filters and open details.
3. **Relics:** one card per family; verify independent I/E/F/R quantities and
   reward probabilities for every refinement.
4. **Resources:** search by resource, location, enemy or category; verify numeric
   drop ordering.
5. **Goals / Planner:** inspect missing components and useful owned relics.
6. **Buildable / Builds / Compare:** save a build, search mods and type both
   equipment names in Compare.
7. **Worldstate:** review current public activity.
8. **Settings / Privacy / Support:** test language persistence, sound controls,
   hotkey reminder and public legal/support links.

## 4. Real GEP inventory flow

1. Start Tracker before Warframe.
2. Start Warframe and sign in. Wait for **GEP READY**.
3. If no snapshot arrives, enter and leave a Relay, Dojo or mission.
4. Confirm **INVENTORY CAPTURED** and a non-zero detected-entry count.
5. Press **Send to Tracker for review**. The raw capture leaves the local shell
   only at this explicit action.
6. In the authenticated `/native-sync` page select **Find capture**, then
   **Analyze inventory**.
7. Review the summary. No collection value may change before confirmation.
8. Apply confirmed changes and verify Credits, Endo, Aya, Ducats, resources,
   relic refinements and mastered equipment when present in GEP.
9. Repeat Send: an identical capture must not create duplicate changes.

## 5. Failure and privacy tests

- Disconnect Internet before Send: display an offline/error state and retain the
  local snapshot for retry.
- Sign out of the embedded Tracker: delivery/preview must require authentication.
- Use a second Tracker user: neither user may see the other's capture.
- Press Discard: the temporary local snapshot disappears.
- Close Warframe: polling stops and the Native shell remains stable.
- Inspect CEF developer tools and OW logs: raw inventory JSON, developer keys,
  passwords and database credentials must be absent.
- Wait 30 minutes with a test snapshot: it must expire locally.

## 6. Display and performance matrix

Repeat the visible-shell and catalog smoke test at 1180×720, 1366×768,
1920×1080 and 2560×1440, including 100%, 125% and 150% Windows scaling where
available. Verify no horizontal overlap and that the GEP panel remains usable.
On a second monitor, move, maximize and restore the app.

Open Overwolf and Windows Task Managers. Navigate for ten minutes and record
idle/peak CPU, memory and network observations. The app must not freeze; memory
must settle after navigation. Record cold-start and warm-navigation times in
`NATIVE_QA_RESULTS.md`.

## 7. Evidence and expected external dependency

- `screenshots-native/` contains numbered Native screenshots.
- `NATIVE_QA_RESULTS.md` contains the signed manual result sheet.
- `reports/NATIVE_SECURITY_REPORT.md` contains hashes and local security checks.
- `RELEASE_NOTES_NATIVE_0.1.2.md` lists functionality and known limitations.

The authenticated interface is served over HTTPS. If the service is waking
from a free hosting instance, the Native shell must stay responsive and show a
loader or retry state rather than appearing frozen.
