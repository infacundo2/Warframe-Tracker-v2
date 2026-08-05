# Warframe Tracker 0.1.0 — MVP inicial

Build de revisión: 4 de agosto de 2026.

## Funciones principales

- Aplicación OW‑Electron con ventana de escritorio y backend ASP.NET local.
- Interfaz completa en inglés como idioma predeterminado y paquete adicional en español.
- Selector `EN / ES` persistente en la barra superior, el tutorial y Ajustes.
- Tutorial inicial bilingüe, omisible y disponible nuevamente desde el menú.
- Captura de inventario de Warframe mediante Overwolf GEP con vista previa y
  confirmación antes de modificar datos.
- Sondeo seguro de `getInfo()` mientras Warframe está activo, además de eventos,
  para recoger inventarios que aparecen únicamente durante pantallas de carga.
- Seguimiento de Warframes, armas, mods, reliquias por refinamiento,
  componentes Prime y recursos.
- Centro de mando, objetivos, elementos construibles, planificador de farmeo,
  comparador, builds y Worldstate.
- Buscador universal mediante `Ctrl+K`.
- Atajo global configurable para mostrar u ocultar la ventana de escritorio.
- Música ambiental, volumen, efectos opcionales y movimiento reducido.
- Política de privacidad y soporte accesibles sin iniciar sesión.

## Privacidad y seguridad

- Datos normalizados almacenados localmente en SQLite.
- JSON bruto descartado después del análisis.
- Vista previa con caducidad de 30 minutos.
- Backend limitado a loopback y protegido mediante una clave efímera.
- Node Integration desactivado, Context Isolation y sandbox activados.

## Estado de validación

- Windows 11 x64: probado.
- Compilación .NET y TypeScript: aprobada.
- Pruebas automatizadas del importador: aprobadas.
- Captura GEP real: aprobada con 2.406 tipos distintos y cobertura autoritativa.
- Refinamientos de reliquia: Intacta, Excepcional, Perfecta y Radiante validados
  como registros independientes; corregida su agrupación en detalle y premios.
- Proceso principal protegido frente a consolas cerradas (`EPIPE`).
- Firma de producción: pendiente de credenciales de consola y certificado de
  firma de código.
