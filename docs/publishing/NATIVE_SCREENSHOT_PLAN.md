# Native screenshot plan

Overwolf requires written instructions and real screenshots. Do not include raw
inventory JSON, account IDs, email addresses, temporary keys or database data.

Store screenshots must be JPG, exactly 1200×675 and at most 100 KB. Capture the
Native window first, then crop/resize a copy for the store; preserve originals
in `screenshots-native/originals/`.

| File | Required visible state | Sensitive-data check |
|---|---|---|
| `01-native-window-en.jpg` | Complete visible Native window, English, Warframe not detected | No email |
| `02-gep-ready-en.jpg` | Warframe detected and `GEP READY` | Hide player name |
| `03-inventory-captured-en.jpg` | Panel expanded, `INVENTORY CAPTURED`, entry count | No raw JSON |
| `04-preview-en.jpg` | Authenticated preview before Apply | Hide account identifiers |
| `05-command-center-en.jpg` | Command Center after confirmed sync | Hide private notes |
| `06-warframes-en.jpg` | Search/filter and mastery indicators | Safe catalog view |
| `07-relics-en.jpg` | I/E/F/R quantities and probability selector | Safe catalog view |
| `08-goal-planner-en.jpg` | Goal with useful owned relics/refinement recommendation | Safe item data |
| `09-settings-es.jpg` | Spanish selected and hotkey reminder | No credentials |
| `10-offline-error-en.jpg` | Clear offline/retry state | No server/log details |

Submit screenshots 01, 03, 04, 06 and 08 as the five store images. Include all
ten in the QA archive. A screenshot of a simulated GEP event must be labelled
`SIMULATED`; screenshots 02–04 should preferably use a real test session.

Before capturing:

1. Use Windows 1920×1080 at 100% scaling.
2. Resize the Native window to a clean 16:9 composition.
3. Use English except screenshot 09.
4. Disable notifications and close unrelated windows.
5. Check the image at 100% zoom for secrets and personal identifiers.
