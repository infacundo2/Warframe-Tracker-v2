# Warframe Tracker 0.1.0 — MVP reviewer guide

This document explains every major feature required to review the first public
MVP. The application opens a visible desktop window, uses English by default,
and includes a persistent Spanish language option.

## Install and first launch

1. On Windows 11 x64, run `Warframe-Tracker-Setup-0.1.0.exe`.
2. Complete the installer and open **Warframe Tracker** from the desktop or
   Start menu.
3. Confirm that the main desktop window remains visible while the app runs.
4. Review the onboarding pages. They explain local storage, safe inventory
   capture, and privacy before profile creation.
5. Create a local profile. No Warframe password is requested.

The MVP installer is intentionally unsigned until Overwolf supplies the
production Console identifiers and package-signing access. Windows SmartScreen
may therefore display an unknown-publisher warning on this review build.

## Core feature tour

1. **Command Center:** collection progress, current goals, almost-complete sets,
   and useful owned relics.
2. **Warframes / Weapons / Mods:** searchable catalog and detail pages with
   owned quantities and acquisition references.
3. **Relics:** one card per relic family; quantities remain independent for
   Intact, Exceptional, Flawless, and Radiant refinements. The detail page shows
   reward probabilities for the selected refinement.
4. **Resources:** searchable acquisition locations, enemy drops, and numeric
   probability sorting.
5. **Goals and Planner:** select a target and review missing components,
   related relics, owned refinements, and recommended farming routes.
6. **Buildable / Builds / Compare:** inspect constructible sets, save lightweight
   configurations, and type directly into comparison selectors.
7. **Worldstate:** current public game activity used as farming context.
8. **Settings:** switch between English and Spanish, configure audio, and test
   the global show/hide shortcut.
9. **Privacy / Support:** available from inside the visible desktop window.

## Safe inventory capture

The real flow was validated on August 4, 2026 with Warframe game ID `8954` and
Overwolf GEP 400.22.0. It received an authoritative snapshot containing 2,406
distinct item types. The submitted evidence deliberately excludes the raw
payload, player identifier, and temporary developer key.

To reproduce it in an authorized development environment:

1. Set a reviewer-owned temporary `OW_DEV_KEY` in the process environment.
2. Start Tracker in OW-Electron development mode **before** starting Warframe.
3. Start Warframe and sign in.
4. Trigger a loading screen by entering or leaving a Relay, Dojo, or mission.
5. Open **Account Sync**, then select **Find capture**.
6. Inspect the preview. No collection values change at this stage.
7. Select **Apply confirmed changes** only after reviewing the summary.

Tracker listens for GEP updates and also checks current game information every
2.5 seconds while Warframe is active. Polling stops when the game exits.

## Privacy and deletion test

1. Open **Privacy** and verify that local processing and optional Overwolf
   telemetry are explained.
2. Open **Support** and follow the local-data deletion instructions.
3. Confirm that no password, raw GEP payload, or developer key is displayed.
4. Public pages:
   - Privacy: `https://infacundo2.github.io/Warframe-Tracker-v2/privacy.html`
   - Terms: `https://infacundo2.github.io/Warframe-Tracker-v2/terms.html`
   - Support: `https://infacundo2.github.io/Warframe-Tracker-v2/support.html`

## Display and language checks

Automated screenshots and layout reports cover 1366×720, 1366×768, 1920×1080,
2560×1440, and 3840×2160. Reports must show `horizontalOverflow: false`.
English screenshots cover the primary review flow; the Spanish screenshot proves
that the optional language pack remains available and persists after restart.

## Expected limitations of this review build

- GEP works in local developer mode with an authorized temporary key.
- The distributable build must receive Overwolf package signing and a trusted
  code-signing certificate before public store distribution.
- The app never automates gameplay and never writes to Warframe.
- Game data and Worldstate availability depend on their upstream sources.

## Evidence map

- `screenshots/`: feature and onboarding screenshots.
- `reports/GEP_LIVE_VALIDATION.md`: anonymized real-capture evidence.
- `reports/windows11-qa.txt`: Windows and display verification.
- `reports/`: language and resolution layout results.
- `RELEASE_NOTES_0.1.0.md`: build changes and known limitations.
- `SECURITY_BUILD_NOTES.md`: privacy, scanning, and signing status.
