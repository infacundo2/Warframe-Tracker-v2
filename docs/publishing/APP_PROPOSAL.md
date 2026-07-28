# App proposal: Warframe Tracker

## One-line pitch

Warframe Tracker is a visible desktop companion that turns the player's local
Warframe inventory into a private farming planner, collection tracker, relic
assistant, and build preparation dashboard.

## User problem

Warframe players own hundreds of equipment items, components, mods, resources,
and refined relic variants. Maintaining those quantities manually is slow and
causes planning tools to become outdated. Existing public profile links expose
only a small subset of this inventory.

## Core experience

The player starts Warframe Tracker, launches Warframe, and opens the in-game
inventory. The app receives the official GEP inventory snapshot and presents a
clear preview grouped by Warframes, weapons, mods, relics, components, and
resources. Nothing is applied until the player confirms.

After confirmation the visible desktop app provides:

- collection progress and nearly completed Prime sets;
- relic quantities and refinements linked to required components;
- farming goals and recommended routes;
- resource and mod acquisition information;
- comparison and lightweight build planning;
- active Worldstate information crossed with local goals.

## Overwolf integration requested

- Platform: OW Electron.
- Game: Warframe.
- Game ID: `8954`.
- Package: `gep`.
- Required features: `game_info`, `match_info`.
- Required info item: `match_info.inventory`.
- Optional info item: `match_info.highlighted`, for future contextual navigation.
- Optional `game_info.username`, only to label the local profile after explicit
  consent.

The app has a permanent, visible desktop window and is not a private,
faceless, or bridge-only application.

## Privacy and security

- The ASP.NET sidecar binds only to an ephemeral `127.0.0.1` port.
- Electron and the sidecar authenticate with a per-launch random 256-bit key.
- The raw inventory JSON is discarded immediately after parsing.
- Only a normalized preview remains in memory for at most 30 minutes.
- Only normalized item identifiers, quantities, and selected account totals are
  persisted in a local SQLite database.
- No Warframe credentials are requested or stored.
- No game memory, process injection, packet interception, or private game files
  are accessed.
- No inventory data is uploaded to our servers.
- Users can inspect a preview and cancel without writing changes.

## Monetization

The initial release has no advertising, telemetry, paid features, or sale of
user data.

## Project status

The existing web interface is functional. The OW Electron shell, authenticated
loopback bridge, full inventory parser, preview workflow, SQLite desktop
storage, simulator, and packaging configuration are implemented. Production
GEP validation is pending Overwolf approval and credentials.
