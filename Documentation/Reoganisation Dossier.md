# 1. Créer le répertoire Archives
New-Item -ItemType Directory -Force -Path "Archives"

# 2. Déplacer les fichiers liés à Python vers Archives
# Déplace tous les fichiers .py
Get-ChildItem -Path . -Filter "*.py" | ForEach-Object { git mv $_.Name Archives\ }

# Déplace requirements.txt s'il existe
if (Test-Path "requirements.txt") { git mv "requirements.txt" Archives\ }

# Déplace la configuration de débogage Python (launch.json) vers Archives en la renommant
if (Test-Path ".vscode\launch.json") { 
    git mv ".vscode\launch.json" "Archives\launch_python.json"
}

# 3. Déplacer le contenu de CSharp_Version vers la racine
$sourceDir = "CSharp_Version"
Get-ChildItem -Path $sourceDir | ForEach-Object {
    # On vérifie si le fichier existe déjà à la racine (ex: .gitignore ou README.md) pour éviter les erreurs
    if (Test-Path $_.Name) {
        Write-Warning "ATTENTION : Le fichier '$($_.Name)' existe déjà à la racine. Il n'a pas été déplacé automatiquement. Veuillez fusionner ou écraser manuellement."
    }
    else {
        # Déplacement avec git pour garder l'historique
        git mv "$sourceDir\$($_.Name)" .
    }
}

# 4. Supprimer le dossier CSharp_Version s'il est vide
if ((Get-ChildItem $sourceDir).Count -eq 0) {
    Remove-Item $sourceDir -Force
    Write-Host "Dossier CSharp_Version supprimé avec succès." -ForegroundColor Green
} else {
    Write-Warning "Le dossier $sourceDir n'est pas vide (fichiers cachés ou doublons ?). Vérifiez manuellement."
}

# 5. Valider et Pousser les changements vers GitHub
Write-Host "Préparation du commit..." -ForegroundColor Cyan
git add .
git commit -m "Refactor: Move Python files to Archives and promote CSharp_Version to root"

Write-Host "Envoi vers GitHub..." -ForegroundColor Cyan
git push

Write-Host "Migration terminée !" -ForegroundColor Green
