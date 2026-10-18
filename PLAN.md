# POS nativo iSeller — Plan por fases

**Stack:** WPF + .NET Framework 4.8 + SQLite + HTTP contra el backend PHP actual.
**Objetivo:** vender sin internet, con catálogo y tasas locales, login offline y sincronización
idempotente al reconectar. Debe correr en Windows 7 SP1, 8.1, 10 y 11.

---

## 1. Decisiones tomadas

| Tema | Decisión | Motivo |
|---|---|---|
| UI | **WPF nativo, sin WebView2** | WebView2 en Windows 7 está congelado en la versión 109 y sin soporte. WPF con .NET 4.8 cubre todo el parque. |
| Runtime | **.NET Framework 4.8** | Preinstalado en Windows 10/11; instalable en 7 SP1 y 8.1. Sin runtime moderno ni despliegues self-contained. |
| Datos locales | **SQLite** | Un archivo, transaccional, sin servicio ni instalación. |
| Backend | **El PHP actual sigue siendo la autoridad** | No se duplican reglas de negocio: el nativo consume y sincroniza. |
| Repositorio | **Separado**, en `C:\xampp\pos-nativo` | El repo web es la raíz de Apache; los binarios y fuentes .NET no deben quedar servidos por HTTP. Ciclo de release distinto. |
| Sincronización | **Outbox + UUID por venta** | El servidor ya tiene `uniq_orden_id_pedido`, así que reintentar es seguro. |
| Precios | **Port exacto de `CalculadoraPrecios`** | El precio debe coincidir al centavo con la web, incluso en sus rarezas. |
| Clave offline | **PBKDF2-SHA256 (100k iteraciones)** | .NET 4.8 no trae Argon2 sin dependencias extra. Guardada con DPAPI. |
| Base local | **SQLite vía `System.Data.SQLite`** | `Microsoft.Data.Sqlite` arrastra un choque de `System.Memory` en net48. |
| Tasa vieja | **Bloquear a las 48 h sin actualizar** | Decisión del dueño: pasado ese plazo la caja exige conexión. |
| Sobreventa | **Permitir stock negativo y marcarlo** | Nunca frenar una venta real; el faltante se revisa al sincronizar. |
| Login offline | **2 días** | Decisión del dueño. |
| Impresión | **58 y 80 mm USB, configurable por equipo** | El usuario elige el ancho en la configuración de la caja. |
| Cajón monedero | **Opcional** | Si no existe o falla, la venta continúa. |
| Equipos | **Windows 7 SP1, 10 y 11** | Decisión del dueño. |
| Entrada | **Teclado + pantalla táctil** | La misma UI sirve para ambos. |

### Por qué no dentro del repo actual

- `htdocs/iseller-tiendas.com` es público vía Apache; `bin/`, `obj/`, `.dll` y fuentes no tienen por qué ser accesibles.
- La app se actualiza con instalador y firma; la web se despliega por FTP/git. Mezclarlos complica ambos.
- No hay código compartido: el contrato es HTTP/JSON. Un monorepo no aporta nada aquí.

Los cambios de backend (endpoints nuevos) sí van en el repo web, como cualquier otra página PHP.

---

## 2. Arquitectura

```
┌──────────────────────────── POS.exe (WPF / net48) ────────────────────────────┐
│                                                                              │
│  Vistas (XAML)  ──  ViewModels  ──  Servicios de dominio                     │
│                                          │                                   │
│              ┌───────────────────────────┼───────────────────────────┐       │
│              │                           │                           │       │
│        CalculadoraPrecios          Repositorios SQLite         Sincronizador│
│        (port del PHP)              (%LOCALAPPDATA%)            (outbox)     │
│              │                           │                           │       │
│              └───────────────────────────┴───────────────────────────┘       │
│                                                                              │
│  Impresión ESC/POS (raw)     Lector de barras (teclado)     DPAPI (token)     │
└──────────────────────────────────┬───────────────────────────────────────────┘
                                   │ HTTPS / JSON
                                   ▼
                 Backend PHP actual  +  endpoints api/pos_*.php
```

### Proyectos

- `Iseller.Pos.App` — WPF, tema, vistas, navegación, atajos de teclado.
- `Iseller.Pos.Core` — dominio puro sin WPF: precios, modelos, reglas de venta, contratos de sync.
- `Iseller.Pos.Tests` — xUnit. Los tests de precios son la red de seguridad del negocio.

