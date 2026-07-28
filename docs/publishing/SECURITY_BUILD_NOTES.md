# Security build notes

Audit performed on July 27, 2026:

- `npm audit --omit=dev`: **0 vulnerabilities** in production dependencies.
- Full development audit: 20 high-severity findings in the official OW Electron
  builder toolchain and its Electron Builder transitive packages.
- `npm audit` reports no currently available complete fix for the direct
  `@overwolf/ow-electron-builder` dependency.

These packages are used to produce the Windows artifact and are not shipped as
application runtime dependencies. The project pins the current official
Overwolf packages and should repeat the full audit whenever Overwolf publishes a
new builder version.

Additional checks:

- Renderer Node integration disabled.
- Context isolation and Chromium sandbox enabled.
- ASP.NET binds to an ephemeral loopback port.
- Per-launch 256-bit bridge key.
- Fixed-time bridge-key comparison.
- 20 MB request limit.
- No local `appsettings.json` or MySQL credentials in the packaged backend.
- Raw GEP JSON discarded immediately after parsing.
