# Project Management App

> "Teljes körű", valós idejű projekt menedzsment alkalmazás  
> **ASP.NET Core + Svelte + PostgreSQL + SignalR**

**Élő demo:** [app.trunkpeter.com](https://app.trunkpeter.com)

---

## Projektleírás

Egy **Jira/Linear ihletésű projekt menedzsment webalkalmazás**, amely Kanban-alapú task kezeléssel, sprint lifecyclekezeléssel, valós idejű websocket (SignalR) frissítésekkel és Git webhook integrációval rendelkezik. A projekt fullstack architektúrán alapul: C# ASP.NET Core backend, Svelte TypeScript frontend, PostgreSQL adatbázis, és Docker alapú deployment.

Az alkalmazás **önhosztolható**: minden konfiguráció környezeti változóból érkezik, a komponensek szabványos Docker konténerek.

---

## Főbb funkciók

### Hitelesítés és jogosultság
- **JWT autentikáció** – bejelentkezés, regisztráció, token rotáció, jelszócsere
- **HttpOnly refresh token süti** – `SameSite=Strict`, az access token kizárólag memóriában
- **TOTP kétfaktoros hitelesítés** – választható, a titok AES-GCM-mel titkosítva tárolva
- **RBAC jogosultságkezelés** – 4 projektszerep: `Owner`, `Admin`, `Member`, `Viewer`
- **Rate limiting** – bejelentkezés, regisztráció, jelszó-visszaállítás, webhook végpont
- **Fiókkezelés** – e-mail megerősítés, jelszó-visszaállítás, fióktörlés anonimizálással

### Projekt és feladatkezelés
- **Projekt & Task CRUD** – automatikus board/oszlop létrehozás, `PM-1` stílusú Task Key generálás
- **Kanban tábla** – drag & drop oszlopok és taskok között (`svelte-dnd-action`)
- **Lexorank pozicionálás (szöveg alapú)** – iparági standard string-alapú rendezési algoritmus, BigInteger alapú implementáció
- **Sprint menedzsment** – teljes lifecycle: `Planning -> Active -> Completed`, backlog kezelés, sprint lezárás befejezetlen task kezeléssel
- **Kommentek & Labelek** – task szintű kommentelés és projekten belüli label kezelés
- **Team Management** – tagok meghívása lejáró meghívólinkkel, szerepkör kezelés
- **Activity Log** – projekt szintű aktivitásnapló szűréssel (felhasználó, típus, dátum)
- **Search & Filter** – Board szintű keresés/szűrés (assignee, prioritás, label, határidő)

### Valós idejű működés
- **SignalR frissítések** – task mozgatás, oszlop- és sprintváltozások azonnal megjelennek minden csatlakozott kliensnél
- **Redis backplane** – több backend példány esetén az események minden kliensig eljutnak
- **Automatikus újracsatlakozás** – kézi keepalive a proxy-timeout kezeléséhez

### Integrációk
- **Git Webhook integráció** – GitHub és GitLab commit/PR automatikus task-összerendelés regex alapján (`PM-123`), a commit üzenetéből és a PR leírásából is
- **Provider-független payload feldolgozás** – a két szolgáltató eltérő payload-alakját normalizáló réteg fordítja közös formára
- **Hozzárendeletlen és kapcsolt elemek** – mindkettő böngészhető; a kapcsoltak kereshetők (sha, üzenet, szerző, task kulcs) és áthelyezhetők másik taskra
- **MinIO fájltárolás** – task és projekt szintű csatolmányok S3-kompatibilis objektumtárolóban, presigned URL-es feltöltés, streaming letöltés
- **Statisztika Dashboard** – ECharts alapú grafikonok: burndown, sprint velocity, team workload, task státusz eloszlás, Cumulative Flow Diagram

### Megfelelés és üzemeltetés
- **Adatvédelem** – adatkezelési tájékoztató, ÁSZF, elfogadás verziózott rögzítése, anonimizáló fióktörlés
- **Strukturált naplózás** – Serilog + Seq, személyes adat nélkül
- **Automatizált tesztkör** – 884 teszt, CI/CD

### Felület
- **Dark/Light mód** – témaváltás localStorage perzisztenciával
- **Overview Dashboard** – személyes task összefoglaló, overdue jelzések, sprint progress
- **Reszponzív kialakítás** – desktop és mobil felbontás

---

## Architektúra

```
project-management-app/
    backend/
        src/ProjectManager.API/     # ASP.NET Core Web API
        tests/
            ProjectManager.Tests/            # gyors unit tesztek (Docker nélkül)
            ProjectManager.IntegrationTests/ # valódi PostgreSQL konténerrel
    frontend/                       # Svelte + TypeScript SPA (Vite)
    docs/                           # Tervezési dokumentáció (többnyire már nem aktuális, a kiindulás elött megfogalmazott tervek)
    docker-compose.yml              # fejlesztői háttérszolgáltatások
    docker-compose.prod.yml         # éles környezet (Dokploy)
    .env.example                    # a környezeti változók teljes referenciája
    SCHEDULE.md                     # Fejlesztési ütemterv és haladásnapló
    TESTING.md                      # Tesztelési dokumentáció
    OPERATIONS.md                   # Mentés, visszaállítás, incidenskezelés, indulási ellenőrzőlista
```

---

## Technológiai stack

### Backend
| Technológia | Szerepe |
|---|---|
| **ASP.NET Core 10** (C#) | REST API |
| **Entity Framework Core** | ORM, Code-First migrációk |
| **PostgreSQL 17** | Relációs adatbázis |
| **Redis** | SignalR backplane és rate limiting |
| **SignalR** | Valós idejű WebSocket kommunikáció |
| **JWT + BCrypt** | Autentikáció és jelszó hash-elés |
| **Otp.NET** | TOTP kétfaktoros hitelesítés |
| **FluentValidation** | Input validáció |
| **Serilog + Seq** | Strukturált naplózás |
| **Resend** | Tranzakciós e-mail küldés |
| **Swagger / OpenAPI** | API dokumentáció *(csak fejlesztői környezetben)* |

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
| **Vitest** | Unit tesztek |

### Infrastruktúra
| Technológia | Szerepe |
|---|---|
| **Docker Compose** | Konténerizált fejlesztői és production környezet |
| **MinIO** | S3-kompatibilis fájltárolás |
| **Nginx** | Frontend statikus fájl kiszolgálás + SPA routing (frontend konténer) |
| **Traefik** *(Dokploy)* | Reverse proxy, WebSocket proxy, SSL termination |
| **Let's Encrypt** *(Traefik)* | Automatikus SSL tanúsítvány |
| **Backblaze B2** | Mentések offsite tárolása |

---

## Adatbázis séma főbb entitásai

A séma tervrajza [dbdiagram.io](https://dbdiagram.io)-val készült.

| Entitáscsoport | Táblák |
|---|---|
| Felhasználók | `Users`, `Roles`, `UserRoles`, `ProjectMembers`, `ProjectInvites` |
| Hitelesítés | `RefreshTokens`, `PasswordResetTokens` |
| Projekt struktúra | `Projects`, `ProjectCounters`, `Boards`, `ColumnDefinitions`, `ProjectTasks`, `TaskAssignments` |
| Sprint | `Sprints`, `TaskStatusHistories` |
| Kommunikáció | `Comments`, `Labels`, `LabelTasks` |
| Git integráció | `Integrations`, `CommitLinks`, `PrLinks` |
| Fájlok | `Attachments`, `PresignedUrlLogs` |
| Jogi megfelelés | `TermsVersions`, `UserTermsAcceptances` |
| Napló | `Activities` |

**Konkurenciavezérlés:** a `Boards`, `ColumnDefinitions`, `Sprints` és `ProjectTasks` táblákon a PostgreSQL `xmin` rendszeroszlopa szolgál optimista konkurencia-tokenként. Ezeket az entitásokat mozgatja egyszerre több felhasználó, tehát itt egy elvesztett módosítás érdemi információt semmisítene meg, ütközés esetén a válasz `409 Conflict`, nem néma felülírás.

A `TaskKey` generálása ellenben `Serializable` tranzakcióban fut, újrapróbálkozással: ott az ütközés nem elveszett módosítás, hanem duplikált kulcs lenne.

---

## Jogosultsági rendszer (RBAC)

A négy projektszerep hierarchikus jogosultság-ellenőrzéssel működik:

```
Owner >= Admin >= Member >= Viewer
```

ASP.NET Core custom `AuthorizationHandler`-rel megvalósítva, route-alapú `projectId` kinyeréssel. 4 policy definiálva: `ProjectOwner`, `ProjectAdmin`, `ProjectMember`, `ProjectViewer`.

Az ellenőrzés **fail-closed**: ismeretlen szerepkör vagy hiányzó/hibás route érték esetén a handler elutasít, nem engedélyez. Az `Owner` szerepkör nem adható át meghívással vagy szerepkör-módosítással, csak a projekt létrehozója lehet az.

Minden erőforrás **projekt-hatókörű útvonalon** érhető el (`/api/projects/{projectId}/...`), mert a jogosultság-ellenőrzés az útvonalból olvassa ki a projektazonosítót.

---

## Lexorank pozicionálás

A Kanban kártyák és oszlopok sorrendjét **Lexorank** algoritmussal kezeli a rendszer – ugyanaz a megoldás, amit a Jira és Linear is használ.

Közbeszúráskor mindig csak 1 sor frissül az adatbázisban. String alapú, Base36 karakterkészlettel, öngyógyító bucket rendszerrel – a hely sosem fogy el. Ütközés esetén automatikus rebalancing triggerelődik.

---

## SignalR eseménytérkép

A backend minden jelentős változásra broadcastol a megfelelő projekt-szobába (`project-{projectId}`). A csoportba lépés tagsági ellenőrzéshez kötött.

| Terület | Események |
|---|---|
| Task | `TaskCreated`, `TaskUpdated`, `TaskMoved`, `TaskDeleted`, `TasksRebalanced` |
| Task kapcsolatok | `TaskAssigneeAdded`, `TaskAssigneeRemoved`, `TaskLabelAdded`, `TaskLabelRemoved` |
| Board és oszlop | `BoardCreated`, `BoardUpdated`, `BoardDeleted`, `ColumnCreated`, `ColumnUpdated`, `ColumnDeleted`, `ColumnsReordered` |
| Sprint | `SprintCreated`, `SprintUpdated`, `SprintDeleted` |
| Projekt és label | `ProjectUpdated`, `ProjectDeleted`, `ProjectArchived`, `ProjectUnarchived`, `LabelCreated`, `LabelDeleted` |
| Csapat | `MemberAdded`, `MemberRemoved`, `MemberRoleUpdated` |
| Komment | `CommentAdded`, `CommentDeleted` |
| Git integráció | `IntegrationCreated`, `IntegrationUpdated`, `IntegrationDeleted`, `IntegrationVerified`, `CommitLinked`, `PrLinked` |
| Fájl | `AttachmentUploaded`, `AttachmentDeleted` |
| Napló | `ActivityCreated` |

A broadcast soha nem buktatja el a műveletet: az adat mentése után a SignalR hívás `try/catch`-ben fut, mert egy szétesett kapcsolat miatt nem adhatunk hibát egy sikeres műveletre.

---

## Biztonság

| Intézkedés | Megvalósítás |
|---|---|
| Jelszótárolás | BCrypt, rögzített 12-es workfactor |
| Access token | Kizárólag memóriában, rövid élettartammal |
| Refresh token | `HttpOnly` + `SameSite=Strict` + `Secure` süti, minden használatkor rotálva |
| Munkamenet-érvénytelenítés | Jelszóváltás, 2FA-művelet és token-visszajátszás esetén minden munkamenet visszavonódik |
| Kétfaktoros hitelesítés | Választható TOTP; a kikapcsoláshoz ismételt hitelesítés kell |
| Titkosítás | AES-GCM a TOTP titkokhoz és a webhook secretekhez, `enc:v1:` verziójelzéssel |
| Rate limiting | Redis-alapú atomikus számláló; e-mail és IP szintű korlát is |
| Webhook hitelesítés | HMAC-SHA256 (GitHub) és konstans idejű token-összevetés (GitLab), fail-closed ismeretlen providerre |
| Hibakezelés | Tipizált kivétel-hierarchia; csak az `AppException` üzenete hagyja el a szervert, minden más `500` + általános szöveg |
| Biztonsági fejlécek | CSP, HSTS, `frameDeny`, `Referrer-Policy`, `nosniff` – Traefik middleware-en |
| Konténerek | Nem-root felhasználóval futnak |

A fejlesztés során a rendszeren folyamatos biztonsági ellenőrzések és fejlesztések kerültek sorra; az azonosított problémák javítása, illetve a tudatosan nyitva hagyott tételek indoklása dokumentált. A biztonségi fejléc teszt eredménye: [MDN HTTP Observatory](https://developer.mozilla.org/en-US/observatory) **A+ (125/100)**.

---

## Adatvédelem és jogi megfelelés

Az alkalmazás személyes adatot kezel (e-mail cím, megjelenítendő név, és a git integráción keresztül a commit szerzők neve), ezért a megfelelés a fejlesztés része volt, nem utólagos kiegészítés:

- **Adatkezelési tájékoztató és ÁSZF** – bejelentkezés nélkül is elérhető, az adatfeldolgozók nevesítve
- **Elfogadás rögzítése** – verziózott, szerveroldalon kikényszerítve; a kliens megkerülhető, ezért a validátor dönt
- **Fióktörlés anonimizálással** – a `UserId` több táblában idegen kulcs, és más felhasználók projekt-előzményeit nem írhatjuk át; a törlés ezért visszafordíthatatlan anonimizálás
- **Activity-leírások sablonosítása** – a nevek olvasáskor kerülnek be, így az anonimizálás visszamenőleg is érvényesül, és az átnevezés sem hagy hátra elavult nevet
- **Automatikus takarítás** – lejárt tokenek és feltöltési naplósorok megőrzési szabály szerint törlődnek
- **Naplózás személyes adat nélkül** – az e-mail cím helyett kulcsolt lenyomat szerepel

A mentési politika, a visszaállítási eljárás és az incidenskezelés az [`OPERATIONS.md`](./OPERATIONS.md)-ben van leírva.

> A jogi szövegeket jogász nem nézte át. Valódi felhasználókkal induló szolgáltatásnál ez megéri.

---

## Tesztelés

| Tesztkör | Darab | Futtatás |
|---|---|---|
| Backend Unit tesztek | 690 | `dotnet test backend/tests/ProjectManager.Tests/ProjectManager.Tests.csproj` |
| Backend integrációs tesztek | 115 | `dotnet test backend/tests/ProjectManager.IntegrationTests/ProjectManager.IntegrationTests.csproj` |
| Frontend tesztek | 79 | `cd frontend && npm test` |

A gyors kör **Docker nélkül** fut (nincs adatbázis, nincs hálózat), ezért minden pusholásnál lefuthat. Az integrációs kör **valódi `postgres:17` konténert** indít (Testcontainers + Respawn): az EF Core InMemory provider nem ismeri az `xmin` leképzést, a szűrt egyedi indexeket és a `Serializable` szemantikát, tehát némán elfogadna olyan kódot, ami élesben elszállna.

A **CI** minden pusholásnál lefuttatja mindkét backend tesztkört, a frontend típusellenőrzést (`svelte-check`), a frontend teszteket és a buildet.

Részletek, tesztstratégia és a tudatosan lefedetlen területek: [`TESTING.md`](./TESTING.md).

---

## Fejlesztői indítás

### Előfeltételek

- [Docker Desktop](https://www.docker.com/products/docker-desktop/)
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 20+](https://nodejs.org/)
- [ngrok](https://ngrok.com/) *(opcionális, Git webhook lokális teszteléshez)*

### 1. Környezeti változók

Másold át a [`.env.example`](./.env.example) fájlt `.env` névre, és töltsd ki. **A fájl minden változónál leírja, mi történik nélküle**, és jelöli, melyik kötelező.

A helyi indításhoz legalább ezek kellenek:

```env
DATABASE_URL=Host=localhost;Port=5432;Database=projectmanager;Username=pmuser;Password=pmpassword
POSTGRES_DB=projectmanager
POSTGRES_USER=pmuser
POSTGRES_PASSWORD=pmpassword

JWT_SECRET=<legalabb-32-bajt>
JWT_ISSUER=ProjectManager.API
JWT_AUDIENCE=ProjectManager.Client
JWT_EXPIRY_MINUTES=60
JWT_REFRESH_TOKEN_LIFETIME=10080

# Pontosan 32 bajt base64-ben: (Generalas leiras az .env.example-ben)
ENCRYPTION_KEY=<32-bajt-base64>

MINIO_ENDPOINT=localhost:9000
MINIO_ACCESS_KEY=minioadmin
MINIO_SECRET_KEY=minioadmin
MINIO_BUCKET=project-manager
MINIO_USE_SSL=false

REDIS_CONNECTION=localhost:6379
```

> A `JWT_SECRET` 32 bájtnál rövidebb értékkel, illetve az `ENCRYPTION_KEY` nélkül az API **fail-fast módon meg sem indul**, ez szándékos.
>
> A `RESEND_API_KEY` nélkül a rendszer elindul, de nem megy ki e-mail. Fejlesztéshez a megerősítést kézzel is beállíthatod az adatbázisban.

A frontend `VITE_*` változói a `frontend/.env` fájlba kerülnek.

### 2. Háttérszolgáltatások indítása Dockerrel

```bash
docker-compose up -d
```

Ez négy konténert indít (fejlesztői konfiguráció):

| Szolgáltatás | Port | Megjegyzés |
|---|---|---|
| PostgreSQL 17 | `5432` | Database: `projectmanager`, User: `pmuser` / `pmpassword` |
| Redis | `6379` | SignalR backplane és rate limiting |
| MinIO | `9000` / `9001` | API / Console – `minioadmin` / `minioadmin` |
| Seq | `5341` / `8080` | Napló-fogadás / webes felület |

### 3. Backend indítása

```bash
cd backend/src/ProjectManager.API
dotnet restore
dotnet ef database update   # migrációk futtatása
dotnet run
```

Az API elérhető: `http://localhost:5178` (Swagger UI: `/swagger`)

### 4. Frontend indítása

```bash
cd frontend
npm install
npm run dev
```

Az alkalmazás elérhető: `http://localhost:5173`

### 5. (Opcionális) Git Webhook lokális tesztelése ngrok-kal

A Git webhook (GitHub/GitLab) csak publikusan elérhető URL-re tud kéréseket küldeni, ezért lokális fejlesztéshez egy ngrok tunnel szükséges a backend porton:

```bash
ngrok http 5178
```

Az ngrok ad egy publikus URL-t (pl. `https://random-id.ngrok-free.dev`), ezt add meg `API_BASE_URL`-ként a `.env`-ben, majd ezt az URL-t használd a GitHub/GitLab integráció webhook címeként a projektben.

A felületen minden integrációhoz tartozik egy **szolgáltató-specifikus beállítási útmutató**, a GitHub és a GitLab webhook beállítása érdemben eltér.

---

## Production Deployment

Az alkalmazás éles környezetben Hetzner VPS-en fut, Dokploy (self-hosted PaaS) segítségével, Cloudflare DNS és SSL mögött. **Ez egy példa telepítés**, lásd a [Hordozhatóság](#hordozhatóság) szakaszt.

### Éles infrastruktúra

| Komponens | Megoldás |
|---|---|
| Szerver | Hetzner Cloud VPS |
| Domain & DNS | Cloudflare (Proxy: ON, SSL: Full Strict) |
| PaaS / Orchestration | Dokploy (Docker Swarm + Traefik) |
| SSL tanúsítvány | Let's Encrypt (Dokploy/Traefik automatikus) |
| Reverse Proxy | Traefik (Dokploy beépített) |
| Backend példányszám | 2 replika, SignalR affinity sütivel |

**Domain struktúra:**
- `app.trunkpeter.com`: Frontend (Svelte SPA, Nginx)
- `api.trunkpeter.com`: Backend API + SignalR Hub

### Deployment lépések

1. **Hetzner VPS + Dokploy telepítés**
```bash
   curl -sSL https://dokploy.com/install.sh | sh
```
   (Docker Swarm manuális inicializálás szükséges lehet: `docker swarm init --advertise-addr <SZERVER_IP>`)

2. **Cloudflare DNS** – két A rekord a szerver IP-jére (`app` és `api` subdomain), Proxy: ON

3. **Dokploy projekt létrehozása** – Docker Compose alapú service, GitHub repo összekötése, `docker-compose.prod.yml` mint compose fájl megadása

4. **Environment Variables** beállítása a Dokploy UI-ban. A teljes lista a [`.env.example`](./.env.example)-ben van, minden változónál leírva, mi történik nélküle. Éles környezetben a fejlesztői beállításon túl ezek is kellenek:
   - `REDIS_PASSWORD`, `REDIS_CONNECTION` – a rate limiting és a SignalR backplane
   - `RESEND_API_KEY`, `EMAIL_FROM` – tranzakciós e-mailek
   - `SEQ_URL`, `SEQ_ADMIN_PASSWORD` – naplózás
   - `DOMAIN`, `FRONTEND_DOMAIN`, `MINIO_DOMAIN`, `COOKIE_DOMAIN`, `MINIO_PUBLIC_URL` – routing és sütik
   - a négy `VITE_LEGAL_*` változó – az adatkezelő adatai a jogi dokumentumokban

5. **Deploy** – Dokploy automatikusan build-eli a `backend/Dockerfile` és `frontend/Dockerfile` alapján mindkét service-t; push-ra automatikus újradeploy fut

Indulás előtt érdemes végigmenni az [`OPERATIONS.md`](./OPERATIONS.md) 4. fejezetének ellenőrzőlistáján.

### SignalR WebSocket a Cloudflare mögött

A Cloudflare proxy ~100 másodperc inaktivitás után bontja a WebSocket kapcsolatokat. Ennek elkerülésére a frontend SignalR kliens rendszeres keepalive ping-et küld (`VITE_SIGNALR_KEEPALIVE_SECONDS`, alapértelmezetten 15mp), ami környezeti változóból ki-/bekapcsolható.

A két backend replika miatt a Traefik **affinity sütit** ad a WebSocket kapcsolatokhoz, az események pedig Redis backplane-en jutnak át a példányok között.

### Biztonsági fejlécek

A Traefik middleware-eken beállított fejlécek: `Content-Security-Policy` (script-src, style-src, connect-src, object-src 'none', frame-ancestors 'none', form-action 'self'), `X-Frame-Options: DENY`, `Referrer-Policy: strict-origin-when-cross-origin`, `Cross-Origin-Resource-Policy: same-origin`, `Strict-Transport-Security`, `X-Content-Type-Options: nosniff`.

A teljes infrastruktúra-döntésekről, gotchákról és implementációs sorrendről bővebben: [`SCHEDULE.md`](./SCHEDULE.md).

---

## Hordozhatóság

Az alkalmazás **nincs a Dokployhoz kötve**. Az alkalmazáskódban nincs platformspecifikus rész: minden konfiguráció környezeti változóból érkezik, a komponensek szabványos Docker konténerek, a `replicas: 2` is sima Compose.

A platformhoz kötődő rész a **Reverse proxy routing deklarációja**, Traefik címkék három szolgáltatás `labels:` blokkjában. Másik környezetben ennek a megfelelőjét kell megírni:

| Cél | Mit kell átírni |
|---|---|
| Másik Traefik-alapú platform | Gyakorlatilag semmit |
| Sima VPS Nginxszel | A címkék helyére `nginx.conf` server blokkok + certbot |
| Kubernetes | A címkék helyére Ingress manifesztek; a konténerek változatlanok |

**Egy kivétel.** A frontend `VITE_*` változói **build-time** értékek: a Vite beépíti őket a bundle-be. Másik környezethez tehát **újra kell építeni a frontend image-et**, a környezeti változó átírása önmagában nem elég. Ez a `Dockerfile` `ARG` + `ENV` párjaival és a compose `build.args` blokkjával van kezelve, de a korlát megmarad.

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
| 2026-05-10 | Tesztelés & hibajavítás | Kész |
| 2026-05-17 | Deployment & dokumentáció | Kész |

Az MVP után következő munkák (SignalR refaktor, biztonsági megerősítés, tesztlefedettség, jogi megfelelés, git integráció) szintén elkészültek, a részletes haladásnapló a [`SCHEDULE.md`](./SCHEDULE.md)-ben van.

---

## Ismert limitációk & Tervezett fejlesztések

### Architektúra
- **Funkció-szerepkör mátrix** – a jelenlegi modell szigorú rangsor (`Owner >= Admin >= Member >= Viewer`); egy jogosultság-halmaz alapú mátrix rugalmasabb lenne, de a jogosultsági modell cseréjét jelenti
- **Nincs többbérlős (multi-tenant) architektúra** – egy telepítés egy csapatot vagy szervezetet szolgál ki; a hozzáférés projekt szinten korlátozott
- **Nincs SSO/SAML** – vállalati bevezetéshez ez általában alapkövetelmény

### Tesztelés és teljesítmény
- **UI / E2E tesztek** – a store-ok és segédfüggvények tesztelve vannak, a DOM-szintű tesztelés nem
- **Terheléses teszt és kapacitásbecslés** – a kötő korlát mérés nélkül nem állítható meg számokkal
- **Gyorsítótárazás** – a statisztikai lekérdezések a legdrágábbak; itt lenne értelme, ha a mérés indokolja
- **Adatbázis-skálázás** – kapcsolat-pooling, olvasási replika, az `Activities` tábla partícionálása

### Funkciók (Ezek sokszor már bizonyos funkciókra épülő komolyabb kibontakozottabb funkciók)
- **Git hivatkozás leválasztása** – az áthelyezés megvan, a hozzárendelés megszüntetése nem
- **Sprint szerinti git áttekintés** – commitok és PR-ek sprintenkénti csoportosítása, sprint szűrő
- **Branch követés és git analitika** – a webhook payloadból származtatott adatokból
- **Multi-Sprint Analytics** – sprintek közötti összehasonlítás
- **Help / Wiki modal** – beépített súgó

Részletes leírások és a tudatosan kihagyott elemek indoklása (Valahol a dokentum végén): [`SCHEDULE.md`](./SCHEDULE.md)