La capa SQLite y la de sync viven en `Core` al principio. Si crecen, se separan en
`Iseller.Pos.Data` / `Iseller.Pos.Sync` sin tocar la UI.

---

## 3. Reglas que no se pueden romper

1. **Idempotencia por `id_pedido`.** El UUID se genera en la caja y no cambia en los reintentos.
2. **Precios congelados.** La venta guarda los precios y las tasas usadas. Una tasa nueva no
   reescribe ventas viejas.
3. **El servidor manda en stock, pero la caja no se bloquea por stock.** Se permite stock
   negativo, la venta queda marcada y el reporte de sincronización muestra el faltante.
4. **Nunca perder una venta local.** Si el envío falla, la fila queda `pendiente` con el error.
5. **El total que ve el cajero es el que se envía.** El servidor valida la suma de las líneas
   contra el total; si no cuadra, rechaza esa venta (no la corrige en silencio).
6. **Credenciales.** Nunca en texto plano ni en SQLite. Token y hash local cifrados con DPAPI
   (`CurrentUser`), y el archivo de base con ACL del usuario de Windows.
7. **TLS 1.2 obligatorio.** En Windows 7 hay que habilitarlo a nivel de sistema; el instalador
   lo verifica y avisa.
8. **Tasa vieja = 48 h.** Pasado ese plazo sin actualizar, la caja bloquea la venta hasta
   conectar. Antes de eso solo avisa con la antigüedad.

---

## 4. Fases

Las duraciones son para un desarrollador a tiempo completo. Cada fase termina con algo usable
y verificable, no con "código escrito".

### Fase 0 — Fundaciones ✅ (hecho, ~2 días)

**Entregado**
- Repo y solución `IsellerPos.sln` (App, Core, Tests) que compilan con `dotnet build`.
- `Directory.Build.props`, `.gitignore`, README.
- `Themes/Theme.xaml`: paleta completa del POS web (fondo, tarjeta, borde, mint, texto,
  danger, info, warning, gradiente) y estilos base de ventana, botones, inputs, tabs,
  tarjetas y tablas.
- `MainWindow` esqueleto con cabecera, barra lateral y área de contenido.
- `CalculadoraPrecios` portada desde `configurar/_calculadrora_precios.php`, incluidas
  las rarezas de `formatPeso()`.
- 9 tests xUnit con valores generados por el PHP real.
- Bug de precios > $999.99 corregido en los dos PHP (`configurar/` y `iseller_store/core/`)
  y en el port nativo.
- `db/esquema.sql` con catálogo, stock, combos, clientes, tasas, ventas y caja.

**Criterio de aceptación:** `dotnet build` y `dotnet test` en verde; la ventana abre con el
tema oscuro. *(El test de apertura de ventana es manual: `dotnet run`.)*

---

### Fase 1 — Login, catálogo y tasas offline (~3 semanas)

**Estado: implementada en la app** (login online/offline, SQLite, descarga de catálogo,
búsqueda, precios y caché de imágenes de producto).

**Objetivo:** abrir la app sin internet, con el catálogo y las tasas del último contacto.

**App**
- Pantalla de login. Online valida contra `/api/pos_login.php`; guarda token y hash local
  (Argon2id con sal) cifrados con DPAPI.
- Desbloqueo offline con el hash local, vigencia de **2 días** desde el último login válido.
  Se guarda **una sesión por cajero** (usuario, `bss_id`, `sucursal_id` y token), porque en un
  mismo POS pueden entrar varias personas el mismo día; cada venta/caja se sincroniza con el
  token de su cajero.
- Descarga de catálogo: productos, stock de la sucursal, combos, clientes, configuración
  (tickets, cortes, registro de clientes) y tasas.
- Repositorios SQLite + migraciones (leer `PRAGMA user_version` y aplicar scripts).
- Búsqueda por nombre y código de barras. Normalizar acentos y mayúsculas; para pocos miles
  de productos alcanza con prefijo/substring con scoring. Se descarta Fuse.js.
- Caché de imágenes en `%LOCALAPPDATA%\iSeller\img`; descarga bajo demanda.

