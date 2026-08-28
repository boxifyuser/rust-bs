@echo off
chcp 65001 >nul
title BICHO SOLTO - Map Host + Server
cd /d "%~dp0"

echo [1/2] Subindo host do mapa (porta 8080)...
netsh advfirewall firewall add rule name="Rust Map Host 8080" dir=in action=allow protocol=TCP localport=8080 >nul 2>&1

powershell -NoProfile -Command "try { $r=Invoke-WebRequest -Uri 'http://127.0.0.1:8080/MAPA_FINAL.map' -Method Head -TimeoutSec 2 -UseBasicParsing; exit 0 } catch { exit 1 }"
if errorlevel 1 (
  start "Rust Map Host" powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0host-map.ps1"
  timeout /t 2 /nobreak >nul
) else (
  echo Map host ja estava ativo.
)

echo [2/2] Iniciando RustDedicated...
echo URL: http://45.168.168.88:8080/MAPA_FINAL.map
echo.
call "%~dp0start.bat"
