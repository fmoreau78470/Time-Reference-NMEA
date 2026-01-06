@echo off
setlocal

if "%~1"=="" (
    echo Usage: %0 ^<Version^>
    echo Exemple: %0 1.2.3
    exit /b 1
)

set VERSION=%~1
set ISCC="C:\Program Files (x86)\Inno Setup 6\iscc.exe"

if not exist %ISCC% (
    echo [ERREUR] Inno Setup Compiler non trouve a l'emplacement par defaut.
    echo Verifiez que Inno Setup 6 est installe dans "C:\Program Files (x86)\Inno Setup 6"
    pause
    exit /b 1
)

echo.
echo ==========================================
echo Verification de l'etat du depot
echo ==========================================
REM git diff-index --quiet HEAD --
REM if %ERRORLEVEL% NEQ 0 (
REM     echo [ATTENTION] Des modifications locales ont ete detectees.
REM     echo Cela risque de creer des conflits lors de la synchronisation (Etape 0).
REM     echo Il est recommande d'avoir un depot propre ^(git stash ou git reset^) avant de deployer.
REM     echo.
REM     pause
REM     exit /b 1
REM )

echo.
echo ==========================================
echo Etape 0 : Synchronisation avec GitHub
echo ==========================================
REM git pull --rebase origin main
REM if %ERRORLEVEL% NEQ 0 (
REM     echo [ERREUR] Echec de la synchronisation avec GitHub. Resoudre les conflits manuellement puis relancer.
REM     pause
REM     exit /b %ERRORLEVEL%
REM )

echo.
echo ==========================================
echo Etape 1 : Incrementer la version a %VERSION%
echo ==========================================
powershell -ExecutionPolicy Bypass -File ".\Set-Version.ps1" -Version %VERSION%
if %ERRORLEVEL% NEQ 0 (
    echo [ERREUR] Echec du script de versionning.
    pause
    exit /b %ERRORLEVEL%
)
echo.
echo Appuyez sur une touche pour passer a l'etape 2 : Generer la documentation locale.
pause

echo.
echo ==========================================
echo Etape 2 : Generer la documentation locale
echo ==========================================
mkdocs build
if %ERRORLEVEL% NEQ 0 (
    echo [ERREUR] Echec de mkdocs build.
    pause
    exit /b %ERRORLEVEL%
)
echo.
echo Appuyez sur une touche pour passer a l'etape 3 : Generer l'executable.
pause

echo.
echo ==========================================
echo Etape 3 : Generer l'executable (dotnet publish)
echo ==========================================
dotnet publish TimeReference.App -c Release -r win-x64 --self-contained false
if %ERRORLEVEL% NEQ 0 (
    echo [ERREUR] Echec de la compilation dotnet.
    pause
    exit /b %ERRORLEVEL%
)
echo.
echo Appuyez sur une touche pour passer a l'etape 4 : Compiler l'installateur.
pause

echo.
echo ==========================================
echo Etape 4 : Compiler l'installateur (Inno Setup)
echo ==========================================
%ISCC% "TimeReference.App\setup.iss"
if %ERRORLEVEL% NEQ 0 (
    echo [ERREUR] Echec de la compilation Inno Setup.
    pause
    exit /b %ERRORLEVEL%
)
echo.
echo Appuyez sur une touche pour passer a l'etape 5 : Validation Git (Commit et Tag).
pause

echo.
echo =================================================================
echo Etape 5 : Validation Git (Commit, Tag)
echo =================================================================
git add .
git commit -m "Release v%VERSION%" || echo "Pas de nouveaux changements a commiter."

echo.
echo Nettoyage et (re)creation du tag v%VERSION%...
git tag -d v%VERSION% >nul 2>&1
git push origin --delete v%VERSION% >nul 2>&1
git tag v%VERSION%
if %ERRORLEVEL% NEQ 0 (
    echo [ERREUR] Echec de la creation du tag v%VERSION%.
    pause
    exit /b %ERRORLEVEL%
)
echo Tag v%VERSION% cree avec succes.
echo.
echo Appuyez sur une touche pour passer a l'etape 6 : Push vers GitHub.
pause

echo.
echo =========================================================
echo Etape 6 : Push vers GitHub
echo =========================================================
git push origin main --tags
if %ERRORLEVEL% NEQ 0 (
    echo [ERREUR] Echec du push vers GitHub.
    pause
    exit /b %ERRORLEVEL%
)
echo.
echo Le script est termine. Appuyez sur une touche pour voir les instructions finales.
echo Appuyez sur une touche pour passer a l'etape 7 : Deploiement Doc Web.
pause

echo.
echo =========================================================
echo Etape 7 : Deploiement de la documentation sur GitHub Pages
echo =========================================================
mkdocs gh-deploy --force
if %ERRORLEVEL% NEQ 0 (
    echo [ERREUR] Echec du deploiement de la documentation.
    pause
    exit /b %ERRORLEVEL%
)
echo.
echo Le script est termine. Appuyez sur une touche pour voir les instructions finales.
pause
echo.
echo [SUCCES] Le code et le tag v%VERSION% ont ete pousses sur GitHub.
echo.
echo Prochaine etape : Publier la release manuellement sur GitHub.
echo   1. Allez sur la page "Releases" de votre depot.
echo   2. Reperez la version v%VERSION% (probablement en "Draft" creee par l'Action).
echo   3. Cliquez sur le bouton "Edit" (crayon).
echo   4. Verifiez les fichiers, ajoutez l'installateur si besoin, et cliquez sur "Publish release".
pause
