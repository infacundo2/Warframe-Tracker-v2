# Warframe Tracker Desktop

Cliente oficial de Overwolf Electron para obtener `match_info.inventory` de
Warframe (game ID `8954`) y entregarlo al servidor Blazor local.

## Desarrollo

Requisitos:

- Windows 10/11 x64.
- .NET SDK 8.
- Node.js 22.12 o superior (requisito del empaquetador oficial actual).
- Credenciales de desarrollo de Overwolf para activar GEP.

```powershell
cd desktop
npm install
$env:OW_DEV_KEY = "token-de-desarrollo"
npm run start:dev-gep
```

Sin autorización GEP, la interfaz y el parser se pueden probar con:

```powershell
npm run build
npx ow-electron . --simulate-inventory=../tools/samples/warframe-inventory.sample.json
```

La app:

1. reserva un puerto aleatorio en `127.0.0.1`;
2. genera una clave efímera de 256 bits;
3. inicia el backend ASP.NET en modo SQLite;
4. captura el inventario mediante Overwolf GEP;
5. descarta el JSON bruto y conserva solo una vista previa normalizada hasta
   que el usuario revisa y confirma.

En el primer inicio la base SQLite está vacía y el catálogo público se descarga
en segundo plano. La primera vista previa puede tardar alrededor de un minuto en
estar disponible.

El modo Electron no fuerza el proveedor de base de datos. SQLite es siempre el
valor seguro por defecto; una conexión MySQL de QA solo se habilita mediante las
variables externas documentadas en el README principal. La portable nunca debe
incluir credenciales y no aplica migraciones a MySQL automáticamente.

## Empaquetado

```powershell
npm run package
```

El instalador queda en `out/desktop-installer`. Para una versión pública son
obligatorios la aplicación registrada, la firma de Overwolf para GEP y un
certificado de firma de código del editor.
