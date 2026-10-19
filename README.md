# iSeller POS nativo

Aplicación de punto de venta para Windows, offline-first, escrita en **WPF / .NET Framework 4.8**
para cubrir desde Windows 7 SP1 hasta Windows 11 sin depender de WebView2 ni de runtimes modernos.

El POS web actual (`iseller-tiendas.com/publico/production/ventas.php`) sigue siendo la referencia
funcional y el backend PHP sigue siendo la autoridad de datos.

## Requisitos para compilar

- Windows 10/11 (para desarrollar).
- .NET SDK 6 o superior (probado con 10.0.401) **o** Visual Studio 2022 con la carga
  ".NET desktop development".
- Los reference assemblies de .NET Framework 4.8 se restauran solos vía NuGet
  (`Microsoft.NETFramework.ReferenceAssemblies`).

## Comandos

```powershell
dotnet build IsellerPos.sln
dotnet test  IsellerPos.sln
dotnet run --project src/Iseller.Pos.App/Iseller.Pos.App.csproj
```

## Estructura

```
src/Iseller.Pos.App     UI WPF (login, venta, ajustes, temas)
src/Iseller.Pos.Core    Dominio y servicios:
                        Precios/    CalculadoraPrecios (port del PHP)
                        Carrito/    carrito, validación y fábrica de venta
                        Datos/      SQLite (esquema embebido de db/esquema.sql)
                        Api/        cliente de api/pos_*.php
                        Auth/       DPAPI + PBKDF2 + reglas de vigencia
                        Impresion/  ticket ESC/POS + spooler de Windows
src/Iseller.Pos.Tests   Tests (xUnit)
db/esquema.sql          Esquema SQLite local
PLAN.md                 Plan por fases, contratos y decisiones
```

## Estado

- Fase 0: solución, tema del POS, cálculo de precios portado y validado contra el PHP.
- Fase 1: login online/offline, catálogo en SQLite, búsqueda y precios locales.
- Fase 2: carrito persistente, venta offline, ticket ESC/POS 58/80 y cajón opcional.
- Fase 3: sincronización idempotente de ventas y refresco de stock.
- Fase 4: caja offline (apertura, cierre y reapertura por UUID).
- Extras: caché de imágenes, carritos reservados, pestaña Enviados y autoactualización.
- Backend: `api/pos_login.php`, `pos_catalogo.php`, `pos_tasas.php`, `pos_heartbeat.php`, `pos_ventas.php`, `pos_caja.php`, `pos_version.php`.

## Identidad visual

- Logo del sistema (`logo1-inv-compact.png`) en el login y en la cabecera.
- Color primario = color del logo, **#00C0C4** (el POS web usa el mint #2dd4a0; aquí manda el logo).
- **Barra superior propia** (mismo color que el fondo del sistema) con el logo, arrastre para mover
  y iconos blancos de minimizar y cerrar. El icono de la ventana (barra de tareas/Alt+Tab) es el logo.
- Textos, botones y miniaturas de las tablas quedan centrados verticalmente.
- **Imágenes:** descarga bajo demanda y caché local en `%LOCALAPPDATA%\iSeller\img`; se guarda
  una **versión reducida** (máx. 200 px para las que GDI+ puede decodificar: PNG/JPEG; los webp
  ya vienen pequeños). La clave incluye el id y la ruta de la foto, así un cambio de foto no
  sirve la vieja. Sin conexión se usan las que ya están en disco.
- **Búsqueda:** es un **modal** igual que en el POS web (botón `(B) BÚSQUEDA` o la tecla `B`):
  buscador arriba y tabla con nombre completo, stock, precios, cantidad y AGREGAR. `Enter` con un
  código de barras agrega el producto exacto; `Esc` cierra. Un número dentro del nombre no arrastra
  códigos.
- **Lector de código de barras:** funciona en toda la ventana (ráfaga de dígitos y `Enter`, con
  pausa máxima de 100 ms entre teclas, igual que el POS web). El producto escaneado **se muestra
  primero** y espera el `Enter` del cajero para entrar al carrito (los lectores baratos a veces
  disparan dos veces); con `Esc` se descarta.
- **Colores por moneda:** igual que el POS web. Pesos en azul, bolívares en rojo y dólares en
  verde, tanto en el carrito, los totales y la tabla de búsqueda como en las ventas enviadas.
  Los **pesos van sin decimales** (como el web) en carrito, buscador, totales, cambio y ticket;
  bolívares y dólares mantienen dos decimales.
- **Botones del carrito:** mismo orden y misma rejilla de 3 columnas que el POS web (crédito,
  descontar productos, reservar carrito, calcular cambio, vender, destruir) y se ajustan al ancho
  disponible. CERRAR CAJA quedó junto a BÚSQUEDA, como en el web.
- **Campos numéricos:** montos de apertura/cierre de caja, cantidad recibida (cambio) y cantidad
  del carrito solo aceptan dígitos y **un** punto decimal. Se bloquean letras, comas, signos,
  espacios y el segundo punto, tanto al teclear como al pegar.
- **Ajustes:** solo impresora (ancho, cajón, prueba). La URL y la clave de la API no se muestran
  ni se editan desde la app; viven en `config.json`. Se abre desde la ventana principal, no desde
  el login.
- Login: los campos de usuario y contraseña comparten plantilla (mismo alto y radio). La entrada
  sin conexión es automática: si el servidor no responde, se valida la contraseña contra el
  acceso guardado de ese usuario (hasta 2 días).
- **Permisos por usuario:** al iniciar sesión se guardan los permisos que devuelve el servidor
  (crédito = menú 11, descuento = menú 12, venta al mayor). Se refrescan en cada inicio con
  conexión y siguen valiendo sin conexión con la sesión guardada. Sin permiso, los botones no
  aparecen y los productos "al mayor" se filtran también en el catálogo local.
- **Varios cajeros por POS:** se guarda una sesión por usuario (usuario, `bss_id`, `sucursal_id`,
  token y hash de la clave). Cada venta y cada caja se sincronizan con el token del cajero que
  las hizo, aunque otro cajero esté usando la caja en ese momento.
- **Cada rol ve lo suyo:** el **cajero** solo ve sus ventas pendientes y enviadas, y solo sube
  las suyas. El **administrador** ve la cola completa del equipo (pendientes y enviadas, con la
  columna CAJERO), puede filtrar por cajero y sincronizar lo de los demás; cada venta conserva
  el id del usuario que la procesó.

75 tests en verde (`dotnet test`).
