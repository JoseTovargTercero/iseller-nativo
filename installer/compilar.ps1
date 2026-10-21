# Compila el instalador del POS: build Release + Inno Setup.
# Uso:  powershell -ExecutionPolicy Bypass -File installer\compilar.ps1
#
# Requiere .NET SDK y Inno Setup 6 (winget install JRSoftware.InnoSetup).
# El .exe resultante queda en dist\iSellerPOS-Setup-<version>.exe

$ErrorActionPreference = 'Stop'
$raiz = Split-Path $PSScriptRoot -Parent

$iscc = Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'
if (-not (Test-Path $iscc)) { $iscc = 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe' }
if (-not (Test-Path $iscc)) {
    throw "No se encontró ISCC.exe (Inno Setup 6). Instálalo con: winget install JRSoftware.InnoSetup"
}

# La versión sale de App.VersionApp para no tenerla en dos sitios.
$app = Join-Path $raiz 'src\Iseller.Pos.App\App.xaml.cs'
$version = (Select-String -Path $app -Pattern 'VersionApp\s*=\s*"([^"]+)"').Matches[0].Groups[1].Value
if (-not $version) { throw "No se pudo leer VersionApp de $app" }

Push-Location $raiz
try {
    Write-Output "Compilando Release $version…"
    dotnet build IsellerPos.sln -c Release --nologo -v q
    if ($LASTEXITCODE -ne 0) { throw "Falló el build Release." }

    Write-Output "Generando el instalador…"
    & $iscc "/DAppVersion=$version" (Join-Path $PSScriptRoot 'iSellerPOS.iss')
    if ($LASTEXITCODE -ne 0) { throw "Falló Inno Setup." }
}
finally {
    Pop-Location
}
