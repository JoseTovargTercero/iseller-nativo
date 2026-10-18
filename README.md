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
src/Iseller.Pos.App     UI WPF (ventana, temas, vistas)
src/Iseller.Pos.Core    Dominio puro: precios, modelos, contratos de sync
src/Iseller.Pos.Tests   Tests (xUnit)
db/esquema.sql          Esquema SQLite local
PLAN.md                 Plan por fases, contratos y decisiones
```

## Estado

Fase 0 completada: solución compilable, tema del POS extraído a XAML, cálculo de precios
portado desde PHP y validado con 8 tests contra valores generados por el PHP real.

Ver [PLAN.md](PLAN.md).
