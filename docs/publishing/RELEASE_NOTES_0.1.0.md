# Warframe Tracker 0.1.0 — MVP inicial

Fecha prevista: pendiente de aprobación de QA.

## Funciones principales

- Aplicación OW‑Electron con ventana de escritorio y backend ASP.NET local.
- Tutorial inicial en español, omisible y disponible nuevamente desde el menú.
- Captura de inventario de Warframe mediante Overwolf GEP con vista previa y
  confirmación antes de modificar datos.
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
- Captura GEP real: pendiente del `OW_DEV_KEY` de Overwolf.
- Firma de producción: pendiente de credenciales de consola y certificado de
  firma de código.
