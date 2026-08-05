# Notas de seguridad de la construcción

Auditoría repetida el 4 de agosto de 2026:

- `npm audit --omit=dev`: **0 vulnerabilidades** de producción.
- `dotnet list package --vulnerable --include-transitive`: **0 paquetes vulnerables**.
- Microsoft Defender: protección en tiempo real y firmas activas; instalador de
  prueba analizado sin amenazas.
- El artefacto de desarrollo todavía aparece como `NotSigned`. Esto es esperado:
  la firma pública requiere el certificado de CA y las claves entregadas tras la
  aprobación del MVP.

Controles incluidos:

- Renderer sin Node.js, con context isolation y Chromium sandbox.
- Preload con una API mínima y lista cerrada de atajos.
- ASP.NET escucha solamente en loopback y usa un puerto efímero.
- Clave de puente aleatoria de 256 bits en cada ejecución.
- Comparación de clave en tiempo constante y límite de solicitud de 20 MB.
- Base SQLite local; no se incluyen credenciales MySQL.
- JSON bruto GEP descartado después de normalizarlo.
- Vista previa temporal y confirmación explícita antes de escribir inventario.
- Credencial `OW_DEV_KEY` utilizada solo en el entorno del proceso de prueba; no
  se almacena en Git, documentación, instalador ni capturas.
- Consentimiento oficial CMP de Overwolf si fuera necesario para publicidad.

La auditoría completa de dependencias de desarrollo puede informar problemas en
la cadena oficial de empaquetado de Electron/Overwolf. Esas herramientas no se
incluyen como dependencias de ejecución; deben revisarse de nuevo cuando Overwolf
publique una versión actualizada del builder.
