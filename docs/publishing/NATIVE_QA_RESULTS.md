# Warframe Tracker Native 0.1.2 — QA result sheet

Tester: Codex automated audit / manual checks pending Abraham  
Date: 2026-08-12  
Windows: Windows 11 Home Single Language x64, build 26200  
Overwolf: 0.305.0.9 observed during Native development  
Warframe: current live client; exact build not recorded  
GEP: 400.22.0 in the anonymized live validation

`PASS` means the result has evidence. `NOT TESTED` must be completed manually
before submission. This sheet deliberately does not claim tests that were not
performed.

| Area | Test | Result | Evidence / notes |
|---|---|---:|---|
| Launch | Visible root window opens and displays a loader/status | PASS | Live visible `Warframe Tracker` window; responsive process observed |
| Launch | Closing root ends all app windows/processes | NOT TESTED | Requires closing the current QA session |
| Window | Minimize and restore | PASS | Overwolf window API returned success and state returned to `Normal` |
| Window | Maximize and restore | PASS | Overwolf window API returned success and state returned to `Normal` |
| Window | Drag and title-bar double-click | NOT TESTED | Manual pointer interaction required |
| Window | `Ctrl+Shift+T` hides and restores | NOT TESTED | Manual interaction required |
| Window | GEP panel collapses without stopping the embedded Tracker | PASS | Expanded/collapsed through the live Native controller; iframe stayed loaded |
| Language | English initial; Spanish selectable and persistent | PASS | Switched to Spanish, reloaded the embedded Tracker and confirmed `lang=es`; restored English |
| GEP | Tracker launched before Warframe registers `match_info` | PASS | `reports/GEP_LIVE_VALIDATION.md` |
| GEP | Tracker launched while Warframe is running registers | NOT TESTED | Must be repeated on final OPK |
| GEP | `onInfoUpdates2` receives a real inventory | PASS | 2,406-type historical validation and 2,256-entry current status observed |
| GEP | `getInfo()` recovers a missed transition | PASS | Historical live validation and implemented polling path |
| GEP | Closing Warframe stops polling without errors | NOT TESTED | Must be repeated on final OPK |
| GEP | Failure is visible and raw JSON is absent from logs | PASS | Source/package audit; raw payload is never logged |
| Sync | Anonymous user cannot stage/preview inventory | PASS | ASP.NET authorization tests |
| Sync | Signed-in user must press Send and then Apply | PASS | Current preview UI and server tests |
| Sync | Two users cannot see each other's capture | PASS | Per-user isolation test coverage |
| Sync | Duplicate snapshot does not duplicate collection data | PASS | SHA-256 deduplication and ingestion tests |
| Sync | Offline Send retains the local snapshot for retry | NOT TESTED | Manual network interruption required |
| Sync | Discard removes the local snapshot | NOT TESTED | Manual check required |
| Sync | 30-minute expiration removes stale snapshot | PASS | Native core expiration behavior/source test coverage |
| Sync | Database failure rolls back the entire Apply | PASS | Transactional ingestion test coverage |
| Data | Credits, Endo, Aya and Ducats match available GEP data | NOT TESTED | Must compare against current in-game totals |
| Data | I/E/F/R relic quantities remain independent | PASS | Neo S13 Radiant live validation |
| Data | Owned and mastered Warframes/weapons are distinguished | PARTIAL | Feature present; current account comparison still required |
| UI | Warframe search accepts typed queries | PASS | Typed `Revenant Prime`; the matching detail link was returned and opened |
| UI | Goal shows useful owned relics and refinement advice | PASS | Revenant Prime plan loaded 27 routes and four owned relics with refinement advice |
| UI | Build mod search queries the full catalog | PASS | Typed `Serration`; seven matching catalog entries were returned |
| UI | Saved-build filtering and pagination | PARTIAL | Status/search controls loaded, but this account currently has only one saved build |
| UI | Compare fields accept typed searches | PASS | Typed `Revenant`; `Revenant Prime` appeared as a selectable result |
| UI | English localization is complete | FAIL | Planner/build cards still contain Spanish strings such as `rutas`, `Relíquia`, `Ranura` and `capacidad` |
| Network | Offline state and retry are understandable | PASS | Native offline event displays a dedicated safe-capture message and Retry connection action; screenshot 10 |
| Performance | No freeze during 10-minute navigation test | PARTIAL | Responsive during 30-second process sample; 10-minute navigation pending |
| Performance | Memory settles after repeated navigation | PARTIAL | Overwolf total remained 680.4–681.9 MB for 30 seconds |
| Display | 1180×720 / 100% | NOT TESTED | Manual resize required |
| Display | 1366×768 / 100% | NOT TESTED | Manual resize required |
| Display | 1920×1080 / 125% | NOT TESTED | Manual DPI test required |
| Display | 2560×1440 / 150% | NOT TESTED | Manual DPI test required |
| Display | Secondary-monitor move/maximize/restore | NOT TESTED | Requires secondary-monitor interaction |
| Security | OPK has zero VirusTotal detections | INVALID FOR CURRENT OPK | Supplied report is for `77710B224F8C4CF9FDE82641FEE3F5C85ABC0F2A6863133038DE7FAF845E4C16`; current OPK is `880E8ADDEAC5E4D30AE4D035071E806920B59B8F7474E981811D9AD44696E6F0` |
| Automated | Native core test suite | PASS | 3/3 Node tests passed |
| Automated | Agent test suite | PASS | 19/19 .NET tests passed |
| Build | Web backend Release build | PASS | .NET 8 Release build completed with 0 warnings and 0 errors |

Cold start (shell visible): NOT MEASURED

Hosted Tracker HTTP response after warmup: 0.406–0.882 s across five requests

Warm Warframe detail navigation: NOT MEASURED

Overwolf total memory during 30-second sample: 680.4–681.9 MB

Overwolf total CPU during 30-second sample: 2.93% average / 4.27% peak on 24 logical processors

Final result: INCOMPLETE — three real GEP screenshots, final manual checks, hosted localization redeploy and a matching VirusTotal report remain

Tester signature or name: Automated evidence recorded by Codex; manual reviewer pending
