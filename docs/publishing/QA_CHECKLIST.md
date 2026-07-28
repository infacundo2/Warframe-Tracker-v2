# QA checklist

## Installation and startup

- [ ] Clean Windows 10 x64 installation.
- [ ] Clean Windows 11 x64 installation.
- [ ] Installer and executable have valid digital signatures.
- [ ] App starts without .NET, Node.js, or MySQL preinstalled.
- [ ] A second launch focuses the existing window.
- [ ] Closing the window terminates the local sidecar.
- [ ] No console window flashes during startup.

## Security

- [ ] Backend listens only on `127.0.0.1`.
- [ ] Inventory POST without bridge key returns 401.
- [ ] Inventory POST from a non-loopback address is rejected.
- [ ] Payloads larger than 20 MB are rejected.
- [ ] Malformed JSON does not alter the database.
- [ ] Raw payload is discarded immediately; normalized preview expires after 30 minutes.
- [ ] External links open in the default browser.
- [ ] Renderer has no Node integration.

## GEP

- [ ] Warframe game ID `8954` is detected.
- [ ] `game_info` and `match_info` are enabled immediately.
- [ ] Current info is queried after detection.
- [ ] New inventory updates are accepted.
- [ ] A GEP outage is explained to the user.
- [ ] Running Warframe as administrator produces a useful privilege warning.

## Inventory

- [ ] Built Warframes match the in-game inventory.
- [ ] Primary, secondary, melee, Archwing, Necramech, and companion equipment.
- [ ] Mod duplicate quantities.
- [ ] Relic counts for Intact, Exceptional, Flawless, and Radiant variants.
- [ ] Prime component quantities.
- [ ] Resources and recipes.
- [ ] Credits, Platinum, Endo, Ducats, and Aya when present in the payload.
- [ ] Unknown objects are omitted and reported.
- [ ] Partial captures never zero absent equipment.
- [ ] User confirmation is required.
- [ ] Data persists after restart.

## Store

- [ ] English description and screenshots.
- [ ] Public HTTPS privacy policy.
- [ ] Correct author, product name, version, and app UID.
- [ ] No claim of affiliation with Digital Extremes.
- [ ] Release notes and support link.
