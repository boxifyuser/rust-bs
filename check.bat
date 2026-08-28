@echo off
echo Starting Server Installation...
SteamCMD\steamcmd\steamcmd.exe +login anonymous +force_install_dir "C:\Users\Natan Soares\Documents\rust" +app_update 258550 validate +quit
echo --Done--
exit
