# Publicación de Warframe Tracker

## Canal correcto

Warframe Tracker es una aplicación compañera de escritorio, no un mod o addon
instalable dentro de Warframe. A julio de 2026, Warframe no aparece como juego
admitido para proyectos de CurseForge. El canal correcto para la captura GEP es
el **Overwolf Appstore**.

- [Overwolf Appstore](https://www.overwolf.com/appstore)
- [Crear y publicar una app OW Electron](https://dev.overwolf.com/ow-electron/getting-started/project-roadmap/)
- [Requisitos de lanzamiento](https://dev.overwolf.com/ow-electron/getting-started/release-your-app)
- [CurseForge: crear un proyecto](https://support.curseforge.com/support/solutions/articles/9000197241-creating-and-submitting-a-project)

## 1. Propuesta y autorización GEP

1. Crear una cuenta en Overwolf Developers.
2. En la consola, crear una nueva propuesta de aplicación pública.
3. Pegar el texto de `APP_PROPOSAL.md`.
4. Indicar expresamente:
   - juego: Warframe;
   - game ID: `8954`;
   - paquete: `gep`;
   - features: `game_info` y `match_info`;
   - dato requerido: `match_info.inventory`;
   - la app siempre dispone de una ventana visible;
   - no es una aplicación puente privada o sin interfaz.
5. Esperar aprobación y la habilitación de desarrollo de GEP.
6. Crear un API key o dev token en la consola.

Sin esa aprobación la interfaz abre y el simulador funciona, pero el paquete GEP
no entregará inventarios reales.

## 2. Probar en modo desarrollo

El empaquetador actual requiere Node.js 22.12 o posterior.

```powershell
cd desktop
npm install
$env:OW_DEV_KEY = "token-entregado-por-overwolf"
npm run start:dev-gep
```

Prueba funcional:

1. iniciar la app antes que Warframe;
2. iniciar sesión o crear el perfil local;
3. abrir el inventario dentro de Warframe;
4. confirmar que aparece una captura en `Inventario automático`;
5. revisar que Warframes, armas, mods, reliquias, componentes y recursos
   coincidan;
6. comprobar que una captura parcial no borra objetos;
7. confirmar los cambios y reiniciar la app para verificar persistencia.

## 3. Requisitos antes del instalador público

- Nombre, autor y versión coherentes en consola y `desktop/package.json`.
- UID de la app asignado por Overwolf.
- Firma del paquete GEP por Overwolf.
- Certificado de firma de código de Windows para el editor.
- Política de privacidad publicada por HTTPS.
- Icono, logo, capturas y descripción de tienda en inglés.
- Flujo de consentimiento si en el futuro se agregan anuncios o analítica.
- Pruebas en una cuenta Windows limpia y sin MySQL instalado.

Variables para el build firmado:

```powershell
$env:OW_CLI_EMAIL = "correo-de-la-cuenta"
$env:OW_CLI_API_KEY = "api-key-de-la-consola"
$env:OW_BUILD_KEY = "build-key-de-la-app"
cd desktop
npm run package
```

El artefacto se genera en `out/desktop-installer`.

## 4. Entrega

1. Subir la versión desde Release Management en Overwolf Developers.
2. Adjuntar notas de versión y artefacto firmado.
3. Completar la ficha con `STORE_LISTING.md`.
4. Usar la URL pública de `privacy.html`.
5. Entregar las credenciales o instrucciones de prueba solicitadas al equipo QA.
6. No declarar que el producto está afiliado a Digital Extremes.

## Bloqueos que requieren al propietario

Codex puede preparar y compilar el proyecto, pero no puede completar por sí solo:

- aceptar contratos legales;
- crear la cuenta del desarrollador;
- comprar/validar el certificado de firma;
- enviar la propuesta bajo la identidad del propietario;
- publicar una versión sin las credenciales y aprobación de Overwolf.
