@echo off
setlocal

set VERSION=%~1
set ISCC="C:\Program Files (x86)\Inno Setup 6\iscc.exe"

if not exist %ISCC% (
    echo [ERREUR] Inno Setup Compiler non trouve a l'emplacement par defaut.
    echo Verifiez que Inno Setup 6 est installe dans "C:\Program Files (x86)\Inno Setup 6"
    pause
    exit /b 1
)

:MENU
cls
echo ==========================================
echo    MENU DE DEPLOIEMENT - Time Reference NMEA
if not "%VERSION%"=="" echo    Version cible : %VERSION%
echo ==========================================
echo 1 : Incrementer la version
echo 2 : Generer l'executable
echo 3 : Compiler l'installateur
echo 4 : Generer la documentation locale
echo 5 : Validation Git (Commit, Tag)
echo 6 : Push vers GitHub
echo 7 : Deploiement de la documentation sur GitHub Pages
echo 8 : Deploiement complet
echo 0 : Sortir sans rien faire
echo ==========================================
set /p CHOICE="Votre choix : "

if "%CHOICE%"=="1" goto DO_VERSION
if "%CHOICE%"=="2" goto DO_EXE
if "%CHOICE%"=="3" goto DO_INSTALLER
if "%CHOICE%"=="4" goto DO_DOC_LOCAL
if "%CHOICE%"=="5" goto DO_GIT
if "%CHOICE%"=="6" goto DO_PUSH
if "%CHOICE%"=="7" goto DO_DOC_WEB
if "%CHOICE%"=="8" goto DO_FULL
if "%CHOICE%"=="0" goto END
goto MENU

:DO_VERSION
call :CHECK_VERSION
if errorlevel 1 goto MENU
call :STEP_VERSION
pause
goto MENU

:DO_EXE
call :STEP_EXE
pause
goto MENU

:DO_INSTALLER
call :STEP_INSTALLER
pause
goto MENU

:DO_DOC_LOCAL
call :STEP_DOC_LOCAL
pause
goto MENU

:DO_GIT
call :CHECK_VERSION
if errorlevel 1 goto MENU
call :STEP_GIT
pause
goto MENU

:DO_PUSH
call :STEP_PUSH
pause
goto MENU

:DO_DOC_WEB
call :STEP_DOC_WEB
pause
goto MENU

:DO_FULL
call :CHECK_VERSION
if errorlevel 1 goto MENU

echo.
echo ==========================================
echo DEMARRAGE DU DEPLOIEMENT COMPLET v%VERSION%
echo ==========================================

call :STEP_VERSION
if errorlevel 1 goto FULL_FAIL
echo.
echo Appuyez sur une touche pour passer a l'etape suivante...
pause

call :STEP_DOC_LOCAL
if errorlevel 1 goto FULL_FAIL
echo.
echo Appuyez sur une touche pour passer a l'etape suivante...
pause

call :STEP_EXE
if errorlevel 1 goto FULL_FAIL
echo.
echo Appuyez sur une touche pour passer a l'etape suivante...
pause

call :STEP_INSTALLER
if errorlevel 1 goto FULL_FAIL
echo.
echo Appuyez sur une touche pour passer a l'etape suivante...
pause

call :STEP_GIT
if errorlevel 1 goto FULL_FAIL
echo.
echo Appuyez sur une touche pour passer a l'etape suivante...
pause

call :STEP_PUSH
if errorlevel 1 goto FULL_FAIL
echo.
echo Appuyez sur une touche pour passer a l'etape suivante...
pause

call :STEP_DOC_WEB
if errorlevel 1 goto FULL_FAIL

echo.
echo [SUCCES] Deploiement complet termine pour v%VERSION%.
echo.
echo Prochaine etape : Publier la release manuellement sur GitHub.
echo   1. Allez sur la page "Releases" de votre depot.
echo   2. Reperez la version v%VERSION% (probablement en "Draft" creee par l'Action).
echo   3. Cliquez sur le bouton "Edit" (crayon).
echo   4. Verifiez les fichiers, ajoutez l'installateur si besoin, et cliquez sur "Publish release".
pause
goto MENU

:FULL_FAIL
echo.
echo [ECHEC] Le deploiement complet a rencontre une erreur.
pause
goto MENU

:CHECK_VERSION
if "%VERSION%"=="" (
    set /p VERSION="Veuillez entrer la version (ex: 1.2.3) : "
)
if "%VERSION%"=="" (
    echo [ERREUR] Version requise.
    exit /b 1
)
exit /b 0

:STEP_VERSION
echo.
echo ==========================================
echo Etape 1 : Incrementer la version a %VERSION%
echo ==========================================
powershell -ExecutionPolicy Bypass -File ".\Set-Version.ps1" -Version %VERSION%
exit /b %ERRORLEVEL%

:STEP_DOC_LOCAL
echo.
echo ==========================================
echo Etape 2 : Generer la documentation locale
echo ==========================================
mkdocs build
exit /b %ERRORLEVEL%

:STEP_EXE
echo.
echo ==========================================
echo Etape 3 : Generer l'executable (dotnet publish)
echo ==========================================
dotnet publish TimeReference.App -c Release -r win-x64 --self-contained true
exit /b %ERRORLEVEL%

:STEP_INSTALLER
echo.
echo ==========================================
echo Etape 4 : Compiler l'installateur (Inno Setup)
echo ==========================================
%ISCC% "TimeReference.App\setup.iss"
exit /b %ERRORLEVEL%

:STEP_GIT
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
exit /b %ERRORLEVEL%

:STEP_PUSH
echo.
echo =========================================================
echo Etape 6 : Push vers GitHub
echo =========================================================
git push origin main --tags --force
exit /b %ERRORLEVEL%

:STEP_DOC_WEB
echo.
echo =========================================================
echo Etape 7 : Deploiement de la documentation sur GitHub Pages
echo =========================================================
mkdocs gh-deploy --force
exit /b %ERRORLEVEL%

:END
exit /b 0
