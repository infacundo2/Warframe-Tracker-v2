# Lista de control QA

`[x]` comprobado, `[ ]` pendiente de credenciales, certificado o hardware externo.

## Instalación e inicio

- [x] Compila y ejecuta en Windows 11 25H2 x64.
- [ ] Instalación limpia en otra máquina Windows 11 x64.
- [ ] Firma digital válida del ejecutable y del instalador.
- [x] El instalador incluye .NET y no requiere Node.js ni MySQL.
- [x] Una segunda ejecución enfoca la ventana existente.
- [x] Al cerrar termina el servidor local.
- [x] El servidor se inicia sin ventana de consola.
- [x] Existe una ventana de escritorio visible mientras la app funciona.
- [x] Atajo global configurable para mostrar u ocultar la ventana.

## Seguridad y privacidad

- [x] Backend limitado a `127.0.0.1` y puerto efímero.
- [x] Puente protegido por una clave aleatoria por ejecución.
- [x] Límite de solicitud de 20 MB.
- [x] El JSON bruto se descarta y la vista previa vence a los 30 minutos.
- [x] Enlaces externos abiertos en el navegador predeterminado.
- [x] Renderer sin integración Node, con aislamiento y sandbox.
- [x] Auditoría NuGet sin vulnerabilidades conocidas.
- [x] Auditoría npm de producción sin vulnerabilidades conocidas.
- [x] Microsoft Defender sin detecciones en el instalador de prueba.
- [x] Política accesible dentro de la app y versión HTTPS lista para GitHub Pages.
- [x] URLs públicas HTTPS de privacidad y soporte comprobadas.

## Resoluciones

- [x] 1366x720, escala lógica 100%, sin desbordamiento horizontal.
- [x] 1366x768, escala lógica 100%, sin desbordamiento horizontal.
- [x] 1920x1080, escala lógica 125%, sin desbordamiento horizontal.
- [x] 2560x1440, escala lógica 100%, sin desbordamiento horizontal.
- [x] 3840x2160, escala lógica 150%, sin desbordamiento horizontal.
- [ ] Repetir escalas en monitores físicos o máquinas virtuales antes del envío final.

## Overwolf GEP e inventario

- [x] Warframe detectado con game ID `8954` usando el `OW_DEV_KEY` temporal.
- [x] `game_info`, `match_info.highlighted` y `match_info.inventory` reales validados.
- [x] Captura autoritativa de 2.406 tipos aplicada y revisada por el usuario.
- [x] Variante real `Neo S13 Radiant` conservada separadamente con cantidad 1.
- [x] Sondeo `getInfo()` cada 2,5 s, eventos y detención al cerrar el juego.
- [x] Las capturas parciales no ponen a cero objetos ausentes.
- [x] Se requiere confirmación antes de aplicar una captura.
- [x] Los datos confirmados persisten localmente.

## Entrega y tienda

- [x] Inglés predeterminado comprobado con un perfil de idioma limpio.
- [x] Paquetes locales inglés/español con selector persistente.
- [x] Doce rutas públicas revisadas sin español residual, salvo la opción deliberada `Español`.
- [x] Cambio EN → ES → EN comprobado sin reinstalar.
- [x] Tutorial inicial bilingüe, saltable y revisitable.
- [x] Guía ilustrada con instrucciones y doce capturas reales, incluyendo EN/ES.
- [x] Capturas JPG 1200x675 de menos de 100 KB.
- [x] Icono 55x55 y tile 258x198.
- [x] Nombre, autor, versión y aviso de marca configurados.
- [x] Notas de versión, política de privacidad y soporte preparados.
- [ ] UID definitivo y claves de consola después de aprobar el MVP.
- [ ] Build pública firmada con certificado de CA confiable.
