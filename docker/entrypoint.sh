#!/bin/bash
set -euo pipefail

RUST_HOME="${RUST_HOME:-/home/steam/rust}"
STEAMCMD="${STEAMCMD:-/home/steam/steamcmd/steamcmd.sh}"
OVERLAY="/home/steam/overlay"
IDENTITY="${SERVER_IDENTITY:-rst}"
SERVER_DIR="${RUST_HOME}/server/${IDENTITY}"
STEAM_PLUGINS="${RUST_HOME}/RustDedicated_Data/Plugins/x86_64"

# Volume Docker costuma ser criado como root — corrige antes de continuar
if [ "$(id -u)" = "0" ]; then
  mkdir -p "${RUST_HOME}/server" "${SERVER_DIR}/cfg"
  chown -R steam:steam /home/steam
  exec gosu steam "$0" "$@"
fi

mkdir -p "${RUST_HOME}" "${SERVER_DIR}/cfg"

if [ -f "${RUST_HOME}/RustDedicated" ]; then
  echo "[rust-bs] Verificando atualizacoes do Rust..."
  "${STEAMCMD}" +force_install_dir "${RUST_HOME}" +login anonymous +app_update 258550 +quit
else
  echo "[rust-bs] Instalacao inicial do Rust Dedicated Server (pode demorar)..."
  "${STEAMCMD}" +force_install_dir "${RUST_HOME}" +login anonymous +app_update 258550 validate +quit
fi

if [ ! -f "${RUST_HOME}/RustDedicated" ]; then
  echo "[rust-bs] ERRO: RustDedicated nao encontrado apos instalacao."
  exit 1
fi

if [ "${RUST_CARBON_ENABLED:-1}" = "1" ]; then
  echo "[rust-bs] Instalando/atualizando Carbon..."
  curl -fsSL "https://github.com/CarbonCommunity/Carbon.Core/releases/download/production_build/Carbon.Linux.Release.tar.gz" \
    | tar -xz -C "${RUST_HOME}"
fi

echo "[rust-bs] Aplicando plugins e configs do repositorio..."
if [ -d "${OVERLAY}/carbon" ]; then
  mkdir -p "${RUST_HOME}/carbon"
  cp -rf "${OVERLAY}/carbon/plugins" "${RUST_HOME}/carbon/" 2>/dev/null || true
  cp -rf "${OVERLAY}/carbon/configs" "${RUST_HOME}/carbon/" 2>/dev/null || true
  cp -rf "${OVERLAY}/carbon/config" "${RUST_HOME}/carbon/" 2>/dev/null || true
  cp -rf "${OVERLAY}/carbon/lang" "${RUST_HOME}/carbon/" 2>/dev/null || true
  cp -rf "${OVERLAY}/carbon/modules" "${RUST_HOME}/carbon/" 2>/dev/null || true
  [ -f "${OVERLAY}/carbon/config.json" ] && cp "${OVERLAY}/carbon/config.json" "${RUST_HOME}/carbon/"
  [ -f "${OVERLAY}/carbon/config.auto.json" ] && cp "${OVERLAY}/carbon/config.auto.json" "${RUST_HOME}/carbon/"
fi

if [ -d "${OVERLAY}/server/rst/cfg" ]; then
  cp -rf "${OVERLAY}/server/rst/cfg/"* "${SERVER_DIR}/cfg/"
fi

# Steam libs no Linux (evita hang apos SteamAPI_Init)
if [ -f "${STEAM_PLUGINS}/steamclient.so" ]; then
  cp -fn "${STEAM_PLUGINS}/steamclient.so" "${RUST_HOME}/" 2>/dev/null || true
fi

export DOORSTOP_ENABLED=1
export DOORSTOP_TARGET_ASSEMBLY="${RUST_HOME}/carbon/managed/Carbon.Preloader.dll"
export LD_LIBRARY_PATH="${STEAM_PLUGINS}:${RUST_HOME}:${LD_LIBRARY_PATH:-}"
export LD_PRELOAD="${RUST_HOME}/libdoorstop.so"
export MALLOC_ARENA_MAX=2

RCON_PASSWORD="${RCON_PASSWORD:?Defina RCON_PASSWORD}"
SERVER_HOSTNAME="${SERVER_HOSTNAME:-BICHO SOLTO BRASIL}"
SERVER_SEED="${SERVER_SEED:-836891193}"
WORLD_SIZE="${WORLD_SIZE:-1800}"
MAX_PLAYERS="${MAX_PLAYERS:-200}"
SERVER_PORT="${SERVER_PORT:-28015}"
RCON_PORT="${RCON_PORT:-28016}"
QUERY_PORT="${QUERY_PORT:-28017}"
APP_PORT="${APP_PORT:-28082}"

cd "${RUST_HOME}"

heartbeat() {
  while true; do
    sleep 120
    if pgrep -x RustDedicated >/dev/null 2>&1; then
      CPU="$(ps -o %cpu= -p "$(pgrep -x RustDedicated | head -1)" 2>/dev/null | tr -d ' ')"
      MEM="$(ps -o %mem= -p "$(pgrep -x RustDedicated | head -1)" 2>/dev/null | tr -d ' ')"
      echo "[rust-bs] $(date -u +%H:%M:%S) RustDedicated ativo (CPU ${CPU:-?}% MEM ${MEM:-?}%) — gerando mapa, aguarde..."
      if [ -f "${SERVER_DIR}/server.log" ]; then
        tail -n 2 "${SERVER_DIR}/server.log" 2>/dev/null | sed 's/^/[rust-bs]   /' || true
      fi
    else
      echo "[rust-bs] ERRO: RustDedicated parou inesperadamente."
      return 1
    fi
  done
}

echo "[rust-bs] Iniciando servidor..."
heartbeat &
HEARTBEAT_PID=$!

set +e
./RustDedicated -batchmode -nographics -load \
  -logfile "${SERVER_DIR}/server.log" \
  +server.ip 0.0.0.0 \
  +server.port "${SERVER_PORT}" \
  +server.queryport "${QUERY_PORT}" \
  +rcon.ip 0.0.0.0 \
  +rcon.port "${RCON_PORT}" \
  +rcon.password "${RCON_PASSWORD}" \
  +rcon.web 1 \
  +app.port "${APP_PORT}" \
  +server.identity "${IDENTITY}" \
  +server.secure 0 \
  +server.gamemode Vanilla \
  +server.level "Procedural Map" \
  +server.seed "${SERVER_SEED}" \
  +server.worldsize "${WORLD_SIZE}" \
  +server.maxplayers "${MAX_PLAYERS}" \
  +server.hostname "${SERVER_HOSTNAME}" \
  +bradley.enabled 0 \
  +events.set_event_enabled bradley_road false &
RUST_PID=$!
set -e

# Repassa logs do arquivo para o stdout do container (Easypanel)
tail -n 0 -F "${SERVER_DIR}/server.log" &
TAIL_PID=$!

wait "${RUST_PID}"
EXIT_CODE=$?

kill "${HEARTBEAT_PID}" "${TAIL_PID}" 2>/dev/null || true
echo "[rust-bs] RustDedicated encerrou com codigo ${EXIT_CODE}"
exit "${EXIT_CODE}"
