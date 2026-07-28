@echo off
setlocal
if "%~1"=="" (
  echo Arrastra un archivo JSON de inventario sobre este CMD.
  echo.
  pause
  exit /b 1
)
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Importar-Inventario-Seguro.ps1" "%~1"
if errorlevel 1 pause
endlocal
