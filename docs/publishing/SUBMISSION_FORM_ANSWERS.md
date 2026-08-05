# Overwolf MVP submission — copy-ready answers

## Approval confirmation

Yes. I have read the submission requirements, tested the MVP on Windows 11
x64, and understand that it must comply with Overwolf technical and game
compliance requirements and remain consistent with the previously approved app
idea.

## Does the app keep at least one desktop window open?

Yes. Warframe Tracker is a visible OW-Electron desktop application. Its main
window remains open while the app is running and can be shown or hidden with a
configurable global shortcut. It is not a background-only process.

## App description

Warframe Tracker is an English-first, bilingual (English/Spanish) Warframe
companion for privately organizing collection progress and planning farming.
It provides Warframe, weapon, mod, relic, component, and resource catalogs;
relic refinements and reward probabilities; goals; farming routes; comparisons;
lightweight builds; and Worldstate context. Spanish is available as a persistent
language option, making the tracker more intuitive for Spanish-speaking players.

## Inventory feature and user control

The desktop app receives inventory information only through Overwolf GEP. It
does not inspect game memory, parse network traffic, automate gameplay, modify
game files, or request Warframe credentials. A received snapshot is normalized
locally and shown as a preview. The user must explicitly confirm before any
collection changes are stored.

## Testing completed

- Windows 11 x64 installation and visible-window behavior.
- English default interface and persistent Spanish language selection.
- Automated layout checks at 1366×720, 1366×768, 1920×1080, 2560×1440, and
  3840×2160.
- Real Warframe GEP capture on August 4, 2026: GEP 400.22.0, game ID 8954,
  `game_info` and `match_info`, authoritative snapshot of 2,406 distinct item
  types, preview, and user-confirmed local application.
- Independent Intact, Exceptional, Flawless, and Radiant relic quantities.
- Local privacy/deletion flow, global shortcut, clean shutdown, and restart.
- Dependency vulnerability checks and Microsoft Defender scan are included in
  the delivery reports.

The evidence contains no raw inventory payload, player identifier, password, or
developer key.

## Reviewer instructions

Install the included Windows x64 build and follow `MVP_QA_GUIDE_EN.md`. To repeat
GEP capture from source, start Tracker in OW-Electron dev mode with a
reviewer-owned temporary development key before opening Warframe, then trigger a
loading screen by entering or leaving a Relay, Dojo, or mission. Open Account
Sync, review the preview, and confirm only if desired.

## Distribution and monetization

The intended distribution is public and free so any player may use the app. The
MVP displays no ads. If monetization is later required or approved, it will use
only Overwolf-supported solutions, consent handling, and placements that comply
with Overwolf and game requirements. No third-party advertising SDK is included.

## Public URLs

- Privacy Policy: `https://infacundo2.github.io/Warframe-Tracker-v2/privacy.html`
- Terms of Use: `https://infacundo2.github.io/Warframe-Tracker-v2/terms.html`
- Support: `https://infacundo2.github.io/Warframe-Tracker-v2/support.html`
- Source: `https://github.com/infacundo2/Warframe-Tracker-v2`

## Signing disclosure

This review MVP is unsigned because the Overwolf Console UID/API/build key and
production package signature are issued after MVP approval. A trusted Windows
code-signing certificate will also be applied before public distribution. The
review build may display a Windows unknown-publisher warning; it must not be
published as the final store binary in this state.