**Backend (repo web)** — ✅ implementado
- `POST /api/pos_login.php` con token Bearer; tabla `pos_tokens` (SHA-256 del token).
- `GET /api/pos_catalogo.php` con snapshot versionado (hash) y `304` si no cambió.
- `GET /api/pos_tasas.php` y `GET /api/pos_heartbeat.php`.

**Aceptación**
- Con el servidor apagado, un usuario que ya inició sesión entra y ve el catálogo completo
  (hasta 2 días desde el último login válido).
- Al tercer día sin conexión, la app exige conectar antes de abrir.
- Los precios de 3 productos elegidos coinciden con la web al centavo.
- Un usuario sin login previo no puede entrar offline.
- Borrar el token en el servidor y reconectar expulsa la sesión local.

Verificado el 18/10/2026 con el negocio 3 (YOLA MARKET 1): 561 productos, 2 combos con stock
armable, y `Harina Pan` en `$1,67 / 1.674,00 Bs / 7.000 Cop` tanto en la app como en el PHP.

---

### Fase 2 — Carrito, venta offline y ticket (~3-4 semanas)

**Estado: implementada.** Carrito persistente, venta local con precios y tasas congelados,
ticket ESC/POS 58/80 mm configurable, cajón monedero opcional y atajos. Pendiente de probar
en hardware real (no hay impresora conectada).

**Objetivo:** cobrar y entregar ticket sin red.

**App**
- Carrito con atajos del POS actual: `B` búsqueda, `V` vender, `C` cambio, `+`/`-`,
  números 1-9 para más vendidos, `Esc` cerrar.
- Lector de barras como teclado, con detección de ráfaga (< 100 ms entre teclas).
- Ventas al mayor y combos con el mismo comportamiento del POS web.
- Venta a crédito (despacho 2) y descuento (despacho 3) con sus permisos.
- Guardado transaccional en `ventas` con estado `pendiente`; nunca se pierde el carrito.
- Ticket: ESC/POS a 58 u 80 mm, ancho configurable por equipo. Impresión raw al spooler.
  Si no hay impresora, la venta se guarda igual y solo se avisa.
- Apertura de cajón monedero opcional al cobrar en efectivo; si no existe o falla, la venta
  continúa.
- Apertura/cierre de caja **todavía no**; se maneja en la fase 4.

**Backend**
- Ninguno nuevo en esta fase (se reutiliza el contrato de la fase 3).

**Aceptación**
- 20 ventas seguidas sin red: todas en SQLite, totales correctos, ticket impreso.
- Cerrar la app a mitad de una venta no deja una venta a medias ni un carrito corrupto.
- Reiniciar la app conserva el carrito activo.
- Con una impresora de 58 mm y otra de 80 mm, el ticket sale con el ancho configurado.
- Desconectar la impresora o el cajón a mitad de turno no impide cobrar.

Verificado el 18/10/2026 con el negocio 3: venta completa desde la UI (2 productos, cliente,
pago Punto) guardada en SQLite con UUID, totales `$4,84 / 4.848,00 Bs / 20.200 Cop`, ítems y
tasas congeladas. El carrito sobrevive al reinicio (test de repositorio). Ticket validado a
nivel de bytes y de ancho de línea para 58 y 80 mm; falta la prueba en impresora física.

Notas de alcance:
- `1-9` agrega el resultado N de la búsqueda; el ranking real de más vendidos llega en la fase 3.
- El lector de barras funciona escribiendo en el buscador y con `Enter`, o con una ráfaga de
  dígitos (< 100 ms) fuera de un campo de texto.
- "Reservar carrito" y la pestaña "Enviados" también quedaron implementadas.

---

### Fase 3 — Sincronización (~3 semanas)

**Estado: implementada.** Lote idempotente, envío automático al guardar, reintento cada 60 s y
al abrir la app, refresco de catálogo/stock tras sincronizar y pestaña "Enviados".

**Objetivo:** subir ventas pendientes sin duplicar y bajar catálogo/stock actualizado.

**App**
- Outbox: al guardar, intenta enviar; si falla, reintenta cada 60 s y al abrir la app.
- Envío en lote con `id_pedido`; procesa la respuesta por venta.
- La venta solo pasa a `enviada` cuando el servidor confirma esa venta.
- Refresco de catálogo/tasas tras sincronizar.
- Pantalla "No guardados" con estado, intentos y último error por venta. Muestra las ventas de
  **todos** los cajeros con su columna CAJERO, filtro por cajero y envío por cajero o de todo,
  para que el administrador pueda sincronizar lo que quedó de cada uno.

