# Warframe Tracker Native 0.1.2

- Paquete Native reconstruido desde el código actual para incorporar por
  completo el panel GEP plegable y la configuración vigente de Render.
- La interfaz remota incluye los filtros e indicadores de maestría para
  Warframes y armas cuando la captura contiene datos históricos `XPInfo`.

- El panel lateral de inventario automático queda oculto por defecto sin
  detener GEP y puede mostrarse con el nuevo indicador compacto `GEP`.
- El estado del indicador permite saber si espera, conecta, capturó o falló sin
  ocupar una columna completa.

- Ventana inicial ampliada a 1680×980, redimensionable y con controles de
  minimizar, maximizar/restaurar y cerrar.
- URL de producción centralizada en `tracker.config.json`.
- Catálogos paginados con consultas más ligeras, filtros SQL, caché de facetas e
  imágenes diferidas.
- Objetivos conectados al planificador de componentes, reliquias I/E/P/R,
  rareza, disponibilidad, rutas y presupuesto de vestigios.
- Constructor de builds con búsqueda de equipamiento/mods, checklist de
  inventario, progreso, estados completado/archivado y eliminación.
- Progreso combinado entre el set objetivo y sus builds relacionadas.
- Migraciones versionadas para MySQL en Render y SQLite local.
