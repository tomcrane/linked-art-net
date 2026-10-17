# Hosting linkedart.net

One virtual server, three containers: Caddy for TLS and routing, the examples site, the dates service. The
deployment files are in [deploy/](../deploy/), in two compose projects: `deploy/edge/` is the shared front door
(Caddy, one per server, routing every hostname) and `deploy/linkedart/` is this site's two containers. They meet
on a Docker network called `web`, so further apps can be added later as their own projects without touching
these. The domain `linkedart.net` is registered with Mythic Beasts
(2026-10-15), which also hosts its DNS.

Status (2026-10-15): the server exists. Mythic Beasts VPS 2 named `linkedartbox`, London (THS), Ubuntu 24.04:

| | |
|---|---|
| IPv4 | 176.126.242.29 |
| IPv6 | 2a00:1098:67d::1 |
| spec | 1 core, 2 GB, 20 GB SSD, 1 TB/month |

Status (2026-10-16): **live.** First deployment of v0.1.0 (commit da2fdc8) completed on 2026-10-16: Caddy holds
certificates for all three hosts, `https://linkedart.net` and `https://dates.linkedart.net` answer, the www host
redirects to the apex, and the service reports its version at `/version`. Sections 0 to 6 below are the record of
how it was done; section 7 onwards is what matters from here.

## 0. Before the first deployment

1. Commit and push everything; the images are built from the tagged commit, so the tag must point at the code
   you mean to run. Run `dotnet test linked-art-net.slnx` first.
2. Tag and push the tag (section 4). Watch the "Container images" workflow in the repository's Actions tab;
   it builds both images in a few minutes.
3. Make the two packages public (GitHub profile > Packages > package > Package settings > Change visibility),
   or plan to `docker login ghcr.io` on the server. The code is MIT, so public is the simple choice.
4. Create the OAuth app and the token (section 5) and have the values ready for `.env`. They go straight into
   the server's `.env`, nowhere else.

## 1. Hosts

| Host | Serves | Notes |
|---|---|---|
| `linkedart.net` | the examples site, playground, corpus browser, trainer | cookies and GitHub login live here |
| `www.linkedart.net` | redirect to the apex | |
| `dates.linkedart.net` | the dates service API and its `/docs` | CORS allows any origin, so it gets its own host |

## 2. The server

Mythic Beasts virtual server, London, Ubuntu 24.04 LTS, with the IPv4 add-on (GitHub's OAuth callbacks and most
visitors need it; IPv6 comes as standard). From the configurator (ex VAT, 2026-10-15):

| Preset | RAM | Cores | Bandwidth | Per month |
|---|---|---|---|---|
| VPS 2 | 2 GB | 1 | 1 TB | £7.00 |
| VPS 4 | 4 GB | 2 | 2 TB | £13.50 |

plus SSD at 8p per GB per month, IPv4 at £2.00, and optional backup space at 80p per GB. VPS 2 with 20 GB of
SSD and IPv4 is £10.60 a month, or ten months' price a year. It is enough: the two .NET containers idle at
around 150 to 200 MB each with the examples and corpus loaded, Caddy at a few tens of MB, and the rest is for
Docker and the OS. VPS 4 at £17.10 is the step up if the trainer gets busy or a second service arrives;
servers can be upgraded at any time pro rata. Monthly billing to start; yearly once it is settled.

On the server, once:

```
sudo apt update && sudo apt install -y docker.io docker-compose-v2 ufw unattended-upgrades
sudo usermod -aG docker $USER            # log out and in again
sudo ufw allow OpenSSH && sudo ufw allow 80 && sudo ufw allow 443 && sudo ufw allow 443/udp && sudo ufw enable   # udp for HTTP/3
docker network create web                 # the network the front door and every app share
sudo mkdir -p /srv/edge /srv/linkedart/data/trainer /srv/linkedart/data/capture
sudo chown -R 1654:1654 /srv/linkedart/data   # the application user in the chiseled images
```

Copy the two projects up from the repository root on your machine. The `/.` on each source copies the folder's
contents, dot files included, into the existing directory rather than nesting a folder inside it:

```
scp -r deploy/edge/. root@linkedart.net:/srv/edge/
scp -r deploy/linkedart/. root@linkedart.net:/srv/linkedart/
```

Then on the server `cd /srv/linkedart && cp .env.example .env` and fill it in. Running as root, the `sudo`
prefixes and the `usermod` line are redundant; they matter if you administer the box as an ordinary user.

## 3. DNS at Mythic Beasts

| Record | Name | Value |
|---|---|---|
| A | `@` | the server's IPv4 |
| A | `www` | the server's IPv4 |
| A | `dates` | the server's IPv4 |
| AAAA | `@`, `www`, `dates` | the server's IPv6 |

Caddy requests certificates on first start, so the records must resolve before `docker compose up`.

## 4. Images

`.github/workflows/docker.yml` builds `ghcr.io/tomcrane/linked-art-net/examples-site` and
`.../dates-service` on every `v*` tag, tagged with the version and `latest`. So the first deployment needs a
tag:

```
git tag v0.1.0 && git push origin v0.1.0
```

GHCR packages are private by default. Either make both packages public (package settings on GitHub; the
code is MIT) or `docker login ghcr.io` on the server with a read-only token before pulling.

## 5. GitHub

- **OAuth app** for the trainer login: homepage `https://linkedart.net`, callback
  `https://linkedart.net/dates/train/signin-github`. Client id and secret go in `.env`.
- **Reviewers**: GitHub logins allowed into the trainer, `TRAINER_REVIEWER` in `.env` (one; add
  `Trainer__Reviewers__1` lines to the compose file for more).
