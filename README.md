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

47 tests en verde (`dotnet test`).
