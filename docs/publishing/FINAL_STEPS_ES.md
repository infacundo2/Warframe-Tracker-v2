# Lo único que debes hacer antes de enviar

El RAR generado es un paquete de preparación, no la entrega definitiva, porque
Overwolf exige capturas reales y VirusTotal debe analizar exactamente el OPK
final. No marques pruebas que no ejecutaste.

## 1. Completar QA manual

Abre `guide/NATIVE_QA_RESULTS.md` y realiza cada prueba siguiendo
`guide/MVP_QA_GUIDE_EN.md`. Escribe `PASS`, `FAIL` o `N/A`, mediciones de carga,
CPU/memoria y las versiones de Windows, Overwolf, Warframe y GEP.

## 2. Tomar las capturas

Sigue `guide/NATIVE_SCREENSHOT_PLAN.md`. La forma mas sencilla es ejecutar
dentro de la carpeta de entrega:

```powershell
.\CAPTURAR-PANTALLA.ps1 -Numero 1
```

Tendras cinco segundos para volver a Warframe Tracker. El script captura la
ventana activa, conserva el PNG original y crea automaticamente el JPG final.
Repite cambiando `-Numero` del 1 al 10 segun el plan. Para convertir una captura
hecha manualmente, usa desde la raiz del repositorio:

```powershell
.\scripts\overwolf-native\Prepare-Store-Screenshot.ps1 `
  -InputPath "captura-original.png" `
  -OutputPath "out\overwolf-native-submission\Warframe-Tracker-Native-0.1.2-Submission\screenshots-native\01-native-window-en.jpg"
```

## 3. VirusTotal

Sube `build/Warframe-Tracker-Native-0.1.2.opk` siguiendo
`guide/VIRUSTOTAL_INSTRUCTIONS.md`. Debe resultar en cero detecciones. Guarda la
captura como `reports/virustotal-0-detections.png` y conserva la URL del informe.

## 4. Cerrar el paquete

Dentro de la carpeta de entrega ejecuta:

```powershell
.\FINALIZE-SUBMISSION.ps1 -VirusTotalUrl "https://www.virustotal.com/gui/file/..."
```

El script rechaza capturas con dimensiones/peso incorrectos, campos QA vacíos o
una URL inválida. Después recalcula todos los hashes y recrea el ZIP y el RAR.

## 5. Publicar páginas legales

Antes del formulario, sube a GitHub los `privacy.html`, `terms.html` y
`support.html` actualizados y abre sus tres URLs públicas en una ventana privada.
Comprueba que describen Overwolf Native y almacenamiento confirmado en la nube,
no la antigua aplicación Electron/SQLite.

## 6. Formulario

Copia las respuestas desde `guide/SUBMISSION_FORM_ANSWERS.md`. Adjunta el OPK,
la guía, las capturas, la hoja QA, el informe de seguridad y las release notes.
No subas el repositorio completo, bases de datos, claves ni JSON de inventario.
