@echo off
setlocal

if "%~1"=="" (
    echo Usage: %0 ^<Version^>
    echo Exemple: %0 1.2.3
    exit /b 1
)

set VERSION=%~1

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
pause

echo.
echo ==========================================
echo Etape 4 : Git Commit et Tag v%VERSION%
echo ==========================================
git add .
git commit -m "Release v%VERSION%"
git tag v%VERSION%
if %ERRORLEVEL% NEQ 0 (
    echo [ERREUR] Echec des operations Git locales.
    pause
    exit /b %ERRORLEVEL%
)
pause

echo.
echo ==========================================
echo Etape 5 : Deployer (Push vers GitHub)
echo ==========================================
git push origin main --tags

echo.
echo [SUCCES] Deploiement termine !
echo.
echo Le workflow GitHub Actions a ete declenche.
echo La release sera disponible dans environ 2 a 5 minutes sur GitHub.
pause
