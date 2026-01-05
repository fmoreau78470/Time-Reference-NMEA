; Script Inno Setup pour Time Reference NMEA

[Setup]
AppName=Time Reference NMEA
AppVersion=1.1.0
AppPublisher=Votre Nom
DefaultDirName={autopf}\Time Reference NMEA
DefaultGroupName=Time Reference NMEA
OutputDir=Installer
OutputBaseFilename=TimeReferenceNMEA_Setup_v1.1.0
Compression=lzma2
SolidCompression=yes
PrivilegesRequired=admin
SetupIconFile=Assets\Icone-Time-Reference.ico

[Files]
; Chemin vers les fichiers publiés (relatif à ce script)
Source: "bin\Release\net8.0-windows\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Time Reference NMEA"; Filename: "{app}\TimeReference.App.exe"
Name: "{autodesktop}\Time Reference NMEA"; Filename: "{app}\TimeReference.App.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Créer une icône sur le Bureau"; GroupDescription: "Icônes supplémentaires:"; Flags: unchecked

[Run]
Filename: "{app}\TimeReference.App.exe"; Description: "Lancer l'application"; Flags: nowait postinstall skipifsilent