- **Submission token**: a fine-grained personal access token scoped to the repository with Contents and
  Pull requests read and write, so the trainer can open pull requests under `corpus/proposed/`.
- **Anthropic API key** (optional, 2026-10-17): `ANTHROPIC_API_KEY` in `.env` lets the trainer ask Claude for proposed
  readings (08-corpus-contributions.md section 7). Create it in the Console organisation linked to the Claude plan's
  monthly API credits, in a workspace of its own with a spend limit. Only the trainer uses it; the public service never
  calls a model. Add it to `.env`, then `docker compose up -d` recreates the site container with it.

## 6. Run

```
cd /srv/edge && docker compose up -d                 # Caddy, once; reload after editing sites/
cd /srv/linkedart && docker compose pull && docker compose up -d
docker compose logs -f --tail=100
```

Caddy's first start fetches three certificates; `docker compose logs caddy` in `/srv/edge` shows them being
obtained. Then check `https://linkedart.net/healthz`, `https://dates.linkedart.net/healthz`, the playground at
`/dates`, the API page at `/dates/api`, and sign in at `/dates/train`. The site calls the service over the compose network (`http://linkedart-dates:8080`), so the
playground works even if the public `dates` host is misconfigured; its "raw JSON" links point at the public host
(`TimeSpanService__PublicUrl`).

## 7. Updating: the release rhythm

**`main` is what is deployed; `de-pmc` is the working branch.** Every release is the same three steps:

```
# 1. on de-pmc, when the tree is green (dotnet test linked-art-net.slnx)
git tag v0.1.x && git push origin v0.1.x          # "Container images" workflow builds and publishes both images

# 2. bring main level with it, so the repository's front page shows the running code
git switch main && git merge --ff-only de-pmc && git push origin main && git switch de-pmc

# 3. on the server, once the workflow is green
cd /srv/linkedart && docker compose pull && docker compose up -d
```

Caddy is untouched by app updates. Pin `IMAGE_TAG` in `.env` to the version if you prefer explicit rollbacks;
`latest` follows the newest tag. Nothing is ever committed to `main` directly, which is why step 2 is always a
fast-forward; a refusal means a stray commit on `main` to move across first. First done 2026-10-16 with v0.1.0.

## 8. Data and backups

Two things on the server are not in git: `data/trainer/` (the queue and submissions, two JSON files) and
`data/capture/unparsed.jsonl` (inputs the service could not parse with confidence; the trainer imports it).
Losing them loses unreviewed queue items and uncollected captures, not the corpus, which is in pull requests.
`.env` holds the secrets; it is not backed up by the scripts below, so keep its values where you keep secrets.

**Nightly snapshot on the box** (added 2026-10-17): `deploy/linkedart/backup.sh` tars `/srv/linkedart/data` into
`/srv/backups/linkedart/data-<utc stamp>.tar.gz`, keeps 30 days, and points `latest.tar.gz` at the newest. Install once,
from the repository root on your machine:

```
scp deploy/linkedart/backup.sh deploy/linkedart/linkedart-backup.cron root@linkedart.net:/root/
```

and on the server:

```
install -m 755 /root/backup.sh /usr/local/bin/linkedart-backup
install -m 644 /root/linkedart-backup.cron /etc/cron.d/linkedart-backup
linkedart-backup && linkedart-backup --list        # first snapshot now, and what it holds
```

Cron runs it at 03:17 UTC and appends a line to `/var/log/linkedart-backup.log`. The two files are kept LF by
`.gitattributes`; if a copy ever arrives with CRLF, cron silently ignores it, so `cat -A /etc/cron.d/linkedart-backup`
should show no `^M`.

**Off-box copy**: `deploy/linkedart/pull-backup.ps1` pulls `latest.tar.gz` to `%USERPROFILE%\Backups\linkedart` over
your SSH key and keeps 26 copies. Run it when you think of it, or weekly from Task Scheduler. The archives are a few
kilobytes.

**Restore**: `cd /srv/linkedart && docker compose down && tar -xzf /srv/backups/linkedart/<file>.tar.gz -C /srv/linkedart
&& chown -R 1654:1654 data && docker compose up -d`. The archive holds `data/...` paths, so it unpacks over the live
directory.

## 9. Behind the proxy

Both containers set `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` so ASP.NET trusts Caddy's `X-Forwarded-Proto`
and `X-Forwarded-For`. Without it the OAuth callback would be built as `http://` and GitHub would refuse it,
and the service's per-IP rate limit would see every request as coming from Caddy.

## 10. Costs

Fixed: £10.60 a month ex VAT for VPS 2 with 20 GB SSD and IPv4 (£106 a year paid yearly), and the domain at
£13.50 a year with DNS included. No metered charges: bandwidth is
included up to the plan's allowance, certificates are free, and there are no logging or load balancer fees.

## 11. Adding another app later

Each app is its own compose project under `/srv/<name>/`, joined to the `web` network, with a `container_name`
Caddy can address. Three steps, none of which touch linkedart.net:

1. Start the app: a compose file like `deploy/linkedart/docker-compose.yml` with `networks: [web]` and no
   `ports:` (Caddy reaches it over the network; nothing else needs to).
2. Route it: a file `/srv/edge/sites/<name>.caddy` naming its hostname(s) and `reverse_proxy <container>:<port>`,
   then `docker compose exec caddy caddy reload --config /etc/caddy/Caddyfile` in `/srv/edge`.
3. Point DNS at the server: A and AAAA records for the hostname, at whichever registrar holds that domain.

Caddy fetches a certificate for the new hostname on the first request. A different domain name is no different
from a subdomain here; Caddy routes on the hostname it is asked for.
