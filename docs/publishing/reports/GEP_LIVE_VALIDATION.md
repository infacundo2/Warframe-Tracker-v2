# Anonymized live GEP validation

- Date: 2026-08-04 (America/Santiago)
- Operating system: Windows 11 x64
- Game: Warframe, Overwolf game ID `8954`
- GEP version observed: `400.22.0`
- Required features: `game_info`, `match_info`
- Trigger: sign-in/loading-screen transitions, including Relay travel
- Result: authoritative `match_info.inventory` snapshot received
- Distinct inventory item types: `2,406`
- UI flow: local preview generated, then applied only after user confirmation
- Refinement verification: `Neo S13 Radiant` was stored independently from its
  Intact, Exceptional, and Flawless variants
- Reliability change: current GEP information is checked every 2.5 seconds while
  Warframe is active; identical snapshots are deduplicated by SHA-256 digest;
  polling stops on game exit

## Privacy handling

This report intentionally excludes the player name, raw inventory payload,
temporary `OW_DEV_KEY`, local database, and any account credential. The key was
provided only through the test process environment and was not committed or
packaged. Warframe Tracker never asks for a Warframe password.

## Result

Pass. The MVP demonstrated real, user-controlled inventory capture through the
approved Overwolf GEP path and preserved refined relic variants independently.
