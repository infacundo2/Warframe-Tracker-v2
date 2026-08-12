# Warframe Tracker Native 0.1.2 — first-build submission

This folder is the single source for the Overwolf Native MVP review. Start with
`SUBMISSION_FORM_ANSWERS.md`, then follow `MVP_QA_GUIDE_EN.md`.

## Submit

- `build/Warframe-Tracker-Native-0.1.2.opk`
- `SUBMISSION_FORM_ANSWERS.md`
- `MVP_QA_GUIDE_EN.md`
- completed `NATIVE_QA_RESULTS.md`
- numbered real images under `screenshots-native/`
- `RELEASE_NOTES_NATIVE_0.1.2.md`
- public Privacy, Terms and Support URLs
- `reports/NATIVE_SECURITY_REPORT.md`
- VirusTotal report URL and zero-detection screenshot

## Reviewer disclosure

This is an Overwolf Native WebApp, not the earlier OW-Electron executable. The
OPK has a visible root window and no EXE, DLL or native plugin. It receives
Warframe inventory only through Overwolf GEP `match_info.inventory`. A temporary
raw capture stays local for up to 30 minutes and is transmitted only after Send;
the authenticated site then requires preview and Apply confirmation.

No development key, raw inventory, Warframe credential, database password or
private identifier is included in this package.

## Public pages

- Privacy: https://infacundo2.github.io/Warframe-Tracker-v2/privacy.html
- Terms: https://infacundo2.github.io/Warframe-Tracker-v2/terms.html
- Support: https://infacundo2.github.io/Warframe-Tracker-v2/support.html
- Source: https://github.com/infacundo2/Warframe-Tracker-v2