**Backend** — ✅ implementado
- `POST /api/pos_ventas.php` (lote). Por cada venta: insertar orden + artículos + stock en una
  transacción, devolver `{id_pedido, ok, orden_id, duplicada, error}`.
- Idempotente por `uniq_orden_id_pedido`; el error 1062 se trata como "ya estaba".
- La sucursal y el usuario salen del token, no del cuerpo.
- La fecha de la venta es la de la caja (`fecha_local`), no la de sincronización.
- Las líneas se validan contra los totales enviados; si no cuadran se acepta y se devuelve
  `advertencias` (no se bloquea una venta real).
- El stock se descuenta y se devuelve `faltantes` cuando la venta dejó el producto bajo cero
  (se guarda en 0, igual que el POS web).

**Aceptación**
- Simular 30 ventas offline, reconectar y ver exactamente 30 órdenes en MySQL.
- Cortar la red justo después de que el servidor insertó pero antes del ACK: al reintentar
  no se duplica.
- Dos ventas idénticas el mismo día y misma caja generan dos órdenes.
- El stock local queda igual al del servidor tras sincronizar.

Verificado el 18/10/2026:
- Envío manual del mismo lote dos veces → la segunda responde `duplicada: true` con el mismo
  `orden_id`, y queda **una sola** orden y **un solo** artículo.
- Venta real hecha en la app (2 productos, cliente, Punto) → sincronizada automáticamente al
  abrir: orden 28 en MySQL con `$4,84 / 4.848,00 Bs / 20.200 Cop`, 2 artículos y stock de
  Harina Pan 34,5 → 32,5.
- Repositorio: `MarcarEnviada` / `RegistrarFallo` cubiertos por test (39/39).

---

### Fase 4 — Caja offline (~2 semanas)

**Estado: implementada.** Apertura/cierre con UUID y sincronización, bloqueo de ventas con
caja cerrada y reapertura por red.

**Objetivo:** apertura, cierre y reapertura sin depender de la red.

**App**
- Apertura y cierre con los mismos campos del POS web (fondo, contado, digital, observaciones).
- Cada movimiento se guarda en SQLite con UUID y estado pendiente; se envía junto con las
  ventas (primero ventas, después caja, para que el cierre sume el día completo).
- Regla de la web: solo se exige a cajeros (nivel 2) con `cortes_caja = 1`.
- Caja cerrada bloquea la venta y muestra "Reabrir caja" (requiere conexión).
- La reapertura borra el cierre local y el del servidor por UUID.

**Backend** — ✅ implementado
- `POST /api/pos_caja.php` con `accion: guardar|reabrir`.
- Idempotente por UUID; se agregó la columna `uuid` (nullable, con índice único) a
  `cortes_de_caja` de forma aditiva.
- El cierre calcula los totales del sistema desde `orden` (misma lógica que
  `registro_cierre.php`) y guarda las diferencias contra lo contado.
- Reabrir borra el cierre por UUID (no "el último del día"), así un reintento no borra dos.

**Aceptación**
- Abrir caja sin red, vender, cerrar y sincronizar deja los mismos montos que la web.
- Un cierre enviado dos veces no duplica el registro.

Verificado el 18/10/2026:
- Apertura y cierre desde la UI, guardados en SQLite y sincronizados; el servidor devolvió
  `corte_id` y el cierre calculó diferencias contra lo contado.
- Mismo movimiento enviado dos veces → `duplicada: true`, un solo registro.
- Reabrir por UUID borró el cierre en el servidor; un segundo intento respondió `ya_estaba`.
- 44/44 tests en verde (incluye reglas de caja y transiciones del repositorio).

---

### Fase 5 — Endurecimiento y despliegue (~2-3 semanas)

**Objetivo:** instalarlo en cajas reales y actualizarlo sin visitar la tienda.

- Instalador **Inno Setup** (soporta Windows 7; instalación por usuario, sin admin).
- **Autoactualización**: `api/pos_version.php` + `Actualizador` (compara versión, descarga el
  ZIP, valida SHA-256 y lo aplica al cerrar la app). Ya implementada; falta firmar el ZIP con
  Authenticode cuando haya certificado.
