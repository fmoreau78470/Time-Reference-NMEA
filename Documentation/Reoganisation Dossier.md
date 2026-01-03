# Script de Sécurisation et Nettoyage du Dépôt Public
# Ce script archive les dossiers sensibles (Documentation interne, Archives code) 
# dans un fichier protégé par mot de passe, puis les retire du dépôt public GitHub.

# --- CONFIGURATION ---
$password = "fuo6nuyg"  # <--- CHANGEZ CECI !
$archiveName = "Documentation_et_Archives.7z"
$foldersToHide = @("Documentation", "Archives")
$7zPath = "$env:ProgramFiles\7-Zip\7z.exe"

# --- 1. CRÉATION DE L'ARCHIVE PROTÉGÉE ---
if (Test-Path $7zPath) {
    Write-Host "Création de l'archive protégée avec 7-Zip..." -ForegroundColor Cyan
    # a=add, -p=password, -mhe=encrypt header (cache les noms de fichiers)
    & $7zPath a $archiveName $foldersToHide -p$password -mhe
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Archive $archiveName créée avec succès." -ForegroundColor Green
    } else {
        Write-Error "Erreur lors de la création de l'archive."
        return
    }
} else {
    Write-Warning "7-Zip n'a pas été trouvé ($7zPath)."
    Write-Warning "Veuillez créer MANUELLEMENT une archive protégée par mot de passe nommée '$archiveName'"
    Write-Warning "contenant les dossiers : $foldersToHide"
    Read-Host "Appuyez sur Entrée une fois l'archive créée pour continuer..."
}

# --- 2. MISE À JOUR DE GITIGNORE ---
Write-Host "Mise à jour de .gitignore..." -ForegroundColor Cyan
foreach ($folder in $foldersToHide) {
    Add-Content .gitignore "`n$folder/"
}
Add-Content .gitignore "`n$archiveName" # On ignore aussi l'archive secrète

# --- 3. NETTOYAGE DU DÉPÔT (GIT RM CACHED) ---
Write-Host "Retrait des dossiers du suivi Git (conservation locale)..." -ForegroundColor Cyan
foreach ($folder in $foldersToHide) {
    git rm -r --cached $folder
}

# --- 4. COMMIT ET PUSH ---
Write-Host "Envoi des modifications sur GitHub..." -ForegroundColor Cyan
git add .gitignore
git commit -m "Securisation: Archivage local des documents internes et retrait du dépôt public"
git push

Write-Host "Terminé ! Les dossiers sont retirés de GitHub mais sont dans $archiveName." -ForegroundColor Green
