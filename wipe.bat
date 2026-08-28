@echo off
chcp 65001
echo.
echo === WIPE do mapa (BICHO SOLTO) ===
echo Isto apaga o SAVE do mundo e recria o loot/mapa no proximo boot.
echo Configs, plugins e users.cfg NAO serao apagados.
echo.
set /p CONFIRM=Digite SIM para confirmar: 
if /I not "%CONFIRM%"=="SIM" (
  echo Cancelado.
  pause
  exit /b 1
)

cd /d "%~dp0"
set "ID=server\rst"

echo Parando processos RustDedicated se existirem...
taskkill /IM RustDedicated.exe /F >nul 2>&1
timeout /t 3 /nobreak >nul

echo Removendo saves do mapa...
del /q "%ID%\*.sav" 2>nul
del /q "%ID%\*.sav.*" 2>nul
del /q "%ID%\player.deaths.*.db*" 2>nul
del /q "%ID%\player.states.*.db*" 2>nul
del /q "%ID%\player.blueprints.*.db*" 2>nul
del /q "%ID%\player.identities.*.db*" 2>nul

echo.
echo Wipe de mapa concluido.
echo No proximo start o loot respawna no tempo PADRAO do Rust ao coletar caixas.
echo (MonumentLootBoot so garante spawn.respawn_groups/default - nao forca fill.)
echo.
set /p STARTNOW=Iniciar o servidor agora? (S/N): 
if /I "%STARTNOW%"=="S" call "%~dp0start.bat"
pause
