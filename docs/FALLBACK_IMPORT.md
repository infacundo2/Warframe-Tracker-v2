# Importación alternativa y límites técnicos

`Importar-Inventario-Seguro.ps1` abre un JSON de inventario, lo valida, inicia
Warframe Tracker únicamente en `127.0.0.1` y entrega la captura mediante una
clave aleatoria que no se escribe en disco.

Uso:

```powershell
.\Importar-Inventario-Seguro.ps1 .\inventario.json
```

También se puede arrastrar el JSON sobre `Importar-Inventario-Seguro.cmd`.

## Lo que este método no hace

PowerShell, BAT y CMD no pueden producir por sí mismos el evento
`match_info.inventory`. Ese evento pertenece al proveedor GEP de Overwolf. Para
obtener automáticamente el inventario completo hay que ejecutar una aplicación
aprobada y habilitada por Overwolf.

No se implementará lectura de memoria, inyección en el proceso, descifrado del
tráfico ni extracción de credenciales. Esas alternativas son frágiles, pueden
violar las reglas de Warframe y exponen la cuenta del usuario.

Un OCR de capturas de pantalla puede añadirse en el futuro, pero solo reconocerá
las páginas visibles y nunca tendrá la precisión o cobertura de GEP.
