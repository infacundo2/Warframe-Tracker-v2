# Entrega del MVP a Overwolf

Warframe Tracker es una aplicación compañera de escritorio para Warframe. La
idea ya fue aceptada por Developer Relations. Overwolf indicó que creará la
consola de distribución después de revisar y aprobar este MVP.

## Estado del MVP

La aplicación ya dispone de una ventana de escritorio visible, interfaz inglesa
predeterminada con paquete español seleccionable, tutorial bilingüe, perfil local,
catálogo, inventario, reliquias, objetivos, recursos,
privacidad, soporte y ajustes. El backend y la base SQLite se ejecutan de forma
local y el instalador incluye el runtime necesario.

La captura real de `match_info.inventory` fue validada el 4 de agosto de 2026
con el `OW_DEV_KEY` temporal. GEP 400.22.0 detectó Warframe (game ID 8954),
activó `game_info` y `match_info`, recibió una instantánea autoritativa de 2.406
tipos de objetos y el usuario confirmó su aplicación local. También se comprobó
una reliquia refinada real (`Neo S13 Radiant`) sin conservar el JSON bruto ni
incluir identificadores personales en esta entrega.

## Cómo probar esta construcción

1. Instalar `Warframe-Tracker-Setup-0.1.0.exe` en Windows 11 x64.
2. Abrir Warframe Tracker y completar o saltar el tutorial en inglés.
3. Crear un perfil local.
4. Recorrer el catálogo y la pantalla de sincronización.
5. Abrir **Ajustes** y probar el atajo global.
6. Consultar **Privacidad** y **Soporte** desde la propia aplicación.
7. Para probar sin GEP real, usar el simulador descrito en `MVP_QA_GUIDE.md`.
8. Para repetir la validación GEP desde el código fuente, definir una credencial
   temporal propia y ejecutar:

```powershell
$env:OW_DEV_KEY = "clave-temporal"
Set-Location .\desktop
npm run start:dev-gep
```

Después se inicia Warframe. El inventario se publica durante el inicio de sesión
o una pantalla de carga; si hace falta, se entra y sale de un Repetidor, Dojo o
misión. Tracker consulta `getInfo()` cada 2,5 segundos mientras Warframe está
activo y también procesa `new-info-update`. La vista previa nunca se aplica sin
confirmación.

## Material incluido

- Guía ilustrada para revisión: `MVP_QA_GUIDE_EN.md` (inglés) y
  `MVP_QA_GUIDE.md` (español).
- Respuestas listas para el formulario: `SUBMISSION_FORM_ANSWERS.md`.
- Capturas reales reproducibles en `screenshots/`, en inglés salvo la evidencia
  explícita del selector español.
- Índice explicativo de capturas en `SCREENSHOT_INDEX.md`.
- Icono y tile de tienda en `store-assets/`.
- Texto de tienda en `STORE_LISTING.md`.
- Notas de versión en `RELEASE_NOTES_0.1.0.md`.
- Política, términos y soporte estáticos en `privacy.html`, `terms.html` y
  `support.html`.
- Evidencia GEP anonimizada en `reports/GEP_LIVE_VALIDATION.md`.
- Informes de Windows y resoluciones en `reports/`.
- Hash, Defender y estado de firma en `reports/INSTALLER_SECURITY_REPORT.md`.

## Publicación pública

La intención es que la aplicación sea pública, gratuita y accesible para toda la
comunidad. Si Overwolf exige monetización para una aplicación pública, se usará
únicamente su solución oficial de anuncios o suscripciones, con CMP cuando
corresponda. No se integrarán anuncios de terceros ni publicidad que interfiera
con Warframe.

## Después de la aprobación

1. Recibir el UID, `OW_CLI_API_KEY` y `OW_BUILD_KEY` desde la consola.
2. Obtener un certificado de firma de código emitido por una CA confiable.
3. Solicitar la firma del paquete GEP y firmar el ejecutable/instalador.
4. Ejecutar `Preparar-Build-Firmada.ps1`.
5. Repetir las pruebas con datos reales y en la matriz física de DPI.
6. Subir la build firmada y completar la ficha pública.

No se declara afiliación ni respaldo de Digital Extremes.
