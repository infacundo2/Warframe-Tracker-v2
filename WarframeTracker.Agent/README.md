# WarframeTracker.Agent

Agente local independiente de Warframe Tracker para Windows. Detecta
`Warframe.x64.exe`, puede vincularse de forma segura con la cuenta Web y aloja
el pipeline opcional de eventos, snapshots y sincronización.

## Funcionalidad disponible

- Detección y ciclo de vida de Warframe.
- Emparejamiento por código temporal, sin reutilizar la contraseña.
- Token de dispositivo cifrado con DPAPI y revocable desde `/agent/devices`.
- Lector incremental opcional de `EE.log`; nunca registra líneas completas.
- Contrato reemplazable `IInventoryProvider`.
- Normalización, hash SHA-256 y diferencias Added/Changed/Removed.
- Snapshot local atómico y cola offline limitada.
- Preview/apply remoto idempotente y reintentos con backoff y jitter.
- Sincronización automática solamente cuando el usuario la habilita.

El proveedor directo real continúa apagado: Warframe no ofrece esa fuente al
Agent mediante una API pública. Para QA existe `SafeInboxInventoryProvider`,
que lee exclusivamente un snapshot normalizado colocado voluntariamente en
`%LOCALAPPDATA%\WarframeTracker\Agent\inventory-inbox.json`. No lee memoria,
tráfico de red ni modifica archivos del juego.

## Primera vinculación

1. Despliega el backend con las migraciones nuevas.
2. Ejecuta `WarframeTracker.Agent.exe`.
3. El Agent mostrará un código y abrirá `/agent/connect` en el navegador.
4. Inicia sesión en la Web y confirma que el código coincide.
5. Vuelve a la consola: debe indicar que guardó el token con DPAPI.

## Ejecutar desde código

```powershell
dotnet run --project .\WarframeTracker.Agent\WarframeTracker.Agent.csproj
```

## Activar EE.log para QA

En `trackeragentsettings.json` cambia:

```json
"EELogProviderEnabled": true
```

La ruta predeterminada es `%LOCALAPPDATA%\Warframe\EE.log`; `EELogPath` permite
un override. EE.log aporta eventos, no un inventario completo.

## Pipeline de inventario de QA

```json
"InventoryProviderEnabled": true,
"ExperimentalProviderEnabled": true,
"AutomaticSyncEnabled": false
```

Mantén `AutomaticSyncEnabled=false` salvo durante una prueba controlada. Cuando
se habilita, cada batch pasa primero por preview y después por apply. Los
reintentos del mismo batch devuelven `already_applied`.

Puedes copiar `samples\inventory-inbox.example.json` a
`%LOCALAPPDATA%\WarframeTracker\Agent\inventory-inbox.json` para una prueba
parcial inocua. Cambia cantidades para generar un snapshot nuevo. Nunca uses
`IsAuthoritative=true` con una lista incompleta.

## Crear ejecutable portable

```powershell
.\scripts\agent\Build-TrackerAgent.ps1
```

El resultado queda en `out\tracker-agent`.
