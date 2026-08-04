# Entrega del MVP a Overwolf

Warframe Tracker es una aplicación compañera de escritorio para Warframe. La
idea ya fue aceptada por Developer Relations. Overwolf indicó que creará la
consola de distribución después de revisar y aprobar este MVP.

## Estado del MVP

La aplicación ya dispone de una ventana de escritorio visible, tutorial inicial
en español, perfil local, catálogo, inventario, reliquias, objetivos, recursos,
privacidad, soporte y ajustes. El backend y la base SQLite se ejecutan de forma
local y el instalador incluye el runtime necesario.

La captura real de `match_info.inventory` y la comprobación exhaustiva contra un
inventario de Warframe quedan expresamente pendientes del `OW_DEV_KEY`. Esto debe
indicarse en el formulario: no se afirmará que la función fue probada con datos
reales antes de recibir la clave.

## Cómo probar esta construcción

1. Instalar `Warframe-Tracker-Setup-0.1.0.exe` en Windows 11 x64.
2. Abrir Warframe Tracker y completar o saltar el tutorial.
3. Crear un perfil local.
4. Recorrer el catálogo y la pantalla de sincronización.
5. Abrir **Ajustes** y probar el atajo global.
6. Consultar **Privacidad** y **Soporte** desde la propia aplicación.
7. Para probar sin GEP real, usar el simulador descrito en `MVP_QA_GUIDE.md`.
8. Tras recibir la clave temporal, ejecutar:

```powershell
$env:OW_DEV_KEY = "clave-temporal"
Set-Location .\desktop
npm run start:dev-gep
```

Después se inicia Warframe, se entra y sale del Repetidor y se abre el inventario
para provocar una actualización. La vista previa nunca se aplica sin confirmación.

## Material incluido

- Guía ilustrada: `MVP_QA_GUIDE.md`.
- Cinco capturas reales en `screenshots/`.
- Icono y tile de tienda en `store-assets/`.
- Texto de tienda en `STORE_LISTING.md`.
- Notas de versión en `RELEASE_NOTES_0.1.0.md`.
- Política y soporte estáticos en `privacy.html` y `support.html`.
- Informes de Windows y resoluciones en `reports/`.

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