- Migraciones de SQLite versionadas y respaldo previo a cada actualización.
- Log local rotativo + comando "Exportar diagnóstico" (sin datos sensibles).
- Plan de piloto: 1 sucursal, 1 semana, comparación diaria contra la web.
- Manual de instalación y de recuperación (caja rota, disco lleno, impresora).

**Aceptación**
- Instalación limpia en Windows 7 SP1, 10 y 11.
- Actualización de versión conserva catálogo, carrito y ventas pendientes.
- Checklist de piloto sin diferencias de totales ni ventas perdidas.

---

## 5. Contratos con el backend

Implementado en el repo web como archivos sueltos dentro de `api/`, igual que el resto de
la API de la app Android. Autenticación doble: cabecera `X-Api-Key` (POS_API_KEY del `.env`,
fail-closed) y `Authorization: Bearer <token>` de usuario (tabla `pos_tokens`).
Todas las respuestas JSON. Errores con `{ "ok": false, "error": "..." }`.

Estado: `pos_login`, `pos_catalogo`, `pos_tasas` y `pos_heartbeat` **implementados y probados**
(Fase 1). `pos_ventas` y `pos_caja` son de las fases 3 y 4.

### `POST /api/pos_login.php`

`usuario` y `password`. `sucursal_id` es opcional; si el negocio tiene varias sucursales y no
se manda, responde `409` con la lista para que la caja muestre el selector.

```json
// request
{ "usuario": "cajero1", "password": "...", "sucursal_id": 7 }

// response
{
  "ok": true,
  "token": "…",
  "expira_en": 2592000,
  "usuario": { "id": 12, "nombre": "Ana", "usuario": "cajero1", "nivel": 2 },
  "bss_id": 3,
  "sucursal": { "id": 7, "nombre": "CENTRO" },
  "permisos": [11, 12],
  "configuracion": { "tickets": 1, "bs_ticket": 0, "cortes_caja": 1, "registro_clientes": 0 },
  "puede_vender_al_mayor": false,
  "servidor_en": "2026-10-18T12:00:00-04:00"
}
```

### `GET /api/pos_catalogo.php?v=<hash>`

Si `v` coincide con la versión actual, `304 Not Modified`.
Si no:

```json
{
  "ok": true,
  "version": "sha256:…",
  "servidor_en": "2026-10-18T12:00:00Z",
  "tasas": {
    "peso_dolar": 4000, "peso_bolivar": 25, "dolar_bolivar": 40,
    "bolivar_peso": 0.22, "bcv": 40,
    "data_monedas": { "precio_dolar_visible": true, "precio_peso_visible": true }
  },
  "productos": [
    {
      "id": 123, "nombre": "ARROZ 1KG", "codigo_barras": "7591234567890",
      "origen": "v", "precio_compra": 10, "cantidad_unidades": 1,
      "porcentaje": 30, "mayor": "0", "es_combo": 0,
      "stock": 24, "foto": "assets/img/stock/optimized/123.webp",
      "combo_items": []
    }
  ],
  "clientes": [{ "cedula": "V12345678", "nombre": "…", "telefono": "…" }]
}
```

Los precios **no** se envían calculados: la app los calcula con la misma fórmula y las tasas
recibidas. Así una tasa nueva recalcula todo el catálogo sin reenviar productos.

### `POST /api/pos_ventas.php` (fase 3, pendiente)

```json
{
  "lote_id": "uuid",
  "ventas": [
    {
      "id_pedido": "uuid",
      "fecha_local": "2026-10-18T14:32:10-04:00",
      "metodo_pago": "option4",
      "despacho": 1,
      "total_usd": 13.00, "total_bs": 520.00, "total_cop": 52000,
      "cliente": { "cedula": "", "nombre": "", "telefono": "" },
      "usuario_id": 12,
      "tasas": { "...": "copia de las tasas usadas" },
      "productos": [
        { "id": 123, "nombre": "ARROZ 1KG", "qty": 1,
          "price": 13.0, "pricePeso": 52000, "priceBolivar": 520.0,
          "price_C": 10, "price_C_Bs": 400, "price_C_Cop": 40000,
          "mayor": "0", "cantidadPaca": 1 }
      ]
    }
  ]
}
```

