# Deploy

Truss generates deployment artifacts; it is not a deployment engine. The files below are yours to edit, and the tools that apply them are the ecosystem's own: docker, compose, ssh, `dotnet ef`. Provisioning infrastructure (databases, queues, DNS, IAM) stays with tools built for it.

---

## Production images

```
truss add docker
```

Writes a production `Dockerfile` beside every host — the API, each [split service](cli.md#truss-split) and the worker — plus a `.dockerignore`. A host born later gets its Dockerfile at birth.

The images are multi-stage and alpine-based: the SDK stage publishes just that host, the runtime stage carries icu for real globalization, runs as the non-root user the .NET images define, binds to 8080 and answers healthchecks at `/health`:

```
docker build -f src/MyShop.Api/Dockerfile -t myshop-api .
```

The compose file `truss new --docker` maintains keeps its job: development dependencies only. These images are what a registry, a VPS or a cluster runs.

---

## Preflight: truss deploy check

```
truss deploy check --env-file .env.production
truss deploy check
```

The number one cause of a first deploy crashlooping is a missing environment value, and the framework is the only one who knows the complete list: it derives it from the manifest. The check prints every value the installed modules will demand at boot, marks what the target is missing, and fails when a deploy would not survive:

```
  ok       ConnectionStrings__Default    the postgres database
  MISSING  Truss__Auth__Jwt__SigningKey  auth: appsettings.json carries the scaffold's development key; production must override it
  MISSING  Truss__Email__Resend__ApiKey  the Resend email provider

2 required value(s) missing. Deploying now would crashloop at boot.
```

Without `--env-file` the current environment is checked. The notes at the end state the facts that outgrow a single host: the inmemory transport does not cross processes, sqlite does not share state between hosts, and each split service is its own deployment with its own environment.

---

## A VPS over SSH

```
truss deploy init ssh
```

The cheapest production story that is still a real one: a single server running docker compose. Three files land in `deploy/`, none of them magical:

- **compose.production.yml**: every host as a service (`${REGISTRY}/myshop-api:${TAG}`), restarting on failure, reading `.env`, plus the backing services the manifest knows about (postgres, rabbitmq or redis) with volumes.
- **deploy.sh**: builds and pushes one image per host, refuses to ship if `truss deploy check` fails against the server's `.env`, packs the schema change as an EF migrations bundle and runs it on the server before the new images start, then `docker compose pull && up -d`. `./deploy/deploy.sh rollback` returns to the previously deployed tag.
- **.env.production.example**: every key the check will demand, one comment each, ready to copy to the server. The real `.env` lives only there; secrets never enter git.

The script assumes a server with docker and an SSH key, and nothing else. When the project outgrows one server, the same images feed a cluster; the k8s artifacts are on the roadmap.
