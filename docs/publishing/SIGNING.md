# Firma de Warframe Tracker

La distribución pública necesita **dos firmas distintas**:

1. Overwolf firma la integridad de GEP durante `ow-electron-builder`.
2. El desarrollador firma el ejecutable con un certificado de firma de código
   emitido por una autoridad certificadora confiable.

Un certificado autofirmado sirve para experimentos internos, pero no satisface
la distribución pública porque Windows no confía en él.

## Datos necesarios

- App registrada y App UID en Overwolf Console.
- `OW_CLI_EMAIL` y `OW_CLI_API_KEY`.
- `OW_BUILD_KEY` de **Release management > App Keys**.
- Certificado de firma de código, por ejemplo de DigiCert o Sectigo, exportable
  como PFX con su clave privada o accesible mediante el proveedor compatible.

Nunca guardar claves o contraseñas en Git, `package.json`, capturas o correos.

## Construcción con un certificado PFX

Abrir PowerShell en la raíz del repositorio y definir las credenciales solo para
la sesión actual:

```powershell
$env:OW_CLI_EMAIL = 'correo-aprobado'
$env:OW_CLI_API_KEY = 'clave-de-consola'
$env:OW_BUILD_KEY = 'build-key-de-la-app'
.\scripts\desktop-electron\Preparar-Build-Firmada.ps1 -CertificatePfx 'C:\ruta\certificado.pfx'
```

El script solicita la contraseña de manera oculta, valida que el certificado
incluya clave privada y permiso de firma de código, construye el instalador y
comprueba que Authenticode devuelva `Valid`.

## Verificación manual

```powershell
Get-AuthenticodeSignature `
  .\out\desktop-installer\Warframe-Tracker-Setup-0.1.0.exe |
  Format-List Status, StatusMessage, SignerCertificate
```

No enviar una build pública si el resultado no es `Valid`.
