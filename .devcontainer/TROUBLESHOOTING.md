# Devcontainer Troubleshooting

Kjente problemer ved bruk av devcontaineren for eShop-workshoppen, og planlagte tiltak hvis de dukker opp.

## Grpc.Tools `protoc` segfault på ARM

**Symptom:**

```
error MSB6006: "…/grpc.tools/2.80.0/tools/linux_arm64/protoc" exited with code 139
```

**Rot-årsak:** Binæren som Grpc.Tools leverer for `linux_arm64` er bygget mot en annen glibc enn det devcontainer-basebildet (`mcr.microsoft.com/devcontainers/dotnet:10.0`, Debian bookworm) har installert. Segfault ved dynamisk linking under proto-generering. Gjelder Apple Silicon (M-serie) der Docker kjører arm64-containere nativt.

Forsøkt (fungerer ikke): Grpc.Tools 2.78.0 → 2.79.0 → 2.80.0. Alle tre har samme arm64-bug i dette basebildet.

### Plan A (aktiv default) — pin containeren til `linux/amd64`

`devcontainer.json` har:

```json
"runArgs": ["--platform=linux/amd64"]
```

- **Apple Silicon:** container kjører via Rosetta. ~20–40 % treghet, men stabilt.
- **Intel Mac / Windows x64 / Linux x64 / Codespaces:** native, full fart.

**Forutsetning på Apple Silicon:** Docker Desktop → Settings → General → ✅ *"Use Rosetta for x86_64/amd64 emulation on Apple Silicon"*. Default på siden Docker Desktop 4.25.

### Plan B — native ARM via system-`protoc`

Hvis ytelsen på Apple Silicon blir et problem, kan man overstyre Grpc.Tools til å bruke et system-installert `protoc` i stedet for det dysfunksjonelle binæret.

1. Installer `protobuf-compiler` i basebildet (enten via devcontainer feature eller `postCreateCommand`):

   ```bash
   sudo apt-get update && sudo apt-get install -y protobuf-compiler
   ```

2. Legg til override i `Directory.Build.props`:

   ```xml
   <PropertyGroup>
     <Protobuf_ProtocFullPath>/usr/bin/protoc</Protobuf_ProtocFullPath>
   </PropertyGroup>
   ```

3. Fjern `"runArgs": ["--platform=linux/amd64"]` fra `devcontainer.json`.

Ikke testet enda — forsøk hvis amd64-pin blir for tregt i praksis.

### Plan C — bytt basebilde

Prøv et basebilde med eldre glibc (Ubuntu 22.04) og installer .NET 10 SDK manuelt i `postCreateCommand`. Kan løse glibc-mismatch uten å miste native ARM.

Ikke prioritert — Plan A holder for workshop.

---

## Build feiler ved første `aspire run` etter rebuild

**Symptom:** tilfeldige `error CS0246: type or namespace could not be found` rett etter container-rebuild.

**Løsning:** Incremental build state. Kjør én gang til, eller `dotnet clean eShop.Web.slnf && dotnet build eShop.Web.slnf`.

---

## Aspire dashboard åpner ikke automatisk

`portsAttributes.19888.onAutoForward` er satt til `openPreview`, men VS Code kan av og til miste det. Workaround:

- Command Palette → *Ports: Focus on Ports View*
- Finn `19888`, høyreklikk → *Open in Browser* (eller *Open in Simple Browser*).

---

## Docker-in-Docker OOM under første start

Aspire starter 4 containere samtidig (postgres, rabbitmq, redis, pgvector-init). På 16 GB-maskiner med mye annet kjørende kan Docker DoD gå OOM.

**Løsning:** lukk andre Docker-containere. Alternativt oppgrader Codespace til 8-core / 32 GB hvis det skjer i skyen.

---

## Playwright-tester timer ut

Chromium-installasjon kan feile ved første `postCreateCommand`. Kjør manuelt:

```bash
npx playwright install chromium
```

---

## dotnet dev-certs fungerer ikke i container

Dev-certen trustes inni containeren, ikke på hosten. Hvis man får SSL-feil i nettleseren mot `https://localhost:5243`, bruk `http://localhost:5223` i stedet (Identity API har begge) eller `ESHOP_USE_HTTP_ENDPOINTS=1`.
