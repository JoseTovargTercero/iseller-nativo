; Instalador del POS nativo de iSeller.
; Compilar con:  ISCC.exe /DAppVersion=1.0.0 installer\iSellerPOS.iss
; (la versión por defecto está abajo por si se compila sin /D)

#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif

#define AppName "iSeller POS"
#define AppExe "IsellerPos.exe"
#define AppPublisher "iSeller"
#define AppDir "iSeller POS"

[Setup]
; Identificador estable: no cambiar o Windows tratará cada versión como otra app.
AppId={{8E2C1E41-7C3A-4C57-9F0B-5C6A7C1D2F10}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={localappdata}\Programs\{#AppDir}
DefaultGroupName={#AppName}
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
SetupIconFile=..\src\Iseller.Pos.App\Assets\logo.ico
OutputDir=..\dist
OutputBaseFilename=iSellerPOS-Setup-{#AppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
; Sin privilegios de administrador: instala para el usuario actual (caja sin admin).
PrivilegesRequired=lowest
; Windows 7 SP1 en adelante, igual que el objetivo de la app.
MinVersion=6.1sp1
DisableProgramGroupPage=yes
CloseApplications=yes
RestartApplications=no
; x86compatible cubre Windows de 32 y 64 bits.
ArchitecturesAllowed=x86compatible

[Languages]
Name: "espanol"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "desktopicon"; Description: "Crear un acceso directo en el escritorio"; GroupDescription: "Accesos directos:"

[Files]
; Todo el build Release, sin símbolos de depuración.
Source: "..\src\Iseller.Pos.App\bin\Release\net48\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb,*.xml"

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{group}\Desinstalar {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "Abrir {#AppName}"; Flags: nowait postinstall skipifsilent

[Code]
// .NET Framework 4.8 es obligatorio. Release >= 528040 = 4.8 (528372 = 4.8 en Win7).
function TieneNet48(): Boolean;
var
  release: Cardinal;
begin
  Result :=
    (RegQueryDWordValue(HKLM, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', release) and (release >= 528040))
    or
    (RegQueryDWordValue(HKLM, 'SOFTWARE\WOW6432Node\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', release) and (release >= 528040));
end;

function InitializeSetup(): Boolean;
begin
  Result := TieneNet48();
  if not Result then
  begin
    MsgBox('Este equipo necesita .NET Framework 4.8 para abrir el POS.' + #13#10 +
           'Descárgalo en https://dotnet.microsoft.com/download/dotnet-framework/net48 y vuelve a ejecutar el instalador.',
           mbCriticalError, MB_OK);
  end;
end;
