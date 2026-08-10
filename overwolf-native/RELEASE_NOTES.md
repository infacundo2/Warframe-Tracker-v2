# Warframe Tracker Native 0.1.1

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
