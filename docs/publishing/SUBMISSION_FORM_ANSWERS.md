# Overwolf Native MVP submission — copy-ready answers

## Approval confirmation

Yes. I have read the submission requirements, tested the MVP on Windows 11
x64, and understand that it must comply with Overwolf technical and game
compliance requirements and remain consistent with the previously approved app
idea.

## Does the app keep at least one desktop window open?

Yes. Warframe Tracker is an Overwolf Native application with a visible,
resizable desktop root window. Closing that root window closes the application.
The app is not a background-only process. The `Ctrl+Shift+T` hotkey hides or
restores the same window.

## Brief purpose

Warframe Tracker is an English-first, bilingual English/Spanish Warframe
companion. It organizes a player's confirmed collection and helps plan farming
with searchable Warframe, weapon, mod, relic, component and resource catalogs;
relic refinements and reward probabilities; goals; farming routes; comparisons;
lightweight builds; and Worldstate context. Spanish remains available as a
persistent language option for Spanish-speaking players.

## Framework

Overwolf Native (`WebApp`) using TypeScript, HTML and CSS for the visible shell,
Overwolf GEP for Warframe events, and an HTTPS ASP.NET Core 8 service for the
authenticated Tracker interface and confirmed cloud collection.

## Inventory feature and user control

Inventory is received only through Overwolf GEP `match_info.inventory`. The app
does not inspect Warframe memory, capture network traffic, automate gameplay,
modify game files, or request Warframe credentials. A raw snapshot is held
locally for no more than 30 minutes. Nothing is sent to the Tracker service
until the player presses **Send to Tracker for review**. The authenticated site
then shows a preview and requires a second explicit confirmation before
normalized collection data is stored.

## Specific instructions for every window/tab (under 2,000 characters)

Warframe Tracker opens one visible Native desktop window. Screenshots follow
this order.

1. **Native shell / Tracker:** Launch the app. The top status identifies whether
Warframe is detected. Use the GEP button to show or hide Automatic Inventory;
collapsing it does not stop capture. The right side contains the authenticated
Tracker website. Sign in or create a Tracker profile; these are Tracker
credentials, never Warframe credentials.
2. **Command Center:** Review collection totals, current goals, nearly completed
sets and farming priorities.
3. **Warframes / Weapons / Mods:** Open each section from navigation, type in the
search field, apply filters and open a detail page. Details show owned/mastered
state, components, related relics and acquisition methods.
4. **Relics:** Search or filter available/vaulted relics. One card represents a
relic family. Open it to edit Intact, Exceptional, Flawless and Radiant counts
and compare reward probabilities.
5. **Resources:** Search by resource, location, enemy or category and open a
resource to inspect farming locations and numerically sorted drops.
6. **Goals / Planner / Builds / Compare:** Add an equipment goal, inspect missing
components and useful owned relics, save a lightweight build, and type equipment
names into the comparison fields.
7. **Automatic inventory:** Start Tracker before Warframe. After sign-in or a
Relay/Dojo/mission loading screen, wait for INVENTORY CAPTURED. Press **Send to
Tracker for review**, inspect the preview, then explicitly apply it. Discard
removes the temporary local snapshot.
8. **Settings / Privacy / Support:** Test English/Spanish persistence, audio,
hotkey reminder, data handling and support links.

No advertising is included, so there is no ad container or reserved ad area.

## Testing completed

- Native TypeScript build, automated tests and official manifest-schema check.
- Visible root window and Warframe game ID `8954` targeting.
- Real GEP `game_info` and `match_info.inventory` capture on Windows 11 x64.
- Independent Intact, Exceptional, Flawless and Radiant relic quantities.
- Authentication, preview-before-apply, per-user isolation and duplicate handling.
- No development key, database password, session nonce or raw inventory is
  packaged in the OPK or evidence.

The manual QA result sheet and security report included with the submission
identify every test and any item that still requires reviewer reproduction.

## Distribution and monetization

The intended distribution is public and free. The MVP contains no advertising.
If monetization is added later, it will use only Overwolf-supported solutions,
consent handling and compliant placements after the privacy policy is updated.

## Public URLs

- Privacy: `https://infacundo2.github.io/Warframe-Tracker-v2/privacy.html`
- Terms: `https://infacundo2.github.io/Warframe-Tracker-v2/terms.html`
- Support: `https://infacundo2.github.io/Warframe-Tracker-v2/support.html`
- Source: `https://github.com/infacundo2/Warframe-Tracker-v2`

## Signing

This is an Overwolf Native OPK. It contains no separate executable or native
plugin and therefore does not require the OV/EV executable certificate that was
required by the abandoned OW-Electron distribution. Overwolf will validate and
distribute the approved Native package through its release channels.
