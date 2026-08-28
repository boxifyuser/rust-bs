@echo off
chcp 65001 >nul
title Rust Map Host - porta 8080
echo Abrindo firewall (porta 8080) se necessario...
netsh advfirewall firewall add rule name="Rust Map Host 8080" dir=in action=allow protocol=TCP localport=8080 >nul 2>&1
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0host-map.ps1"
pause
