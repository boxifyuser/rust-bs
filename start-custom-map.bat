@echo off
chcp 65001
cls
echo Starting Server com MAPA_FINAL...

set "MAPFILE=C:\RustMaps\MAPA_FINAL.map"
if not exist "%MAPFILE%" (
  echo.
  echo ERRO: mapa nao encontrado: %MAPFILE%
  echo.
  echo No RustEdit use File - Save As e salve em:
  echo   C:\RustMaps\MAPA_FINAL.map
  echo.
  pause
  exit /b 1
)

RustDedicated.exe -batchmode -nographics -silent-crashes ^
+server.ip 0.0.0.0 ^
+server.port 28015 ^
+server.queryport 28017 ^
+rcon.ip 0.0.0.0 ^
+rcon.port 28016 ^
+rcon.password "19541954" ^
+app.port 28082 ^
+server.identity "rst" ^
+server.gamemode Vanilla ^
+server.levelurl "http://45.168.168.88:8080/MAPA_FINAL.map" ^
+spawn.respawn_groups true ^
+spawn.respawn_populations true ^
+spawn.respawn_individuals true ^
-LogFile "server\rst\server.log"
