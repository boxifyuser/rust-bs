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
4. **Environment** — nao e necessario. Toda a configuracao esta em `docker-compose.yml`.
5. **Deploy**

A primeira subida pode levar **15–30 minutos** (download do Rust via SteamCMD + Carbon).

### Aviso "ports might cause conflicts"

O Easypanel **nao quer** `ports` no `docker-compose.yml`. As portas do Rust devem ser mapeadas no **painel**, nao no arquivo.

No serviço **rust-bs**, procure a aba **Ports** (ou equivalente) e adicione:

| Publicada (host) | Alvo (container) | Protocolo |
|------------------|------------------|-----------|
| 28015 | 28015 | TCP |
| 28015 | 28015 | UDP |
| 28016 | 28016 | TCP |
| 28017 | 28017 | UDP |
| 28082 | 28082 | TCP |

Se o serviço Compose **nao tiver** aba Ports, crie um serviço **App** em vez de Compose (veja secao abaixo) — ele tem suporte nativo a portas TCP/UDP.

### Erro "Dockerfile: no such file or directory"

1. Confirme **Build Path = `/`** na aba Source do serviço Compose
2. Faça **Deploy** novamente (o compose agora clona o GitHub no build)
3. Se persistir, use serviço **App** em vez de Compose (veja abaixo)

## Alternativa: App com Dockerfile (recomendado para Rust)

Se o Compose der problemas com portas ou build, use serviço **App**:

1. **Add Service** → **App**
2. Source: GitHub → `boxifyuser/rust-bs`, branch `main`, Build Path `/`
3. Build: **Dockerfile**
4. Aba **Ports** — adicione os mapeamentos da tabela acima (TCP + UDP no 28015)
5. Volume persistente: `/home/steam/rust/server`
6. Environment: mesmas variaveis da secao `environment` em `docker-compose.yml`
7. **Deploy**

Documentação Easypanel: [Builders](https://easypanel.io/docs/builders) · [Compose Service](https://easypanel.io/changelog/1-46-0-1)

## 3.1 Aparecer na lista do Rust (server browser)

Para o jogo **encontrar o servidor sozinho** (aba Comunidade / Modded), configure:

### Token Steam (GSLT)

O token ja esta em `docker-compose.yml` (`STEAM_GSLT`). Para trocar, edite o arquivo e faca deploy.

1. Gere em https://steamcommunity.com/dev/managegameservers (App ID **252490**)
2. Atualize `STEAM_GSLT` em `docker-compose.yml`
3. Commit + Deploy

### IP publico

Definido em `docker-compose.yml` como `SERVER_PUBLIC_IP`.

### Onde procurar no jogo

Como o servidor usa **Carbon** (plugins), ele aparece na aba **Modded**, nao em Vanilla:

1. Menu → **Play Game** → **Rust**
2. Aba **Modded** (ou **Community** com filtro Modded)
3. Busque: **BICHO SOLTO**
4. Pode levar **15–60 min** apos o deploy para indexar na Steam

### Porta de query

Confirme **UDP 28017** aberta no firewall Hostinger — sem ela o servidor nao responde ao browser do jogo.

## 4. Conectar no jogo

No Rust, abra o console (F1):

```
client.connect 168.231.91.245:28015
```

Ou busque **BICHO SOLTO** na aba **Modded** do server browser (apos configurar GSLT).

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
