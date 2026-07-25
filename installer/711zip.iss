; Inno Setup script for 711zip.
; Compiled by tools/build-dist.ps1, which passes PublishDir and AppVersion.

#ifndef AppVersion
  #define AppVersion "0.1.0"
#endif
#ifndef PublishDir
  #define PublishDir "..\src\App\bin\x64\Release\net10.0-windows10.0.19041.0\win-x64\publish"
#endif

#define AppName "711zip"
#define AppExe "Zip711.exe"
#define AppPublisher "metawka"
#define AppUrl "https://github.com/metawka/711zip"

[Setup]
AppId={{A7F1E2C4-711D-4B11-9E7A-1A2B3C4D5E6F}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}
DefaultDirName={autopf}\711zip
DefaultGroupName=711zip
DisableProgramGroupPage=yes
UninstallDisplayName=711zip
UninstallDisplayIcon={app}\{#AppExe}
OutputDir=..\dist
OutputBaseFilename=711zip-{#AppVersion}-setup
Compression=lzma2/ultra64
SolidCompression=yes
LZMANumBlockThreads=4
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=modern
MinVersion=10.0.17763

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{group}\711zip"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\711zip"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,711zip}"; Flags: nowait postinstall skipifsilent
