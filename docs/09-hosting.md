# Hosting linkedart.net

One virtual server, three containers: Caddy for TLS and routing, the examples site, the dates service. The
deployment files are in [deploy/](../deploy/). The domain `linkedart.net` is registered with Mythic Beasts
(2026-10-15), which also hosts its DNS.

Status (2026-10-15): files written; no server yet.

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
sudo ufw allow OpenSSH && sudo ufw allow 80 && sudo ufw allow 443 && sudo ufw enable
sudo mkdir -p /srv/linkedart/data/trainer /srv/linkedart/data/capture
sudo chown -R 1654:1654 /srv/linkedart/data   # the application user in the chiseled images
```

Copy `deploy/docker-compose.yml`, `deploy/Caddyfile` and `deploy/.env.example` (as `.env`) into
`/srv/linkedart/` and fill in `.env`.

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

## 6. Run

```
cd /srv/linkedart
docker compose pull
docker compose up -d
docker compose logs -f --tail=100
```

Check `https://linkedart.net/healthz`, `https://dates.linkedart.net/healthz`, the playground at `/dates`, and
sign in at `/dates/train`. The site calls the service over the compose network (`http://dates:8080`), so the
playground works even if the public `dates` host is misconfigured.

## 7. Updating

Tag a release, wait for the images workflow, then on the server `docker compose pull && docker compose up -d`.
Pin `IMAGE_TAG` in `.env` to the version if you prefer explicit rollbacks; `latest` follows the newest tag.

## 8. Data and backups

Two things on the server are not in git: `data/trainer/` (the queue and submissions, two JSON files) and
`data/capture/unparsed.jsonl` (inputs the service could not parse with confidence; the trainer imports it).
Back up `/srv/linkedart/data` nightly, either to the Mythic Beasts backup space or with a cron job that copies
the directory to a private repository or off-box storage. Losing it loses unreviewed queue items, not the
corpus, which is in pull requests.

## 9. Behind the proxy

Both containers set `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` so ASP.NET trusts Caddy's `X-Forwarded-Proto`
and `X-Forwarded-For`. Without it the OAuth callback would be built as `http://` and GitHub would refuse it,
and the service's per-IP rate limit would see every request as coming from Caddy.

## 10. Costs

Fixed: £10.60 a month ex VAT for VPS 2 with 20 GB SSD and IPv4 (£106 a year paid yearly), and the domain at
£13.50 a year with DNS included. No metered charges: bandwidth is
included up to the plan's allowance, certificates are free, and there are no logging or load balancer fees.
