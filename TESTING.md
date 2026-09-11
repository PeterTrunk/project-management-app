# Manual Test Results

## Unit Tesztek (xUnit)

1. xUnit — `dotnet test backend/tests/ProjectManager.Tests/ProjectManager.Tests.csproj`

Összesen **579 teszt**, futásidő ~0,4 másodperc. A projekt szándékosan függőségmentes:
nem kell hozzá Docker, adatbázis vagy hálózat, ezért a CI-ban minden pusholásnál lefut.

**Tiszta logika**
- `LexorankService` (26 teszt) — rendezőkulcs generálás és felezés
- `ProjectRoles` — szerepkör-hierarchia, `RankOf` fail-closed viselkedése ismeretlen szerepkörre
- `Common/Exceptions` — az `AppException` leszármazottak státuszkód-szerződése (400/403/404/409/429)

**Biztonsági segédosztályok**
- `EncryptionService` — AES-GCM oda-vissza, `enc:v1:` prefix, prefix nélküli (legacy) visszafejtés,
  nonce-ismétlés hiánya, hamisított tag és rossz kulcs elutasítása
- `SecureTokenGenerator` — URL-biztos kimenet, hossz, egyediség

**Middleware**
- `GlobalExceptionHandlerMiddleware` — az `AppException` üzenete kimegy a saját státuszkódjával,
  minden más 500-at és általános szöveget kap (a belső üzenet és a stack trace nem szivárog),
  továbbá a már elindult válasz `Abort()`-ot kap kiírás helyett

**Validátorok**
- Mind a 34 FluentValidation validátor, határértékekkel
- `ValidatorCoverageTests` — egy új validátor tesztek nélkül nem maradhat észrevétlen

**Konzisztencia**
- `LegalVersionTests` — a backend és a frontend dokumentumverziója nem csúszhat el
  (eltérés esetén minden regisztráció elbukna)

## Integrációs tesztek (xUnit + Testcontainers)

1. `dotnet test backend/tests/ProjectManager.IntegrationTests/ProjectManager.IntegrationTests.csproj`

**Ehhez Docker kell.** A tesztkód maga indít egy valódi `postgres:17` konténert, lefuttatja rá
a migrációkat, és a végén eldobja. Az első futás lassabb (image letöltés), utána másodpercek.

Azért valódi PostgreSQL és nem EF Core InMemory, mert az `AppDbContext` `xmin`
konkurenciavezérlésre, `ExecuteUpdateAsync`-re, Serializable tranzakciókra és egyedi indexekre
épül — ezeket az InMemory vagy nem tudja, vagy némán elfogadja. Az utóbbi a veszélyesebb: zöld
teszt, ami nem néz oda.

A tesztek elválasztását a **Respawn** adja: `TRUNCATE ... CASCADE` minden teszt **előtt**
(nem utána — így egy elszállt teszt állapota megvizsgálható marad).

Összesen **56 teszt** (55 aktív, 1 szándékosan kihagyott), futásidő ~2 másodperc.

**Füstteszt** — az infrastruktúra maga:
- a migrációk lefutottak, nincs függőben lévő
- a felseedelt gráf másik contextből is olvasható
- `CreatedAt`/`UpdatedAt` bélyegzés működik
- az `xmin` feltöltődik és változik módosításkor
- a Respawn üres adatbázist hagy minden teszt előtt

**Projekt-hatókör (IDOR)** — a mag 6 szolgáltatás mind a 28 hatókörös metódusa:
`TaskService`, `SprintService`, `ColumnService`, `BoardService`, `CommentService`,
`LabelService`.

A minta mindenhol azonos: az **A projekt azonosítójával** nyúlunk a **B projekt entitásához**,
és `NotFoundException`-t várunk. A mutáló metódusoknál a kivétel nem elég — friss contexttel
ellenőrizzük, hogy a sor tényleg megvan még és nem változott.

A `CrossProjectCoverageTests` reflexióval őrzi a lefedettséget: egy új, projekt-hatókörű
metódus nem maradhat teszt nélkül.

**Jogosultsági réteg** — `ProjectRoleHandler`, valódi adatbázissal:
- hiányzó és hibás formátumú felhasználói claim
- hiányzó és hibás route érték
- nem tag felhasználó
- a teljes szerepkör-hierarchia (Viewer &lt; Member &lt; Admin &lt; Owner), 10 kombinációban
- **fail-closed**: ismeretlen szerepkör, ismeretlen követelmény, és mindkettő egyszerre —
  ez utóbbi zárja a `-1 >= -1` lyukat

Egy `Skip`-elt teszt jelzi az ismert eltérést: a szinkron `SaveChanges()` nincs felülírva az
`AppDbContext`-ben, tehát ott kimarad az időbélyegzés. Ennek javítása külön döntés.

