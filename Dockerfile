FROM debian:bookworm-slim

RUN dpkg --add-architecture i386 \
  && apt-get update \
  && apt-get install -y --no-install-recommends \
    ca-certificates \
    curl \
    tar \
    unzip \
    gosu \
    locales \
    lib32gcc-s1 \
    libsdl2-2.0-0 \
    libfontconfig1 \
    libglib2.0-0 \
    libatomic1 \
  && sed -i '/en_US.UTF-8/s/^# //g' /etc/locale.gen \
  && locale-gen en_US.UTF-8 \
  && rm -rf /var/lib/apt/lists/*

RUN useradd -m -u 1000 steam

WORKDIR /home/steam

RUN mkdir -p /home/steam/steamcmd \
  && curl -fsSL "https://steamcdn-a.akamaihd.net/client/installer/steamcmd_linux.tar.gz" \
    | tar -xzf - -C /home/steam/steamcmd

COPY --chown=steam:steam docker/entrypoint.sh /home/steam/entrypoint.sh
COPY --chown=steam:steam carbon/ /home/steam/overlay/carbon/
COPY --chown=steam:steam server/rst/cfg/ /home/steam/overlay/server/rst/cfg/

RUN chmod +x /home/steam/entrypoint.sh

# Entrypoint roda como root para ajustar permissoes do volume, depois troca para steam

ENV RUST_HOME=/home/steam/rust \
    STEAMCMD=/home/steam/steamcmd/steamcmd.sh \
    SERVER_IDENTITY=rst \
    RUST_CARBON_ENABLED=1 \
    LANG=en_US.UTF-8 \
    LC_ALL=en_US.UTF-8

EXPOSE 28015/tcp 28015/udp 28016/tcp 28017/udp 28082/tcp

VOLUME ["/home/steam/rust"]

ENTRYPOINT ["/home/steam/entrypoint.sh"]
