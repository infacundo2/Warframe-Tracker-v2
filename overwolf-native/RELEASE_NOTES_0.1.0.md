# Warframe Tracker Native 0.1.0 — Review build

## Included

- Visible Windows desktop window built on Overwolf Native/CEF.
- English-first interface with the existing English/Spanish Tracker embedded.
- Warframe GEP registration for `game_info` and `match_info`.
- Automatic `match_info.inventory` capture through `onInfoUpdates2` and
  `getInfo()` polling.
- Local 30-minute capture retention, SHA-256 duplicate detection and 20 MB
  payload limit.
- Explicit user action before transmitting a capture.
- Authenticated server-side preview before any inventory changes are applied.
- Per-user capture isolation, rate limiting and transactional application.
- Configurable `Ctrl+Shift+T` Overwolf hotkey.
- Localhost development workflow and automated OPK packaging.

## Privacy and security

- No Warframe credentials are requested or stored.
- No process memory, packets or game files are read directly.
- Raw inventory JSON is never written to logs.
- No database credentials, OW development keys or private certificates are
  included in the OPK.
- No ads or reserved ad container are present in this MVP.

## Known review prerequisites

- Overwolf must confirm transfer of the approved Warframe GEP access to Native.
- Replace the localhost Tracker URL with the final HTTPS deployment before
  submitting the production OPK.
- Perform real GEP QA with the final App UID and production/testing channel.