**Docker nélkül:** a `PMA_TEST_POSTGRES` környezeti változóval egy meglévő adatbázisra
irányítható. Ide soha ne a fejlesztői adatbázis kerüljön — a Respawn minden táblát ürít.

## Frontend tesztek (vitest)

1. `cd frontend && npm test` — **52 teszt**, ~0,4 másodperc
2. `cd frontend && npm run check` — típusellenőrzés, 0 hiba

Nem kell hozzá Docker és böngésző: a tesztelt store-ok tiszta függvények, `node` környezetben
futnak. jsdom sincs, mert egyikük sem nyúl `window`-hoz, `document`-hez vagy
`localStorage`-hoz.

**Mit fed le:** mind a **23 SignalR store handler** (`taskStore` 13, `boardStore` 7,
`sprintStore` 3). Ezeknél nincs backend háló — ha egy handler rossz sorra ír, a szerver adata
helyes marad, a felhasználó mégis hibás felületet lát valós időben.

A tesztek a triviális eseteken túl ezeket rögzítik:
- **idempotencia** — ugyanaz az esemény kétszer is megérkezhet újracsatlakozáskor
- a megnyitott task (`activeTask`) külön hivatkozás, annak is frissülnie kell
- a board törlése az oszlopait is viszi
- az oszlop-átrendezés rendez is, nem csak frissít
- az aktív sprint származtatott állapot, a `state` mezőből következik

A tesztfájlok a vizsgált kód mellett élnek (`lib/stores/taskStore.test.ts`), így a
`npm run check` **őket is típusellenőrzi**.

## E2E integration tesztelés (MVP szinten)
2. Manuális Integration Tesztek
2.1 Auth Flow
[x] Regisztráció érvényes adatokkal
[x] Regisztráció érvénytelen adatokkal (hibakezelés)
[x] Bejelentkezés helyes adatokkal
[x] Bejelentkezés helytelen adatokkal
[x] Token refresh működése
[x] Kijelentkezés
[x] Profil módosítás (displayName)
[x] Jelszó változtatás
2.2 Projekt Management
[x] Projekt létrehozás
[x] Projekt módosítás
[x] Projekt archiválás
[x] Projekt dearchiválás
[x] Archivált projekten nem lehet módosítani
[x] Projekt törlés 
[x] Több projekt ugyanolyan ProjKey-jel
2.3 Board & Oszlop Management
[x] Board létrehozás
[x] Board módosítás
[x] Board törlés
[x] Oszlop létrehozás
[x] Oszlop módosítás (név, MapsToStatus)
[x] Oszlop törlés (soft delete)
[x] Törölt oszlop nem jelenik meg a boardon
[x] Törölt oszlop CFD adatai megmaradnak
[x] Oszlop átrendezés (drag & drop)
[x] Backlog oszlop nem törölhető
[x] Taskot tartalmazó oszlop nem törölhető
2.4 Task Lifecycle
[x] Task létrehozás Projekt Backlogba
[x] Task létrehozás Board oszlopba
[x] Task módosítás (cím, leírás, prioritás, határidő)
[x] Task board hozzárendelés
[x] Task sprint hozzárendelés
[x] Task mozgatás oszlopok között (drag & drop)
[x] Task mozgatás Done oszlopba -> CompletedAt beállítás
[x] Task visszamozgatás Done-ból -> CompletedAt törlés
[x] Task assignee hozzáadás/eltávolítás
[x] Task label hozzáadás/eltávolítás
[x] Task törlés
[x] Lezárt sprint taskja nem mozgatható
2.5 Sprint Lifecycle
[x] Sprint létrehozás
bug: Sprint létrehozás után aktív projekt
   néha nullázódik -> projekt újraválasztás szükséges 
   -> Nem reprodukálható következetesen
   -> Figyelés szükséges
