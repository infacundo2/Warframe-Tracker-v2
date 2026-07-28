Warframe Tracker is a Spanish-first desktop companion that helps
Spanish-speaking players organize their inventory and plan their in-game goals.
Most existing Warframe tools are mainly in English, while our interface uses
clear Spanish terms, guided filters, relic probabilities, goals, resource
information, and suggested missions. It never automates gameplay or obtains
items for the player.

We request OW Electron GEP access for Warframe (game ID 8954), specifically
game_info and match_info.inventory. The player opens the in-game inventory, the
app receives the official snapshot, and a visible preview groups Warframes,
weapons, mods, relics, components, and resources. Nothing is saved until the
player confirms.

Initial testing will be limited to the developer and a small group of friends so
we can compare captured quantities with the real inventory and correct Spanish
terminology. This is not a private or faceless bridge: the app has a complete
visible interface and may be used publicly by anyone interested once testing is
stable.

Privacy is local-first: the raw JSON is discarded after parsing, confirmed data
is stored in local SQLite, and no Warframe credentials, memory reading, process
injection, packet interception, or inventory uploads are used. The initial
release has no ads, telemetry, paid features, or sale of user data.
