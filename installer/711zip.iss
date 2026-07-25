; Inno Setup script for 711-zip.
; Compiled by tools/build-dist.ps1, which passes PublishDir and AppVersion.

#ifndef AppVersion
  #define AppVersion "0.1.0"
#endif
#ifndef PublishDir
  #define PublishDir "..\src\App\bin\x64\Release\net10.0-windows10.0.19041.0\win-x64\publish"
#endif

#define AppName "711-zip"
#define AppExe "Zip711.exe"
#define AppPublisher "metawka"
#define AppUrl "https://github.com/metawka/711zip"
; All program files live under {app}\app so the install root stays clean
; (just the launcher shortcut and the uninstaller).
#define AppSub "app"

[Setup]
AppId={{A7F1E2C4-711D-4B11-9E7A-1A2B3C4D5E6F}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}
DefaultDirName={autopf}\711zip
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
UninstallDisplayName={#AppName}
UninstallDisplayIcon={app}\{#AppSub}\{#AppExe}
SetupIconFile=..\src\App\Assets\app.ico
OutputDir=..\dist
OutputBaseFilename=711zip-{#AppVersion}-setup
Compression=lzma2/ultra64
SolidCompression=yes
LZMANumBlockThreads=4
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=modern
MinVersion=10.0.17763
ChangesAssociations=yes

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}\{#AppSub}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
; Clean launcher in the install root, plus Start-menu and (optional) desktop icons.
Name: "{app}\711-zip";              Filename: "{app}\{#AppSub}\{#AppExe}"; WorkingDir: "{app}\{#AppSub}"
Name: "{group}\711-zip";            Filename: "{app}\{#AppSub}\{#AppExe}"; WorkingDir: "{app}\{#AppSub}"
Name: "{autodesktop}\711-zip";      Filename: "{app}\{#AppSub}\{#AppExe}"; WorkingDir: "{app}\{#AppSub}"; Tasks: desktopicon

[Registry]
; ---- "Open with" registration (Screenshot_5 dialog) ----
Root: HKA; Subkey: "Software\Classes\Applications\{#AppExe}"; ValueType: string; ValueName: "FriendlyAppName"; ValueData: "711-zip"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\Applications\{#AppExe}\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#AppSub}\{#AppExe},0"
Root: HKA; Subkey: "Software\Classes\Applications\{#AppExe}\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#AppSub}\{#AppExe}"" ""%1"""
Root: HKA; Subkey: "Software\Classes\Applications\{#AppExe}\SupportedTypes"; ValueType: string; ValueName: ".7z"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\Applications\{#AppExe}\SupportedTypes"; ValueType: string; ValueName: ".zip"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\Applications\{#AppExe}\SupportedTypes"; ValueType: string; ValueName: ".rar"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\Applications\{#AppExe}\SupportedTypes"; ValueType: string; ValueName: ".tar"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\Applications\{#AppExe}\SupportedTypes"; ValueType: string; ValueName: ".gz"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\Applications\{#AppExe}\SupportedTypes"; ValueType: string; ValueName: ".bz2"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\Applications\{#AppExe}\SupportedTypes"; ValueType: string; ValueName: ".xz"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\Applications\{#AppExe}\SupportedTypes"; ValueType: string; ValueName: ".cab"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\Applications\{#AppExe}\SupportedTypes"; ValueType: string; ValueName: ".iso"; ValueData: ""

; ---- Explorer context menu: one cascading "711-zip" submenu per type ----
; .zip
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.zip\shell\711zip"; ValueType: string; ValueName: "MUIVerb"; ValueData: "711-zip"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.zip\shell\711zip"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#AppSub}\{#AppExe},0"
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.zip\shell\711zip\shell\01open"; ValueType: string; ValueName: ""; ValueData: "Открыть в 711-zip"
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.zip\shell\711zip\shell\01open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#AppSub}\{#AppExe}"" ""%1"""
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.zip\shell\711zip\shell\02extracthere"; ValueType: string; ValueName: ""; ValueData: "Извлечь здесь"
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.zip\shell\711zip\shell\02extracthere\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#AppSub}\{#AppExe}"" --extract-here ""%1"""
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.zip\shell\711zip\shell\03extractto"; ValueType: string; ValueName: ""; ValueData: "Извлечь в папку"
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.zip\shell\711zip\shell\03extractto\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#AppSub}\{#AppExe}"" --extract ""%1"""

; .7z
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.7z\shell\711zip"; ValueType: string; ValueName: "MUIVerb"; ValueData: "711-zip"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.7z\shell\711zip"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#AppSub}\{#AppExe},0"
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.7z\shell\711zip\shell\01open"; ValueType: string; ValueName: ""; ValueData: "Открыть в 711-zip"
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.7z\shell\711zip\shell\01open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#AppSub}\{#AppExe}"" ""%1"""
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.7z\shell\711zip\shell\02extracthere"; ValueType: string; ValueName: ""; ValueData: "Извлечь здесь"
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.7z\shell\711zip\shell\02extracthere\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#AppSub}\{#AppExe}"" --extract-here ""%1"""
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.7z\shell\711zip\shell\03extractto"; ValueType: string; ValueName: ""; ValueData: "Извлечь в папку"
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.7z\shell\711zip\shell\03extractto\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#AppSub}\{#AppExe}"" --extract ""%1"""

; .rar
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.rar\shell\711zip"; ValueType: string; ValueName: "MUIVerb"; ValueData: "711-zip"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.rar\shell\711zip"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#AppSub}\{#AppExe},0"
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.rar\shell\711zip\shell\01open"; ValueType: string; ValueName: ""; ValueData: "Открыть в 711-zip"
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.rar\shell\711zip\shell\01open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#AppSub}\{#AppExe}"" ""%1"""
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.rar\shell\711zip\shell\02extracthere"; ValueType: string; ValueName: ""; ValueData: "Извлечь здесь"
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.rar\shell\711zip\shell\02extracthere\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#AppSub}\{#AppExe}"" --extract-here ""%1"""
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.rar\shell\711zip\shell\03extractto"; ValueType: string; ValueName: ""; ValueData: "Извлечь в папку"
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.rar\shell\711zip\shell\03extractto\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#AppSub}\{#AppExe}"" --extract ""%1"""

; Folders: cascading submenu (open + add to archive)
Root: HKA; Subkey: "Software\Classes\Directory\shell\711zip"; ValueType: string; ValueName: "MUIVerb"; ValueData: "711-zip"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\Directory\shell\711zip"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#AppSub}\{#AppExe},0"
Root: HKA; Subkey: "Software\Classes\Directory\shell\711zip\shell\01open"; ValueType: string; ValueName: ""; ValueData: "Открыть в 711-zip"
Root: HKA; Subkey: "Software\Classes\Directory\shell\711zip\shell\01open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#AppSub}\{#AppExe}"" ""%1"""
Root: HKA; Subkey: "Software\Classes\Directory\shell\711zip\shell\02compress"; ValueType: string; ValueName: ""; ValueData: "Добавить в архив"
Root: HKA; Subkey: "Software\Classes\Directory\shell\711zip\shell\02compress\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#AppSub}\{#AppExe}"" --compress ""%1"""

; Any file: single "Добавить в архив" entry
Root: HKA; Subkey: "Software\Classes\*\shell\711zip.compress"; ValueType: string; ValueName: ""; ValueData: "Добавить в архив (711-zip)"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\*\shell\711zip.compress"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#AppSub}\{#AppExe},0"
Root: HKA; Subkey: "Software\Classes\*\shell\711zip.compress\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#AppSub}\{#AppExe}"" --compress ""%1"""

[Run]
Filename: "{app}\{#AppSub}\{#AppExe}"; Description: "{cm:LaunchProgram,711-zip}"; Flags: nowait postinstall skipifsilent
