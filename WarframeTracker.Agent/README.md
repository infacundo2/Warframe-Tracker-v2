# WarframeTracker.Agent

Agente local independiente de Warframe Tracker. La Fase 1 solamente detecta
`Warframe.x64.exe`, administra el ciclo de una sesión y se apaga limpiamente.
No captura inventario, no lee `EE.log` y no se comunica todavía con Render.

## Ejecutar

```powershell
dotnet run --project .\WarframeTracker.Agent\WarframeTracker.Agent.csproj
```

Estados esperados en consola:

```text
[Agent] Iniciado. Estado: esperando Warframe.
[Warframe] Proceso detectado. PID 1234. Sesión iniciada.
[Warframe] Proceso cerrado. PID 1234. Recursos de sesión liberados.
```

Pulsa `Ctrl+C` para realizar un apagado limpio.

## Configuración

La configuración no contiene secretos y está en `trackeragentsettings.json`.
El intervalo de detección admite valores entre 2 y 60 segundos. Los providers,
`EE.log` y la sincronización permanecen desactivados hasta sus respectivas
fases.
