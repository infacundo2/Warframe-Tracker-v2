# Warframe Tracker — hoja de ruta

La rama `main` conserva la versión estable. Las fases se desarrollan y validan
en ramas separadas antes de integrarse.

## Estado

- Fase 1: implementada en `feature/operator-goals`.
- Fase 2: motor inicial implementado; calcula rutas por componente, inventario
  I/E/P/R, probabilidades, vestigios e intentos para 50%, 75% y 90%.
- Fase 2 incluye cantidades requeridas por componente y recurso tomadas del
  catálogo normalizado; objetivos, planificador y análisis de construcción
  distinguen entre poseer una pieza y reunir la cantidad necesaria.
- Fase 3: centro de mando personal e historial automático implementados. Los
  eventos comienzan a registrarse desde la migración `AddInventoryHistory`.
- Inteligencia de reliquias: laboratorio implementado con comparación I/E/P/R,
  escuadrones de 1–4 jugadores, simulaciones de 1/4/10/20 aperturas, vestigios
  e intentos estimados para alcanzar 50%, 75% y 90%.
- Fase 4: Worldstate ampliado con fisuras, alertas, invasiones, ciclos, Baro y
  Nightwave. Las fisuras se cruzan con reliquias poseídas y objetivos activos.
- Herramientas transversales: consola universal con `Ctrl+K`, comparación de
  Warframes y armas, y mapa navegable de componentes y reliquias implementados.
- Inventario avanzado: exportación e importación JSON combinable implementadas.
  El constructor ligero guarda builds, calcula capacidad y señala mods faltantes.
- Gestión avanzada: edición múltiple de reliquias, historial reversible,
  estadísticas semanales y metadatos personales (notas, etiquetas y fecha).
- Mods y builds: filtros por compatibilidad, polaridad, rareza, colección e
  inventario; calculadora de fusión y creación de objetivos desde una build.
- Validación HTTP: rutas principales y detalles reales responden correctamente;
  la evidencia y la limitación visual del entorno están en `VALIDATION.md`.
- Planificador y herramientas: rutas de misión, estrategias corta/multiobjetivo,
  comparación de mods/reliquias y exportación CSV añadidas.
- Experiencia: diagrama orbital SVG, profundidad reactiva, celebración al 100%,
  energía reducida y enlaces de builds portables sin datos de cuenta.
- Objetivos/builds: metas cuantitativas de reliquias sumando I/E/P/R y cálculo
  de capacidad por rango, polaridad coincidente/conflictiva y Forma sugerida.
- Inteligencia final: comandos de búsqueda por intención, precios online v2,
  valor esperado por refinamiento e historial manual de aperturas.
- Inventario final: duplicados de mods con Endo estimado y estados separados
  para plano, set completo y objeto construido, conservados en JSON/CSV.

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