[x] Sprint módosítás (név, cél, dátumok)
[x] Sprint törlés (Planning státuszban)
[x] Sprint aktiválás (Planning -> Active)
[x] Taskok első oszlopba kerülnek aktiváláskor
[x] Sprint visszatervezés (Active -> Planning)
[x] Taskok Board Backlogba kerülnek visszatervezéskor
[x] Sprint lezárás (Active -> Completed)
[x] Befejezetlen taskok -> Backlog vagy következő sprint
[x] Befejezett taskok -> ClosedAt beállítás
[x] Sprint lezárás után taskok nem mozgathatók
2.6 Team Management
[x] Meghívó link generálás
[x] Meghívó link lejárat (ha van)
[x] Meghívóval csatlakozás
[x] Tag eltávolítás
[x] Role módosítás (Member -> Admin stb.)
[x] RBAC ellenőrzés (Viewer nem tud módosítani)
2.7 Git Webhook
[x] Integráció létrehozás (GitHub)
[x] Webhook URL másolás
[x] Ping event -> IsVerified = true
[x] Push event -> CommitLink létrehozás
[x] Push event task matching (PM-123 formátum)
[x] Push event unmatched commit (TaskId = null)
[x] PR megnyitás -> PrLink létrehozás (open)
[x] PR cím módosítás -> PrLink title frissítés
[x] PR lezárás merge nélkül -> state = closed
[x] PR merge -> state = merged, MergedAt beállítás
[x] Unmatched commit manuális task hozzárendelés
[x] Unmatched PR manuális task hozzárendelés
[x] Token regenerálás -> régi URL érvénytelen
[x] Secret reset -> IsVerified = false
[x] Integráció letiltás -> webhook nem fogadja el
2.8 File Feltöltés
[x] Task szintű fájl feltöltés
[x] Task szintű fájl letöltés
[x] Task szintű fájl törlés
[x] Projekt szintű fájl feltöltés (Team Resources)
[x] Projekt szintű fájl letöltés
[x] Projekt szintű fájl törlés
[x] Különböző fájltípusok (pdf, jpg, png, docx, xlsx)
[x] Nagy fájl feltöltés (>10MB)
2.9 Statisztikák
[x] Task státusz eloszlás (pie chart) — projekt szintű
[x] Task státusz eloszlás — sprint szintű
[x] Sprint burndown helyes adatok
[x] Sprint burnup helyes adatok
[x] Team workload helyes elosztás - Kérdéses: A backlogban lévő de már hozzárendelt taskokat is hozzászámolja, (ami még aktívan nincs munka alatt), esetleg kiegészítést hogy tervezett load, vagyis a jelenlegi / már megtörtént feladat szám és egy másik oszlop a majd tervezett taskoknak ami a tervezett sprint + backlog-ban lévőek.
[x] Sprint velocity befejezett sprinteknél
[x] CFD adatok helyessége (Valószinüleg jó de felvetődött bennem hogy a különböző boardokat hogy kezeli, Main boardon ha nincs testing de egy másik boardon van akkor is mutatja, át kell gondolni hogy valid e ez a szemlélettés így.)
[x] CFD megőrzi az adatokat oszlop törlés után
[x] Dátum szűrő CFD-n
[x] Sprint szűrő
2.10 Activity Log
[x] Task műveletek logolva
[x] Sprint műveletek logolva
[x] Board/Oszlop műveletek logolva
[x] Git webhook események logolva (System actor)
[x] Integráció műveletek logolva
[x] Szűrés felhasználó alapján
[x] Szűrés entitás típus alapján
[x] Szűrés dátum intervallum alapján
[x] Mai nap szűrő
[x] Lapozás működése
2.11 SignalR Real-time
[x] Két böngészőablak — task mozgatás szinkron
[x] Két böngészőablak — sprint aktiválás szinkron
[x] Két böngészőablak — komment hozzáadás szinkron
[x] Git webhook event megjelenik activity logban real-time
[x] IntegrationVerified badge megjelenik real-time
[x] Overview frissül task mozgatáskor

3. Edge Case Tesztek
3.1 Lexorank
[x] Sok task egy oszlopban (50+) — pozíciók egyediek
[ ] Taskok drag & drop gyors egymás utáni mozgatás (lokális futtatás esetén nem mérvadó, nincs egyértelmű kimenetel, meg kell fontolni Q használatát.)
[x] Rebalancing trigger és utána helyes sorrend
3.2 Concurrent Műveletek
[ ] Két user egyszerre mozgat taskot
[ ] Két user egyszerre módosítja ugyanazt a taskot
3.3 Határesetek
[x] Üres projekt (nincs board, sprint, task)
[x] Üres sprint lezárása
[x] Sprint lezárás az összes task kész
[x] Sprint lezárás egy sem kész
[x] Task mozgatás ugyanabba az oszlopba

4. UI/UX Tesztek
4.1 Dark/Light Mode
[x] Összes nézetben helyes megjelenés dark módban
[x] Összes nézetben helyes megjelenés light módban
[x] ECharts grafikonok helyes színek mindkét módban
[x] Téma megmarad oldal újratöltés után
4.2 Responsive
[x] Desktop (1920px) — teljes nézet
[x] Notebook (1366px) — kisebb sidebar
[x] Sidebar collapse működése
4.3 Overdue/Due Soon jelzések
[x] Lejárt task piros jelzés
[x] Hamarosan lejáró task sárga jelzés
[x] Kész task zöld jelzés
[x] Board filter: csak lejárt taskok
[x] Board filter: hamarosan lejáró taskok
