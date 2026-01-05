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
dotnet publish TimeReference.App -c Release
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
pause

echo.
echo [SUCCES] Le code et le tag v%VERSION% ont ete pousses sur GitHub.
echo.
echo Prochaine etape : Publier la release manuellement sur GitHub.
echo   1. Allez sur la page "Releases" de votre depot.
echo   2. Cliquez sur "Draft a new release".
echo   3. Choisissez le tag "v%VERSION%".
echo   4. Uploadez le fichier : TimeReference.App\Installer\TimeReferenceNMEA_Setup_v%VERSION%.exe
pause
