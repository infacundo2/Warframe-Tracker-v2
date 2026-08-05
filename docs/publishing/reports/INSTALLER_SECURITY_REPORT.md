# Installer security report

- Build: Warframe Tracker 0.1.0, Windows x64
- File: `Warframe-Tracker-Setup-0.1.0.exe`
- Size: 148,515,022 bytes
- SHA-256: `7CEE0A7AE84EF42C8053940793D07A25CBA9BD549CB5C8F2CFB7DBB948964082`
- Microsoft Defender real-time protection: enabled
- Defender signature version: `1.455.509.0`
- Defender signatures last updated: 2026-08-04 12:42:12 local time
- Custom scan result for installer: no threat detection recorded
- Authenticode status: `NotSigned`

## Signing status

The unsigned state is expected only for the MVP review artifact. The build log
confirms that `OW_CLI_EMAIL` and `OW_CLI_API_KEY` were unavailable, so the
Overwolf package signature could not be created. A trusted Windows code-signing
certificate is also still required. This installer must be signed and rescanned
before public store distribution.
