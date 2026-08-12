# Warframe Tracker Native 0.1.2

## First Native MVP review build

- Visible, resizable Overwolf Native root window with minimize,
  maximize/restore, close and `Ctrl+Shift+T` controls.
- Official Warframe GEP targeting for game ID `8954`, using `game_info` and
  `match_info.inventory`.
- Automatic inventory capture with SHA-256 duplicate detection, a 20 MB input
  limit and temporary IndexedDB retention of at most 30 minutes.
- Explicit two-step consent: Send to the authenticated Tracker, then preview and
  Apply confirmed changes.
- Collapsible GEP panel that continues capturing while hidden.
- English-first interface with persistent Spanish selection.
- Searchable Warframe, weapon, mod, relic and resource catalogs; goals,
  farming planner, builds, comparison and Worldstate.
- Independent Intact, Exceptional, Flawless and Radiant relic quantities.
- Cloud-backed collection separated per authenticated Tracker user.
- No advertising in this MVP.

## Privacy and compliance

- Does not read Warframe process memory, intercept network traffic, modify game
  files or automate gameplay.
- Does not request Warframe email, password, 2FA code or session credentials.
- Raw inventory is not logged. It is transmitted only after the player presses
  Send and is discarded after parsing; normalized confirmed collection data is
  stored in the Tracker service.
- No development key, database secret, executable or native plugin is included
  in the OPK.

## Known external dependency

The authenticated Tracker uses an HTTPS hosted service. A cold free-host wakeup
can delay the embedded interface; the Native controller remains visible while
the service connects. Performance measurements and retry/offline behavior are
included in the QA result sheet.
