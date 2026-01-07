# Script pour générer un rapport CSV des traductions (Matrice de langues)
# Ce script scanne le dossier lang, récupère toutes les clés et crée un tableau comparatif.

param(
    [string]$LangFolder = ".\TimeReference.App\lang",
    [string]$OutputFile = ".\Translations_Report.csv"
)

Write-Host "Analyse des fichiers de langue dans $LangFolder..." -ForegroundColor Cyan

$jsonFiles = Get-ChildItem -Path $LangFolder -Filter "*.json"
if ($jsonFiles.Count -eq 0) {
    Write-Error "Aucun fichier .json trouvé."
    exit
}

# 1. Collecte des données et des clés uniques
$allKeys = New-Object System.Collections.Generic.SortedSet[string]
$langData = @{}

foreach ($file in $jsonFiles) {
    $langCode = $file.BaseName
    Write-Host "Lecture de $langCode..."
    try {
        $content = Get-Content $file.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
        $langData[$langCode] = $content
        
        # Ajout des clés au set global
        foreach ($prop in $content.PSObject.Properties) {
            [void]$allKeys.Add($prop.Name)
        }
    } catch {
        Write-Warning "Erreur lors de la lecture de $($file.Name): $_"
    }
}

# 2. Construction de la matrice
$report = foreach ($key in $allKeys) {
    $row = [ordered]@{ "KEY" = $key }
    foreach ($file in $jsonFiles) {
        $langCode = $file.BaseName
        $val = $langData[$langCode].$key
        if ($null -eq $val) { 
            $val = "--- MANQUANT ---" 
        } else {
            # Remplacement des sauts de ligne pour lisibilité CSV simple
            $val = $val.ToString().Replace("`n", " [LF] ").Replace("`r", "")
        }
        $row[$langCode] = $val
    }
    [PSCustomObject]$row
}

# 3. Export CSV (Délimiteur point-virgule pour Excel)
$report | Export-Csv -Path $OutputFile -NoTypeInformation -Encoding UTF8 -Delimiter ";"

Write-Host "Rapport généré avec succès : $OutputFile" -ForegroundColor Green
