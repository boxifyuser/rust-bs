# Deploy na VPS com Easypanel

Guia para publicar o servidor **BICHO SOLTO** na VPS Hostinger (`168.231.91.245`) usando [Easypanel](https://easypanel.io) conectado ao GitHub.

Repositório: [github.com/boxifyuser/rust-bs](https://github.com/boxifyuser/rust-bs)

## Requisitos da VPS

| Recurso | Mínimo recomendado |
|---------|-------------------|
| RAM | 8 GB (ideal 12 GB+ para 200 slots) |
| CPU | 4 vCPUs |
| Disco | 40 GB SSD |
| SO | Ubuntu 24.04 + Easypanel |

## 1. Liberar portas no firewall

No painel Hostinger **e** no Easypanel, libere:

| Porta | Protocolo | Uso |
|-------|-----------|-----|
| 28015 | TCP + UDP | Jogo |
| 28016 | TCP | RCON |
| 28017 | UDP | Query |
| 28082 | TCP | App |

Painel Hostinger: VPS → Firewall → adicionar regras acima.

## 2. Conectar GitHub no Easypanel

1. Acesse o Easypanel (`http://168.231.91.245:3000` ou seu domínio)
2. **Settings → GitHub** → conecte a conta e autorize o repositório `boxifyuser/rust-bs`
3. Se o repo for privado, use um Personal Access Token com escopo `repo`

## 3. Criar o serviço (Compose)

Recomendado para servidores de jogo com várias portas e volume persistente.

1. **Create Project** → nome: `rust-bs`
2. **Add Service** → tipo **Compose**
3. Source: **GitHub**
   - Repository: `boxifyuser/rust-bs`
   - Branch: `main`
   - **Build Path:** `/` (raiz do repositório — obrigatório)
   - **Docker Compose File:** `docker-compose.yml`
4. **Environment** — aba do serviço Compose, adicione **uma variável por linha** (Key / Value):

| Key | Value (exemplo) |
|-----|-----------------|
| `RCON_PASSWORD` | `sua-senha-forte` |
| `SERVER_HOSTNAME` | `BICHO SOLTO BRASIL \| SOLO` |
| `SERVER_SEED` | `836891193` |
| `WORLD_SIZE` | `1800` |
| `MAX_PLAYERS` | `200` |
| `SERVER_PORT` | `28015` |
| `RCON_PORT` | `28016` |
| `QUERY_PORT` | `28017` |
| `APP_PORT` | `28082` |
| `SERVER_IDENTITY` | `rst` |
| `RUST_CARBON_ENABLED` | `1` |

> **Importante:** o Easypanel **não** lê `.env.local` nem arquivos `.env` do seu PC.
> As variáveis precisam estar na aba **Environment** do serviço no painel.
> `RCON_PASSWORD` é **obrigatória** — sem ela o deploy avisa e o servidor não inicia.

5. **Deploy**

A primeira subida pode levar **15–30 minutos** (download do Rust via SteamCMD + Carbon).

### Erro "Dockerfile: no such file or directory"

1. Confirme **Build Path = `/`** na aba Source do serviço Compose
2. Faça **Deploy** novamente (o compose agora clona o GitHub no build)
3. Se persistir, use serviço **App** em vez de Compose (veja abaixo)

## Alternativa: App com Dockerfile

Se preferir serviço **App** em vez de Compose:

1. **Add Service** → **App**
2. Source: GitHub → `boxifyuser/rust-bs`
3. Build: **Dockerfile** (detectado automaticamente)
4. Em **Ports**, mapeie: `28015`, `28016`, `28017`, `28082` (TCP e UDP no 28015)
5. Adicione volume persistente: `/home/steam/rust/server`
6. Configure as variáveis de ambiente
7. **Deploy**

Documentação Easypanel: [Builders](https://easypanel.io/docs/builders) · [Compose Service](https://easypanel.io/changelog/1-46-0-1)

## 4. Conectar no jogo

No Rust, abra o console (F1) e digite:

```
client.connect 168.231.91.245:28015
```

Ou adicione o IP na lista de servidores.

## 5. Atualizar após mudanças no GitHub

Sempre que você der `git push` no repositório:

1. Abra o serviço no Easypanel
2. Clique em **Deploy** (ou ative auto-deploy no webhook do GitHub)

O build recria a imagem com plugins/configs novos. Os **saves** ficam no volume `rust_data`.

## 6. Logs e manutenção

- **Logs**: aba Logs do serviço no Easypanel
- **Console**: aba Console → `docker exec` no container
- **RCON**: porta `28016` com a senha definida em `RCON_PASSWORD`
- **Wipe**: no Easypanel, remova o volume `rust_data` e faça redeploy (apaga saves)

## Estrutura de deploy

```
Dockerfile              → imagem Linux com SteamCMD + Carbon
docker-compose.yml      → portas, volume e variáveis
docker/entrypoint.sh    → instala Rust, aplica configs e inicia
.env.example            → modelo de variáveis (sem senhas)
```

## Observações

- O repositório versiona **plugins e configs**, não os binários do jogo (baixados na VPS).
- Troque a senha RCON antes do deploy público.
- Servidor de jogo **não usa** proxy HTTP do Easypanel/Traefik — as portas são expostas diretamente.
- Para mapa customizado (`.map`), será necessário hospedar o arquivo e configurar `+server.levelurl` no entrypoint.
