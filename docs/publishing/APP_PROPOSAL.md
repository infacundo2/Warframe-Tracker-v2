# App proposal: Warframe Tracker

## One-line pitch

Warframe Tracker is a Spanish-first visible desktop companion that turns the
player's local Warframe inventory into an intuitive farming planner, collection
tracker, relic assistant, and build preparation dashboard.

## User problem

Warframe players own hundreds of equipment items, components, mods, resources,
and refined relic variants. Maintaining those quantities manually is slow and
causes planning tools to become outdated. Existing public profile links expose
only a small subset of this inventory.

Most established Warframe companion tools present their interfaces and
documentation primarily in English. This creates unnecessary friction for
Spanish-speaking players, especially when they need to understand relic
refinements, component relationships, drop probabilities, farming locations,
and inventory states. Warframe Tracker is designed in Spanish from the start,
using familiar terminology, clear explanations, guided actions, and visual
filters instead of requiring the player to translate every workflow.

The primary audience is the Spanish-speaking Warframe community, particularly
players in Latin America. The goal is to make inventory tracking and farming
planning feel approachable even for users who have never used an external
Warframe tool before.

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

The interface is currently in Spanish and emphasizes an intuitive progression:
select an objective, see the missing components, find the related relics, check
owned refinements, and choose a suitable farming route. Technical identifiers
remain internal whenever possible, while the player sees understandable names,
status labels, probabilities, and recommendations.

## Initial testing and availability

We are requesting development GEP access primarily so the developer can validate
the complete inventory flow with a small initial QA group composed of the
developer and a few friends who actively play Warframe. This first group will
help compare captured quantities with the real in-game inventory, identify
Spanish terminology issues, and verify that partial snapshots never remove
valid local data.

This is only the initial testing stage, not a permanently private bridge. The
application always has a complete visible desktop interface and is intended to
remain publicly available once the capture flow is stable. If other players
find it useful, they will be welcome to download and use it as well. Wider
adoption would be appreciated, but the immediate objective is a controlled,
safe, and useful test with real players rather than aggressive growth.

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

The app has a permanent, visible Spanish desktop interface and is not a
faceless or bridge-only application.

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
