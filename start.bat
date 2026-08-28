@echo off
chcp 65001
cls
echo Starting Server...
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
+server.level "Procedural Map" ^
+server.seed 836891193 ^
+server.worldsize 1800 ^
+bradley.enabled 0 ^
-LogFile "server\rst\server.log"
