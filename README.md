# Project Management App

> Teljes körű, valós idejű projekt menedzsment alkalmazás  
> **ASP.NET Core + Svelte + PostgreSQL + SignalR**

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
- **Sprint menedzsment** – teljes lifecycle: `Planning → Active → Completed`, backlog kezelés, sprint lezárás befejezetlen task kezeléssel
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
| **Nginx** *(production)* | Reverse proxy, WebSocket proxy, SSL termination |
| **Let's Encrypt** *(production)* | Automatikus SSL tanúsítvány |

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
# JWT
JWT_SECRET=your-jwt-secret-min-32-chars

# PostgreSQL
DB_HOST=localhost
DB_PORT=5432
DB_NAME=projectmanager
DB_USER=pmuser
DB_PASSWORD=pmpassword

# MinIO
MINIO_ENDPOINT=localhost:9000
MINIO_ACCESS_KEY=minioadmin
MINIO_SECRET_KEY=minioadmin
MINIO_BUCKET=project-manager

# API
API_BASE_URL=https://localhost:5178
```

### 1. Adatbázis indítása Dockerrel

```bash
docker-compose up -d
```

Ez elindít egy PostgreSQL 17 konténert a következő beállításokkal: (ez csak development config)
- **Host:** `localhost:5432`
- **Database:** `projectmanager`
- **User:** `pmuser` / **Password:** `pmpassword`

### 2. Backend indítása

```bash
dotnet restore
dotnet ef database update   # migrációk futtatása
dotnet run
```

Az API elérhető: `https://localhost:5173` (Swagger UI: `/swagger`)

### 3. Frontend indítása

```bash
cd frontend
npm install
npm run dev
```

Az alkalmazás elérhető: `http://localhost:5173`

---

## Fejlesztési ütemterv

A részletes haladásnaplót a [`SCHEDULE.md`](./SCHEDULE.md) tartalmazza.

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
| 2026-05-17 | Deployment & dokumentáció | Folyamatban |

---

## Ismert limitációk & Tervezett fejlesztések

- **SignalR centralizálás** – komponensenkénti event kezelés helyett AppLayout szintű centralizált megoldás
- **WebhookSecret titkosítás** – jelenleg plain text tárolás, tervezett AES-256 titkosítás
- **File feltöltés optimalizálás** – méret limit konfiguráció, chunked upload
- **TOTP 2FA** – bejelentkezés és kritikus műveletek védelme
- **GitLab webhook** – teljes támogatás és tesztelés
- **Redis backplane** – horizontális skálázáshoz SignalR-rel

Részletes tervek: [`SCHEDULE.md`](./SCHEDULE.md)