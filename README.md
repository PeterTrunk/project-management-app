# Project Management App

> Teljes körű, valós idejű projekt menedzsment alkalmazás  
> **ASP.NET Core + Svelte + PostgreSQL + SignalR**

**Élő demo:** [app.trunkpeter.com](https://app.trunkpeter.com)

---

## Projektleírás

Egy **Jira/Linear ihletésű projekt menedzsment webalkalmazás**, amely Kanban-alapú task kezeléssel, sprint lifecyclekezeléssel, valós idejű SignalR frissítésekkel és Git webhook integrációval rendelkezik. A projekt fullstack architektúrán alapul: C# ASP.NET Core backend, Svelte TypeScript frontend, PostgreSQL adatbázis, és Docker alapú deployment.

---

## Főbb funkciók

### Már implementált
- **JWT autentikáció** – bejelentkezés, regisztráció, token rotáció (refresh token), jelszócsere
- **RBAC jogosultságkezelés** – 4 projektszerep: `Owner`, `Admin`, `Member`, `Viewer`
- **Projekt & Task CRUD** – automatikus board/oszlop létrehozás, `PM-1` stílusú Task Key generálás
- **Kanban tábla** – drag & drop oszlopok és taskok között (`svelte-dnd-action`)
- **Lexorank pozicionálás** – iparági standard string-alapú rendezési algoritmus, BigInteger alapú implementáció
- **Sprint menedzsment** – teljes lifecycle: `Planning -> Active -> Completed`, backlog kezelés, sprint lezárás befejezetlen task kezeléssel
- **SignalR valós idejű frissítések** – task mozgatás, oszlop és sprint változások azonnal megjelennek minden bejelentkezett felhasználónál
- **Kommentek & Labelek** – task szintű kommentelés és projekten belüli label kezelés
- **Git Webhook integráció** – GitHub/GitLab commit és PR automatikus task-összerendelés regex alapján (`PM-123`), unmatched commit/PR manuális hozzárendeléssel, webhook verifikáció ping event alapján
- **MinIO fájltárolás** – task és projekt szintű csatolmányok S3-kompatibilis objektumtárolóban, streaming letöltés
- **Statisztika Dashboard** – ECharts alapú grafikonok: burndown/burnup, sprint velocity, team workload, task státusz eloszlás, Cumulative Flow Diagram
- **Team Management** – tagok meghívása (meghívólink), szerepkör kezelés
- **Activity Log** – projekt szintű aktivitásnapló szűréssel (felhasználó, típus, dátum)
- **Dark/Light mód** – témaváltás localStorage perzisztenciával
- **Overview Dashboard** – személyes task összefoglaló, overdue jelzések, sprint progress
- **Search & Filter** – Board szintű keresés/szűrés (assignee, prioritás, label, határidő)

### Tervezett / Fejlesztés alatt
- Az alap MVP terv minden fejezete elkészült.
- További fejleszések és limitációk a dokumentum végén.

---

## Architektúra

```
project-management-app/
    backend/            # ASP.NET Core Web API
    frontend/           # Svelte + TypeScript SPA (Vite)
    docs/               # Tervezési Dokumentáció (Többnyire már nem aktuális)
    docker-compose.yml
    SCHEDULE.md         # Fejlesztési ütemterv és haladásnapló
    TESTING.md          # Tesztelési dokumentáció
```

---

## Technológiai stack

### Backend
| Technológia | Szerepe |
|---|---|
| **ASP.NET Core** (C#) | REST API |
| **Entity Framework Core** | ORM, Code-First migrációk |
| **PostgreSQL 17** | Relációs adatbázis |
| **SignalR** | Valós idejű WebSocket kommunikáció |
| **JWT + BCrypt** | Autentikáció és jelszó hash-elés |
| **FluentValidation** | Input validáció |
| **Swagger / OpenAPI** | API dokumentáció |

### Frontend
| Technológia | Szerepe |
|---|---|
| **Svelte + TypeScript** | SPA keretrendszer |
| **Vite** | Build tool |
| **svelte-spa-router** | Kliens oldali routing |
| **svelte-dnd-action** | Drag & Drop |
| **axios** | HTTP kliens, JWT interceptorral |
| **@microsoft/signalr** | SignalR kliens |
| **ECharts** | Statisztika grafikonok |
| **lucide-svelte** | Ikon könyvtár |

### Infrastruktúra
| Technológia | Szerepe |
|---|---|
| **Docker Compose** | Konténerizált fejlesztői és production környezet |
| **MinIO** | S3-kompatibilis fájltárolás |
| **Nginx** | Frontend statikus fájl kiszolgálás + SPA routing (frontend konténer) |
| **Traefik** *(Dokploy)* | Reverse proxy, WebSocket proxy, SSL termination |
| **Let's Encrypt** *(Traefik)* | Automatikus SSL tanúsítvány |

---

## Adatbázis séma főbb entitásai

A séma tervrajza [dbdiagram.io](https://dbdiagram.io)-val készült.

| Entitáscsoport | Táblák |
|---|---|
| Felhasználók | `Users`, `ProjectMembers`, `RefreshTokens`, `ProjectInvites` |
| Projekt struktúra | `Projects`, `ProjectCounters`, `Boards`, `ColumnDefinitions`, `ProjectTasks`, `TaskAssignments` |
| Sprint | `Sprints`, `TaskStatusHistories` |
| Kommunikáció | `Comments`, `Labels`, `LabelTasks` |
| Git integráció | `Integrations`, `CommitLinks`, `PrLinks` |
| Fájlok | `Attachments` |
| Napló | `Activities` |

---

## Jogosultsági rendszer (RBAC)

A négy projektszerep hierarchikus jogosultság-ellenőrzéssel működik:

```
Owner >= Maintainer >= Member >= Viewer
```

ASP.NET Core custom `AuthorizationHandler`-rel megvalósítva, route-alapú `projectId` kinyeréssel. 4 policy definiálva: `ProjectOwner`, `ProjectMaintainer`, `ProjectMember`, `ProjectViewer`.

---

## Lexorank pozicionálás

A Kanban kártyák és oszlopok sorrendjét **Lexorank** algoritmussal kezeli a rendszer – ugyanaz a megoldás, amit a Jira és Linear is használ.

Közbeszúráskor mindig csak 1 sor frissül az adatbázisban. String alapú, Base36 karakterkészlettel, öngyógyító bucket rendszerrel – a hely sosem fogy el. Ütközés esetén automatikus rebalancing triggerelődik.

---

## SignalR eseménytérkép

A backend minden jelentős változásra broadcastol a megfelelő project/board szobába:

| Esemény | Trigger |
|---|---|
| `TaskAssigneeAdded`, `TaskAssigneeRemoved` | Assignee műveletek |
| `TaskLabelAdded`, `TaskLabelRemoved` | Task label műveletek |
| `TasksRebalanced` | Lexorank rebalancing |
| `MemberAdded`, `MemberRemoved`, `MemberRoleUpdated` | Team műveletek |
| `ProjectArchived`, `ProjectUnarchived` | Projekt archiválás |
| `ActivityCreated` | Activity log |
| `IntegrationCreated`, `IntegrationDeleted`, `IntegrationVerified`, `IntegrationUpdated` | Git integráció műveletek |
| `CommitLinked`, `PrLinked` | Git webhook események |
| `AttachmentUploaded`, `AttachmentDeleted` | Fájl műveletek |

---

## Fejlesztői indítás

### Előfeltételek

- [Docker Desktop](https://www.docker.com/products/docker-desktop/)
- [.NET 8+ SDK](https://dotnet.microsoft.com/download)
- [Node.js 20+](https://nodejs.org/)
- [ngrok](https://ngrok.com/) *(opcionális, Git webhook lokális teszteléshez)*

### Environment Variables

A projekt gyökerében hozz létre egy `.env` fájlt:

```env
# PostgreSQL
DATABASE_URL=Host=localhost;Port=5432;Database=projectmanager;Username=pmuser;Password=pmpassword
POSTGRES_DB=projectmanager
POSTGRES_USER=pmuser
POSTGRES_PASSWORD=pmpassword

# Frontend URL (CORS-hoz)
FRONTEND_URL=http://localhost:5173

# JWT
JWT_SECRET=your-jwt-secret-min-32-chars
JWT_ISSUER=ProjectManager.API
JWT_AUDIENCE=ProjectManager.Client
JWT_EXPIRY_MINUTES=60

# MinIO
MINIO_ENDPOINT=localhost:9000
MINIO_ACCESS_KEY=minioadmin
MINIO_SECRET_KEY=minioadmin
MINIO_BUCKET=project-manager
MINIO_USE_SSL=false

# API
API_BASE_URL=http://localhost:5178
```

### 1. Háttérszolgáltatások indítása Dockerrel

```bash
docker-compose up -d
```

Ez elindít egy PostgreSQL 17 és egy MinIO konténert a következő beállításokkal (ez csak development config):
- **PostgreSQL:** `localhost:5432` — Database: `projectmanager`, User: `pmuser` / Password: `pmpassword`
- **MinIO:** `localhost:9000` (API), `localhost:9001` (Console)

### 2. Backend indítása

```bash
cd backend/src/ProjectManager.API
dotnet restore
dotnet ef database update   # migrációk futtatása
dotnet run
```

Az API elérhető: `http://localhost:5178` (Swagger UI: `/swagger`)

### 3. Frontend indítása

```bash
cd frontend
npm install
npm run dev
```

Az alkalmazás elérhető: `http://localhost:5173`

### 4. (Opcionális) Git Webhook lokális tesztelése ngrok-kal

A Git webhook (GitHub/GitLab) csak publikusan elérhető URL-re tud kéréseket küldeni, ezért lokális fejlesztéshez egy ngrok tunnel szükséges a backend porton:

```bash
ngrok http 5178
```

Az ngrok ad egy publikus URL-t (pl. `https://random-id.ngrok-free.dev`) — ezt add meg `API_BASE_URL`-ként a `.env`-ben, majd ezt az URL-t használd a GitHub/GitLab integráció webhook címeként a projektben.

---

## Production Deployment

Az alkalmazás éles környezetben Hetzner VPS-en fut, Dokploy (self-hosted PaaS) segítségével, Cloudflare DNS és SSL mögött.

### Infrastruktúra

| Komponens | Megoldás |
|---|---|
| Szerver | Hetzner Cloud VPS |
| Domain & DNS | Cloudflare (Proxy: ON, SSL: Full Strict) |
| PaaS / Orchestration | Dokploy (Docker Swarm + Traefik) |
| SSL tanúsítvány | Let's Encrypt (Dokploy/Traefik automatikus) |
| Reverse Proxy | Traefik (Dokploy beépített) |

**Domain struktúra:**
- `app.trunkpeter.com`: Frontend (Svelte SPA, Nginx)
- `api.trunkpeter.com`: Backend API + SignalR Hub

### Deployment lépések

1. **Hetzner VPS + Dokploy telepítés**
```bash
   curl -sSL https://dokploy.com/install.sh | sh
```
   (Docker Swarm manuális inicializálás szükséges lehet: `docker swarm init --advertise-addr <SZERVER_IP>`)

2. **Cloudflare DNS** — két A rekord a szerver IP-jére (`app` és `api` subdomain), Proxy: ON

3. **Dokploy projekt létrehozása** — Docker Compose alapú service, GitHub repo összekötése, `docker-compose.prod.yml` mint compose fájl megadása

4. **Environment Variables** beállítása Dokploy UI-ban (lásd lentebb)

5. **Deploy** — Dokploy automatikusan build-eli a `backend/Dockerfile` és `frontend/Dockerfile` alapján mindkét service-t; push-ra automatikus újradeploy fut

### Production Environment Variables

```env
# PostgreSQL
POSTGRES_DB=projectmanager
POSTGRES_USER=pmuser
POSTGRES_PASSWORD=
DATABASE_URL=Host=postgres;Port=5432;Database=projectmanager;Username=pmuser;Password=

# JWT
JWT_SECRET=
JWT_ISSUER=ProjectManager.API
JWT_AUDIENCE=ProjectManager.Client
JWT_EXPIRY_MINUTES=60

# MinIO
MINIO_ENDPOINT=minio:9000
MINIO_ACCESS_KEY=
MINIO_SECRET_KEY=
MINIO_BUCKET=project-manager
MINIO_USE_SSL=false

### Production Environment Variables

```env
# PostgreSQL
POSTGRES_DB=projectmanager
POSTGRES_USER=pmuser
POSTGRES_PASSWORD=
DATABASE_URL=Host=postgres;Port=5432;Database=projectmanager;Username=pmuser;Password=

# JWT
JWT_SECRET=
JWT_ISSUER=ProjectManager.API
JWT_AUDIENCE=ProjectManager.Client
JWT_EXPIRY_MINUTES=60

# MinIO
MINIO_ENDPOINT=minio:9000
MINIO_ACCESS_KEY=
MINIO_SECRET_KEY=
MINIO_BUCKET=project-manager
MINIO_USE_SSL=false

# Domains & URLs
API_BASE_URL=https://[API_BASE_URL]
FRONTEND_URL=https://[FRONTEND_URL]
DOMAIN=[API_BASE_URL]
FRONTEND_DOMAIN=[FRONTEND_URL]

# Frontend build args
VITE_API_URL=https://[API_BASE_URL]
VITE_SIGNALR_KEEPALIVE_ENABLED=true
VITE_SIGNALR_KEEPALIVE_SECONDS=15
```

### SignalR WebSocket a Cloudflare mögött

A Cloudflare proxy ~100 másodperc inaktivitás után bontja a WebSocket kapcsolatokat. Ennek elkerülésére a frontend SignalR kliens rendszeres keepalive ping-et küld (`VITE_SIGNALR_KEEPALIVE_SECONDS`, alapértelmezetten 15mp), ami környezeti változóból ki-/bekapcsolható.

### Biztonsági fejlécek

A Traefik `frontend-security` middleware-en keresztül beállított fejlécek: `Content-Security-Policy` (script-src, style-src, connect-src, object-src 'none', frame-ancestors 'none', form-action 'self'), `X-Frame-Options: DENY`, `Referrer-Policy: strict-origin-when-cross-origin`, `Cross-Origin-Resource-Policy: same-origin`.

Eredmény: [MDN HTTP Observatory](https://developer.mozilla.org/en-US/observatory) **A+ (125/100)**.

A teljes infrastruktúra-döntésekről, gotchákról és implementációs sorrendről bővebben: [`SCHEDULE.md`](./SCHEDULE.md).

---

## Fejlesztési ütemterv

| Hét | Témakör | Státusz |
|---|---|---|
| 2026-02-22 | Dev környezet & adatbázis design | Kész |
| 2026-03-01 | EF Core modellek & migrációk | Kész |
| 2026-03-08 | JWT autentikáció & RBAC | Kész |
| 2026-03-15 | Projekt & Task CRUD API | Kész |
| 2026-03-22 | Svelte frontend alap & layout | Kész |
| 2026-03-29 | Kanban tábla & Task kezelés | Kész |
| 2026-04-05 | SignalR valós idejű frissítések | Kész |
| 2026-04-12 | Sprint & Team Management | Kész |
| 2026-04-19 | Git Webhook & MinIO fájltárolás | Kész |
| 2026-04-26 | Statisztika Dashboard & ECharts | Kész |
| 2026-05-03 | Keresés/szűrés & UI finomítás | Kész |
| 2026-05-10 | Tesztelés & hibajvítás | Kész |
| 2026-05-17 | Deployment & dokumentáció | Kész |

---

## Ismert limitációk & Tervezett fejlesztések

- **SignalR centralizálás** – komponensenkénti event kezelés helyett AppLayout szintű centralizált megoldás
- **WebhookSecret titkosítás** – jelenleg plain text tárolás, tervezett AES-256 titkosítás
- **File feltöltés optimalizálás** – méret limit konfiguráció, chunked upload
- **TOTP 2FA** – bejelentkezés és kritikus műveletek védelme
- **GitLab webhook** – teljes támogatás és tesztelés
- **Redis backplane** – horizontális skálázáshoz SignalR-rel

| Fejezet | Témakör |
|---|---|
| SignalR Architecture Refactor | Centralizált event kezelés, Redis backplane előkészítés |
| Team & Project Improvements | Meghívó kezelés, csapat terhelés szétválasztás |
| File Upload Improvements | Fájlméret limit, content-type szűrés, chunked upload |
| Security Hardening | TOTP 2FA, AES-256 webhook secret titkosítás |
| Git Webhook Enhancements | PR body matching, GitLab támogatás, provider absztrakció |
| Git View Sprint Overview | Sprintenkénti commit/PR áttekintés és átrendelés |
| Git Intelligence – Branches & Insights | Branch követés, sprint git analitika |
| Multi-Sprint Analytics | Sprintek közötti összehasonlítás (3-4 sprint adat után) |

Részletes leírások: [`SCHEDULE.md`](./SCHEDULE.md)