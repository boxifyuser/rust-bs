# rust-bs

Servidor Rust dedicado **BICHO SOLTO BRASIL** com Carbon.

## Estrutura versionada

- `carbon/plugins/` — plugins customizados
- `carbon/configs/` — configuracoes dos plugins
- `server/rst/cfg/` — configuracao do servidor
- `RST/` — configs de mapa e launcher
- `*.bat` / `*.ps1` — scripts de start local (Windows)
- `Dockerfile` + `docker-compose.yml` — deploy na VPS (Linux)

## Setup local (Windows)

1. Instale o servidor com `install.bat`
2. Configure `RST/Config.cfg` e `server/rst/cfg/server.cfg`
3. Inicie com `start.bat` ou `start-server-with-map.bat`

## Deploy na VPS (Easypanel + GitHub)

Veja o guia completo em **[DEPLOY.md](DEPLOY.md)**.

Resumo:

1. Conecte o repo `boxifyuser/rust-bs` no Easypanel
2. Crie um serviço **Compose** apontando para `docker-compose.yml`
3. Defina `RCON_PASSWORD` e demais variáveis (`.env.example`)
4. Libere as portas `28015–28017` e `28082` no firewall
5. Deploy — conecte com `client.connect SEU_IP:28015`

## Observacao

Senhas (RCON, web panel, API keys) nao devem ser commitadas em repositorios publicos. Use variáveis de ambiente no Easypanel.
