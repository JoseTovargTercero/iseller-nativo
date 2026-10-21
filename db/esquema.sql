-- Esquema local del POS nativo (SQLite).
-- Todo lo que la caja necesita para vender sin internet.
-- La nube sigue siendo la autoridad final; esta base es la copia de trabajo.

PRAGMA user_version = 2;

CREATE TABLE IF NOT EXISTS meta (
    clave TEXT PRIMARY KEY,
    valor TEXT
);

-- Catalogo descargado del servidor.
CREATE TABLE IF NOT EXISTS productos (
    id                INTEGER PRIMARY KEY,
    nombre            TEXT    NOT NULL,
    nombre_busqueda   TEXT    NOT NULL DEFAULT '',
    codigo_barras     TEXT,
    origen            TEXT    NOT NULL DEFAULT 'v', -- 'v' venezolano, 'c' colombiano
    precio_compra     REAL    NOT NULL DEFAULT 0,
    cantidad_unidades REAL    NOT NULL DEFAULT 1,
    porcentaje        REAL    NOT NULL DEFAULT 0,
    mayor             TEXT    NOT NULL DEFAULT '0',
    es_combo          INTEGER NOT NULL DEFAULT 0,
    activo            INTEGER NOT NULL DEFAULT 1,
    foto              TEXT,
    actualizado_en    TEXT
);

CREATE INDEX IF NOT EXISTS ix_productos_codigo ON productos (codigo_barras);
CREATE INDEX IF NOT EXISTS ix_productos_busqueda ON productos (nombre_busqueda);

CREATE TABLE IF NOT EXISTS stock (
    producto_id INTEGER NOT NULL,
    sucursal_id INTEGER NOT NULL,
    cantidad    REAL    NOT NULL DEFAULT 0,
    PRIMARY KEY (producto_id, sucursal_id)
);

CREATE TABLE IF NOT EXISTS combo_items (
    combo_id INTEGER NOT NULL,
    nombre   TEXT    NOT NULL,
    cantidad REAL    NOT NULL,
    PRIMARY KEY (combo_id, nombre)
);

CREATE TABLE IF NOT EXISTS clientes (
    cedula   TEXT PRIMARY KEY,
    nombre   TEXT,
    telefono TEXT
);

-- Una sola fila (id = 1).
CREATE TABLE IF NOT EXISTS tasas (
    id                   INTEGER PRIMARY KEY CHECK (id = 1),
    peso_dolar           REAL    NOT NULL,
    peso_bolivar         REAL    NOT NULL,
    dolar_bolivar        REAL    NOT NULL,
    bolivar_peso         REAL    NOT NULL,
    bcv                  REAL    NOT NULL,
    precio_dolar_visible INTEGER NOT NULL DEFAULT 1,
    precio_peso_visible  INTEGER NOT NULL DEFAULT 1,
    data_monedas         TEXT    NOT NULL DEFAULT '{}',
    actualizado_en       TEXT    NOT NULL
);

-- Carrito en curso. Se guarda item por item para sobrevivir a un cierre de la app.
CREATE TABLE IF NOT EXISTS carrito_activo (
    producto_id INTEGER PRIMARY KEY,
    item_json   TEXT    NOT NULL,
    agregado_en TEXT    NOT NULL
);

-- Ventas locales. estado = pendiente | enviada | error.
-- id_pedido es el UUID que tambien usa el servidor (idempotencia).
CREATE TABLE IF NOT EXISTS ventas (
    id_pedido        TEXT PRIMARY KEY,
    fecha_local      TEXT    NOT NULL, -- ISO-8601 con offset
    metodo_pago      TEXT    NOT NULL, -- option1..option7
    despacho         INTEGER NOT NULL, -- 1 venta, 2 credito, 3 descuento
    total_usd        REAL    NOT NULL,
    total_bs         REAL    NOT NULL,
    total_cop        REAL    NOT NULL,
    cliente_cedula   TEXT,
    cliente_nombre   TEXT,
    cliente_telefono TEXT,
    usuario_id       INTEGER NOT NULL,
    usuario_nombre   TEXT    NOT NULL,
    estado           TEXT    NOT NULL DEFAULT 'pendiente',
    intentos         INTEGER NOT NULL DEFAULT 0,
    ultimo_error     TEXT,
    tasas_json       TEXT    NOT NULL, -- tasas y banderas usadas al cobrar
    productos_json   TEXT    NOT NULL, -- items con precios congelados
    pagos_json       TEXT,             -- desglose del pago fraccionado (option8)
    creada_en        TEXT    NOT NULL,
    enviada_en       TEXT
);

CREATE INDEX IF NOT EXISTS ix_ventas_estado ON ventas (estado);

-- Aperturas y cierres de caja. sincronizada_en NULL = pendiente.
CREATE TABLE IF NOT EXISTS caja (
    id              TEXT PRIMARY KEY,
    tipo            TEXT NOT NULL, -- apertura | cierre
    montos_json     TEXT NOT NULL,
    observaciones   TEXT,
    usuario_id      INTEGER NOT NULL,
    creada_en       TEXT NOT NULL,
    sincronizada_en TEXT
);

CREATE INDEX IF NOT EXISTS ix_caja_pendiente ON caja (sincronizada_en);

-- Carritos apartados para retomar después.
CREATE TABLE IF NOT EXISTS carritos_reservados (
    id             TEXT PRIMARY KEY,
    cliente        TEXT NOT NULL,
    productos_json TEXT NOT NULL,
    creada_en      TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_reservados_creada ON carritos_reservados (creada_en);
