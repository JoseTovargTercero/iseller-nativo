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
                 Backend PHP actual  +  endpoints /api/pos/*
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
3. **El servidor manda en stock.** La caja vende con el último snapshot; al sincronizar se
   refresca. La política de sobreventa se decide en §8.
4. **Nunca perder una venta local.** Si el envío falla, la fila queda `pendiente` con el error.
5. **El total que ve el cajero es el que se envía.** El servidor valida la suma de las líneas
   contra el total; si no cuadra, rechaza esa venta (no la corrige en silencio).
6. **Credenciales.** Nunca en texto plano ni en SQLite. Token y hash local cifrados con DPAPI
   (`CurrentUser`), y el archivo de base con ACL del usuario de Windows.
7. **TLS 1.2 obligatorio.** En Windows 7 hay que habilitarlo a nivel de sistema; el instalador
   lo verifica y avisa.

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
  las rarezas de `formatPeso()` y del casteo de `number_format`.
- 8 tests xUnit con valores generados por el PHP real.
- `db/esquema.sql` con catálogo, stock, combos, clientes, tasas, ventas y caja.

**Criterio de aceptación:** `dotnet build` y `dotnet test` en verde; la ventana abre con el
tema oscuro. *(El test de apertura de ventana es manual: `dotnet run`.)*

---

### Fase 1 — Login, catálogo y tasas offline (~3 semanas)

**Objetivo:** abrir la app sin internet, con el catálogo y las tasas del último contacto.

**App**
- Pantalla de login. Online valida contra `/api/pos/login`; guarda token y hash local
  (Argon2id con sal) cifrados con DPAPI.
- Desbloqueo offline con el hash local, vigencia configurable (por defecto 14 días).
- Descarga de catálogo: productos, stock de la sucursal, combos, clientes, configuración
  (tickets, cortes, registro de clientes) y tasas.
- Repositorios SQLite + migraciones (leer `PRAGMA user_version` y aplicar scripts).
- Búsqueda por nombre y código de barras. Normalizar acentos y mayúsculas; para pocos miles
  de productos alcanza con prefijo/substring con scoring. Se descarta Fuse.js.
- Caché de imágenes en `%LOCALAPPDATA%\iSeller\img`; descarga bajo demanda.

**Backend (repo web)**
- `POST /api/pos/login` con token Bearer; tabla `pos_tokens` (guardar SHA-256 del token).
- `GET /api/pos/catalogo` con snapshot versionado (hash). Sin `updated_at` en `productos`
  ni `stock`, el snapshot completo es lo correcto por ahora; si pesa, agregar `updated_at`
  y pasar a deltas.
- `GET /api/pos/tasas` y `GET /api/pos/heartbeat` (hora del servidor).

**Aceptación**
- Con el servidor apagado, un usuario que ya inició sesión entra y ve el catálogo completo.
- Los precios de 3 productos elegidos coinciden con la web al centavo.
- Un usuario sin login previo no puede entrar offline.
- Borrar el token en el servidor y reconectar expulsa la sesión local.

---

### Fase 2 — Carrito, venta offline y ticket (~3-4 semanas)

**Objetivo:** cobrar y entregar ticket sin red.

**App**
- Carrito con atajos del POS actual: `B` búsqueda, `V` vender, `C` cambio, `+`/`-`,
  números 1-9 para más vendidos, `Esc` cerrar.
- Lector de barras como teclado, con detección de ráfaga (< 100 ms entre teclas).
- Ventas al mayor y combos con el mismo comportamiento del POS web.
- Venta a crédito (despacho 2) y descuento (despacho 3) con sus permisos.
- Guardado transaccional en `ventas` con estado `pendiente`; nunca se pierde el carrito.
- Ticket: render ESC/POS 58/80 mm e impresión raw al spooler. Logo opcional como raster.
- Apertura/cierre de caja **todavía no**; se usa la caja existente o se bloquea según §8.

**Backend**
- Ninguno nuevo en esta fase (se reutiliza el contrato de la fase 3).

**Aceptación**
- 20 ventas seguidas sin red: todas en SQLite, totales correctos, ticket impreso.
- Cerrar la app a mitad de una venta no deja una venta a medias ni un carrito corrupto.
- Reiniciar la app conserva el carrito activo.

---

### Fase 3 — Sincronización (~3 semanas)

**Objetivo:** subir ventas pendientes sin duplicar y bajar catálogo/stock actualizado.

**App**
- Outbox: al guardar, intenta enviar; si falla, backoff exponencial (30 s → 30 min) y
  reintento al recuperar conexión y al abrir la app.
- Envío en lote con `id_pedido`; procesa la respuesta por venta.
- La venta solo pasa a `enviada` cuando el servidor confirma esa venta.
- Refresco de catálogo/tasas tras sincronizar.
- Pantalla "No guardados" y "Enviados" equivalentes a las pestañas actuales.

**Backend**
- `POST /api/pos/ventas` (lote). Por cada venta: insertar orden + artículos + stock en una
  transacción, devolver `{id_pedido, ok, orden_id, error}`.
- Confiar en `uniq_orden_id_pedido` para el reintento; el error 1062 significa "ya estaba".
- **Quitar** la validación de "venta idéntica" por total+fecha+cliente de `accion_carta.php`:
  hoy colapsa dos ventas legítimas iguales en una y responde `status: true`.
- Validar suma de líneas contra `total_usd`/`total_bs`/`total_cop` con tolerancia de 0.01.
- Devolver hora del servidor para compensar relojes desfasados.

**Aceptación**
- Simular 30 ventas offline, reconectar y ver exactamente 30 órdenes en MySQL.
- Cortar la red justo después de que el servidor insertó pero antes del ACK: al reintentar
  no se duplica.
- Dos ventas idénticas el mismo día y misma caja generan dos órdenes.
- El stock local queda igual al del servidor tras sincronizar.

---

### Fase 4 — Caja offline (~2 semanas)

**Objetivo:** apertura, cierre y reapertura sin depender de la red.

**App**
- Replicar localmente `consulta_apertura`, `registro_apertura` y `registro_cierre` con el
  mismo formulario (fondo, contado, digital, observaciones).
- Cada apertura/cierre es un registro con UUID y estado pendiente; se sincroniza igual que
  una venta.
- Reabrir caja requiere red (es una corrección administrativa); si no hay, se avisa.

**Backend**
- `POST /api/pos/caja` idempotente por UUID; reutilizar la lógica actual de los endpoints.

**Aceptación**
- Abrir caja sin red, vender, cerrar y sincronizar deja los mismos montos que la web.
- Un cierre enviado dos veces no duplica el registro.

---

### Fase 5 — Endurecimiento y despliegue (~2-3 semanas)

**Objetivo:** instalarlo en cajas reales y actualizarlo sin visitar la tienda.

- Instalador **Inno Setup** (soporta Windows 7; instalación por usuario, sin admin).
- Actualizador propio: descarga ZIP firmado, verifica SHA-256 + firma Authenticode y
  reemplaza al reiniciar. Sin dependencias externas.
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

Prefijo sugerido: `/api/pos/`. Autenticación `Authorization: Bearer <token>`.
Todas las respuestas JSON. Errores con `{ "ok": false, "error": "..." }`.

### `POST /api/pos/login`

```json
// request
{ "usuario": "cajero1", "clave": "...", "dispositivo": "POS-CAJA-01" }

// response
{
  "ok": true,
  "token": "…",
  "expira": "2026-11-17T23:59:59Z",
  "usuario": { "id": 12, "nombre": "Ana", "nivel": 2 },
  "bss_id": 3,
  "sucursal": { "id": 7, "nombre": "CENTRO" },
  "permisos": [11, 12],
  "configuracion": { "tickets": 1, "bs_ticket": 0, "cortes_caja": 1, "registro_clientes": 0 },
  "puede_vender_al_mayor": false
}
```

### `GET /api/pos/catalogo?v=<hash>`

Si `v` coincide con la versión actual, `304 Not Modified`.
Si no:

```json
{
  "ok": true,
  "version": "sha256:…",
  "generado_en": "2026-10-18T12:00:00Z",
  "tasas": {
    "peso_dolar": 4000, "peso_bolivar": 25, "dolar_bolivar": 40,
    "bolivar_peso": 0.22, "bcv": 40,
    "precio_dolar_visible": true, "precio_peso_visible": true,
    "actualizado_en": "2026-10-18T11:00:00Z"
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

### `POST /api/pos/ventas`

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

- `GET /api/pos/ventas/ultimas?limit=20` — para la tabla "Últimas ventas".
- `GET /api/pos/top-vendidos` — ranking de la sucursal.
- `POST /api/pos/caja` — `{ "id": "uuid", "tipo": "apertura|cierre", "montos": {...}, "observaciones": "" }`.
- `GET /api/pos/heartbeat` — `{ "ok": true, "servidor_en": "..." }`.

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
| Tasas viejas sin internet | Precios incorrectos | Mostrar antigüedad de la tasa y política en §8; congelar tasa por venta. |
| Sobreventa offline | Stock negativo | Política en §8 + reporte de diferencias al sincronizar. |
| Reloj de la caja desfasado | Fechas y reportes mal | Guardar UTC + offset; comparar con `servidor_en`. |
| Windows 7 sin TLS 1.2 | No conecta | Verificación en instalador; parche de sistema documentado. |
| Impresora desconocida | No imprime | Definir modelos en §8; capa de impresión aislada con fallback a PDF. |
| Bug de precios > 999.99 | Precio en dólares = 1 | Ya reproducido en test. Corregir en PHP y app a la vez, o dejarlo si el negocio lo asume. |
| Antivirus bloquea el .exe | No arranca | Firma de código y instalador firmado. |
| SQLite corrupta | Pérdida de ventas | Transacciones + respaldo diario + WAL. |

---

## 7. Decisiones pendientes del dueño (antes de la fase 1/2)

1. **Tasa vieja:** ¿máximo cuántas horas sin actualizar se permite vender? ¿Se bloquea o solo avisa?
2. **Sobreventa:** ¿permitir stock negativo (nunca frenar una venta real) o bloquear en 0?
3. **Impresoras:** marca/modelo y ancho (58/80 mm). ¿Hay cajón monedero?
4. **Login offline:** ¿cuántos días puede un cajero entrar sin conectar?
5. **Caja obligatoria:** ¿se puede vender sin apertura de caja registrada?
6. **Equipos:** ¿queda alguna Windows 7 u 8.1? ¿Pantalla táctil o teclado?
7. **Bug de precio > 999.99:** ¿se corrige en ambos lados o se mantiene por compatibilidad?

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

1. Cerrar las decisiones de §7 con el dueño.
2. Implementar en el repo web `login`, `catalogo` y `tasas` (Fase 1 backend) contra una
   tabla de tokens; probar con `curl`/Postman.
3. Construir en la app el login offline + repositorios + pantalla de catálogo.
4. Recién después, carrito y venta.
5. La sincronización se prueba con el servidor real y un script que corte la red.
