# Script pour définir la version du projet Time Reference NMEA
# Utilisation : 
#   .\Set-Version.ps1 -Version 1.2.0  (Force une version)
#   .\Set-Version.ps1                 (Incrémente le Patch automatiquement)

param(
    [Parameter(Mandatory=$false)]
    [string]$Version
)

# --- Chemins des fichiers ---
$csprojPath = ".\TimeReference.App\TimeReference.App.csproj"
$issPath = ".\TimeReference.App\setup.iss"

# --- Logique d'auto-incrémentation ---
if ([string]::IsNullOrWhiteSpace($Version)) {
    [xml]$xml = Get-Content $csprojPath
    # Sélection du PropertyGroup contenant la version
    $currentVersion = ($xml.Project.PropertyGroup | Where-Object { $_.Version } | Select-Object -First 1).Version
    if ($currentVersion -match '^(\d+)\.(\d+)\.(\d+)$') {
        $newPatch = [int]$matches[3] + 1
        $Version = "$($matches[1]).$($matches[2]).$newPatch"
        Write-Host "Auto-incrémentation : $currentVersion -> $Version" -ForegroundColor Yellow
    } else {
        Write-Error "Impossible de lire la version actuelle pour l'incrémenter."
        return
    }
}

if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    Write-Error "Le format de la version doit être X.Y.Z (ex: 1.2.0). Fourni : $Version"
    return
}

Write-Host "Mise à jour du projet vers la version $Version..." -ForegroundColor Cyan

# --- 1. Mise à jour du fichier .csproj ---
Write-Host "Modification de $csprojPath..."
[xml]$xml = Get-Content $csprojPath

# Sélection du bon PropertyGroup (celui avec Version ou le premier)
$pg = $xml.Project.PropertyGroup | Where-Object { $_.Version } | Select-Object -First 1
if (-not $pg) { $pg = $xml.Project.PropertyGroup | Select-Object -First 1 }

# Helper pour mise à jour sécurisée
function Update-XmlNode($parent, $name, $val) {
    $node = $parent.SelectSingleNode($name); if ($node) { $node.InnerText = $val } else { $e=$parent.OwnerDocument.CreateElement($name); $e.InnerText=$val; $parent.AppendChild($e) | Out-Null }
}

Update-XmlNode $pg "Version" $Version
Update-XmlNode $pg "FileVersion" "$Version.0"
Update-XmlNode $pg "AssemblyVersion" "$Version.0"

$xml.Save($csprojPath)
Write-Host ".csproj mis à jour." -ForegroundColor Green

# --- 2. Mise à jour du script Inno Setup ---
Write-Host "Modification de $issPath..."
$issContent = (Get-Content $issPath -Raw) -replace '(?m)^(AppVersion=).*', "AppVersion=$Version"
$issContent = $issContent -replace '(?m)^(OutputBaseFilename=).*', "OutputBaseFilename=TimeReferenceNMEA_Setup_v$Version"
Set-Content -Path $issPath -Value $issContent
Write-Host "setup.iss mis à jour." -ForegroundColor Green

Write-Host "Versionning terminé pour v$Version." -ForegroundColor Cyan