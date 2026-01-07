; Script Inno Setup pour Time Reference NMEA

[Setup]
AppName=Time Reference NMEA
AppVersion=1.2.1
AppPublisher=Votre Nom
DefaultDirName={autopf}\Time Reference NMEA
DefaultGroupName=Time Reference NMEA
OutputDir=Installer
OutputBaseFilename=TimeReferenceNMEA_Setup_v1.2.1
Compression=lzma2
SolidCompression=yes
PrivilegesRequired=admin
SetupIconFile=Assets\Icone-Time-Reference.ico
LanguageDetectionMethod=none

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "fr"; MessagesFile: "compiler:Languages\French.isl"

[Files]
; Chemin vers les fichiers publiés (relatif à ce script)
Source: "bin\Release\net8.0-windows\win-x64\publish\*"; DestDir: "{app}"; Excludes: "config.json"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "bin\Release\net8.0-windows\win-x64\publish\config.json"; DestDir: "{app}"; Flags: onlyifdoesntexist uninsneveruninstall
Source: "..\site\*"; DestDir: "{app}\site"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Time Reference NMEA"; Filename: "{app}\TimeReference.App.exe"
Name: "{autodesktop}\Time Reference NMEA"; Filename: "{app}\TimeReference.App.exe"; Tasks: desktopicon
Name: "{group}\Documentation (FR)"; Filename: "{app}\site\index.html"
Name: "{group}\Documentation (EN)"; Filename: "{app}\site\en\index.html"

[Registry]
Root: HKCU; Subkey: "Software\Time Reference NMEA"; ValueType: string; ValueName: "InstallLanguage"; ValueData: "{language}"; Flags: uninsdeletevalue

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Run]
Filename: "{app}\TimeReference.App.exe"; Description: "{cm:LaunchApp}"; Flags: nowait postinstall skipifsilent runascurrentuser

[CustomMessages]
fr.CreateDesktopIcon=Créer une icône sur le Bureau
en.CreateDesktopIcon=Create a desktop icon

fr.AdditionalIcons=Icônes supplémentaires :
en.AdditionalIcons=Additional icons:

fr.LaunchApp=Lancer l'application
en.LaunchApp=Launch the application





