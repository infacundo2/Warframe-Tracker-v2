# Installer security report

- Build: Warframe Tracker 0.1.0, Windows x64
- File: `Warframe-Tracker-Setup-0.1.0.exe`
- Size: 148,515,022 bytes
- SHA-256: `40FB07DD41E98FD1B2DF594EBB0DAACCA45E9D8290E4BDADBBAC4AD7A31330FC`
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
