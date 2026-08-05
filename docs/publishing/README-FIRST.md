# Warframe Tracker 0.1.0 — Overwolf MVP package

Start with `SUBMISSION_FORM_ANSWERS.md`, then follow `MVP_QA_GUIDE_EN.md`.
`SCREENSHOT_INDEX.md` maps each screenshot to a feature. The installer is under
`build/`, and all technical results are under `reports/`.

## What to submit now

- The completed Overwolf MVP form using `SUBMISSION_FORM_ANSWERS.md`.
- `build/Warframe-Tracker-Setup-0.1.0.exe` as the Windows 11 x64 review build.
- The ten images under `screenshots/`.
- `MVP_QA_GUIDE_EN.md`, `RELEASE_NOTES_0.1.0.md`, and the public Privacy,
  Terms, and Support URLs.
- `reports/GEP_LIVE_VALIDATION.md` as anonymized proof of the real inventory
  flow.

## Important reviewer disclosure

The MVP code, visible desktop window, local backend, and real GEP path have been
tested. The installer is not the final public-store binary: it still needs the
Overwolf production package signature and a trusted Windows code-signing
certificate. The review build can be inspected and its UI works, but GEP packages
will not load from this unsigned distributable. Reviewers can reproduce GEP from
source in authorized dev mode with their own temporary key.

Do not publish this unsigned installer to end users. After Console access is
issued, follow `SIGNING.md`, rebuild, repeat Defender/QA checks, and replace the
installer and checksum.

## Public pages

- Privacy: https://infacundo2.github.io/Warframe-Tracker-v2/privacy.html
- Terms: https://infacundo2.github.io/Warframe-Tracker-v2/terms.html
- Support: https://infacundo2.github.io/Warframe-Tracker-v2/support.html
- Source: https://github.com/infacundo2/Warframe-Tracker-v2

No temporary key, raw inventory payload, player identifier, local database, or
Warframe credential is included in this package.