```json
{
  "ok": true,
  "servidor_en": "2026-10-18T18:33:00Z",
  "resultados": [
    { "id_pedido": "uuid", "ok": true, "orden_id": 9876, "duplicada": false },
    { "id_pedido": "uuid2", "ok": false, "error": "Stock insuficiente…" }
  ]
}
```

### Otros

- `GET /api/pos_tasas.php` — implementado. Refresca solo las tasas.
- `GET /api/pos_heartbeat.php` — implementado. Hora del servidor y validación del token.
- `GET /api/pos_ventas_ultimas.php` — fase 3.
- `GET /api/pos_top_vendidos.php` — fase 3.
- `POST /api/pos_caja.php` — fase 4. `{ "id": "uuid", "tipo": "apertura|cierre", "montos": {...}, "observaciones": "" }`.

La app sella la fecha de descarga de las tasas con `servidor_en`, no con su reloj: así el
límite de 48 h no depende del reloj de la caja.

### Tabla de tokens (MySQL)

```sql
CREATE TABLE pos_tokens (
    token_hash  CHAR(64) PRIMARY KEY,
    id_user     INT NOT NULL,
    bss_id      INT NOT NULL,
    sucursal_id INT NULL,
    creado      DATETIME NOT NULL,
    expira      DATETIME NOT NULL,
    revocado    TINYINT NOT NULL DEFAULT 0
);
```

---

## 6. Riesgos

| Riesgo | Impacto | Mitigación |
|---|---|---|
| Tasas viejas sin internet | Precios incorrectos | Avisar con la antigüedad; **bloquear a las 48 h**. Congelar tasa por venta. |
| Sobreventa offline | Stock negativo | **Permitir negativo** y marcarlo; reporte de diferencias al sincronizar. |
| Reloj de la caja desfasado | Fechas y reportes mal | Guardar UTC + offset; comparar con `servidor_en`. |
| Windows 7 sin TLS 1.2 | No conecta | Verificación en instalador; parche de sistema documentado. |
| Impresora o cajón ausente | No imprime / no abre | Impresión 58/80 configurable y aislada; fallback a PDF. El cajón es opcional y su fallo no corta la venta. |
| Bug de precios > 999.99 | Precio en dólares = 1 | **Corregido** en `configurar/` y `iseller_store/core/`, y en el port nativo. Test de regresión. |
| Antivirus bloquea el .exe | No arranca | Firma de código y instalador firmado. |
| SQLite corrupta | Pérdida de ventas | Transacciones + respaldo diario + WAL. |

---

## 7. Decisiones tomadas (18/10/2026)

1. **Tasa vieja:** se avisa y se permite vender hasta **48 h**; después se bloquea hasta conectar.
2. **Sobreventa:** se permite stock negativo. La venta se marca y se revisa al sincronizar.
3. **Impresoras:** 58 y 80 mm USB. El ancho se configura por equipo. El cajón monedero es
   opcional; si no existe o falla, la venta continúa.
4. **Login offline:** **2 días** desde el último login válido.
5. **Caja:** se replica la regla de la web (`cortes_caja`): si está activo, exige apertura;
   si no, permite vender.
6. **Equipos:** Windows 7 SP1 y Windows 10/11. Entrada por teclado y por pantalla táctil.
7. **Precio > $999.99:** corregido en ambos lados (PHP y nativo) en la Fase 0.

---

## 8. Lo que NO entra en esta versión

- Edición de productos o precios desde la caja.
- Anulación de ventas offline (solo se puede anular con red).
- Reportes offline completos (solo últimas ventas y totales del día).
- Multi-sucursal simultánea en una misma instalación.
- Sincronización bidireccional de clientes creados offline (se envían con la venta).
- App móvil o web nueva.

---

## 9. Orden de arranque recomendado

1. Las decisiones de §7 ya están cerradas.
2. Implementar en el repo web `login`, `catalogo` y `tasas` (Fase 1 backend) contra una
   tabla de tokens; probar con `curl`/Postman.
3. Construir en la app el login offline + repositorios + pantalla de catálogo.
4. Recién después, carrito y venta.
5. La sincronización se prueba con el servidor real y un script que corte la red.
