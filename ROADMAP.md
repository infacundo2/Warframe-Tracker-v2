# Warframe Tracker — hoja de ruta

La rama `main` conserva la versión estable. Las fases se desarrollan y validan
en ramas separadas antes de integrarse.

## Estado

- Fase 1: implementada en `feature/operator-goals`.
- Fase 2: motor inicial implementado; calcula rutas por componente, inventario
  I/E/P/R, probabilidades, vestigios e intentos para 50%, 75% y 90%.
- Pendiente en fase 2: incorporar costes de recursos cuando exista una fuente
  de catálogo normalizada para esos datos.

## Fase 1 — Objetivos del operador

- Marcar Warframes, armas y mods como objetivos.
- Definir prioridad y consultar progreso.
- Mostrar componentes pendientes y reliquias relacionadas.
- Recomendar primero las reliquias que el usuario ya posee.
- Detectar sets completos o a una pieza de completarse.

## Fase 2 — Planificador de farmeo

- Construir una ruta desde componentes pendientes.
- Separar reliquias disponibles y vaulted.
- Comparar Intacta, Excepcional, Perfecta y Radiante.
- Estimar intentos y vestigios según la estrategia elegida.
- Priorizar una ruta corta, económica o de máxima probabilidad.

## Fase 3 — Centro de mando personal

- Progreso global del catálogo y del inventario.
- Objetivos prioritarios y sets casi completos.
- Historial reciente y estadísticas personales.
- Panel “¿Qué puedo construir?”.

## Fase 4 — Worldstate

- Fisuras, alertas, invasiones, ciclos y comerciante del Vacío.
- Cruce de fisuras activas con reliquias poseídas y objetivos.
- Avisos relevantes sin notificaciones invasivas.

## Fase 5 — Herramientas avanzadas

- Buscador universal con `Ctrl+K`.
- Calculadora de escuadrón y refinamiento.
- Mapa de relaciones entre objetos.
- Comparador y constructor ligero de builds.
- Importación, exportación y edición masiva del inventario.

## Fase 6 — Experiencia holográfica

- Diagramas SVG orbitales y conexiones animadas.
- Capas con profundidad y movimiento reducido opcional.
- Microanimaciones de progreso y finalización.
- Presupuesto estricto de rendimiento y accesibilidad.
