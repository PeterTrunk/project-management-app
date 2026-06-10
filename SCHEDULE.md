# Project Schedule
- 2026-02-22: Development Environment Setup & Database Design
- 2026-03-01: EF Core Models, Migrations & Database Infrastructure
- 2026-03-08: Authentication & Authorization (JWT + RBAC)
- 2026-03-15: Project & Task CRUD API
- 2026-03-22: Svelte Frontend Setup & Core Layout
- 2026-03-29: Kanban Board & Task Management
- 2026-04-05: SignalR Real-Time Updates & Notifications _(tavaszi szunet - kiemelt fejlesztesi het)_
- 2026-04-12: Sprint Management & Team Management
- 2026-04-19: Git Webhook Integration & MinIO File Storage
- 2026-04-26: Statistics Dashboard & ECharts Integration
- 2026-05-03: Search/Filter, UI Polish & Responsive Design
- 2026-05-10: Testing, Bug Fixes & Final Integration
- 2026-05-17: Deployment, Documentation & Presentation Preparation

# Continuation (After MVP - kötetlen idő beosztással)
- x: SignalR Architecture Refactor
- x: Team & Project Improvements
- x: File Upload Improvements
- x: Security Hardening (TOTP 2FA)
- x: Git Webhook Enhancements
- x: Git View Sprint Overview
- x: Git Intelligence – Branches & Insights
- x: Multi-Sprint Analytics

## Development Environment Setup & Database Design
Docker Compose environment setup with PostgreSQL, MinIO (S3-compatible object storage), and Nginx reverse proxy. PostgreSQL schema design using dbdiagram.io, defining all core entities (users, projects, tasks, sprints, labels, comments, attachments, activity_log) with proper relations, constraints, and indexing strategy. Project repository initialization with backend (ASP.NET Core) and frontend (Svelte) folder structure.

**Kihagyott elemek (tudatos döntés)**
- MinIO és Nginx konfiguráció - a Git Webhook & MinIO héten kerül sorra
- Svelte frontend inicializálás - még nincs kipróbálható funkció

## EF Core Models, Migrations & Database Infrastructure
Entity Framework Core Code First model classes and initial migration. Npgsql provider configuration for PostgreSQL. Database triggers for automated fields (updated_at, task_key generation). Statistical views for reporting queries. Seed data for development and testing.

**Kihagyott elemek (tudatos döntés)**
- Statisztikai view-ok - a Statistics Dashboard héten (10. hét) kerül sorra
- task_key generálás - a CRUD API héten kerül sorra a ProjectCounter alapján

## Authentication & Authorization (JWT + RBAC)
JWT-based authentication with login, registration, refresh, logout, 
profile and password change endpoints. Password hashing with BCrypt. 
Token rotation: short-lived access token + long-lived refresh token 
stored in PostgreSQL (RevocationHandler alapú visszavonással).
Role-based access control implementation with 4 project roles: 
Owner, Maintainer, Member, Viewer - hierarchikus jogosultság ellenőrzéssel
(Owner >= Maintainer >= Member >= Viewer). Custom AuthorizationHandler 
route-alapú projectId kinyeréssel. 4 policy definiálva:
ProjectOwner, ProjectMaintainer, ProjectMember, ProjectViewer.

**Kihagyott elemek (tudatos döntés)**
- Svelte frontend JWT token handling (localStorage) - 
  frontend inicializálás a CRUD API hét után kerül sorra
- Role nevek eltérnek az eredetitől - általánosabb, 
  iparági standard elnevezések (Owner/Maintainer/Member/Viewer) 
  amelyek bármilyen csapattípusra alkalmazhatók.

## Project & Task CRUD API
RESTful API endpoints for project and task management: creation, reading, updating, and deletion. Task model with priority, status, due date, and assignee. Label and comment CRUD operations. Input validation with FluentValidation. Swagger/OpenAPI documentation for all endpoints.

**Elvégzett munkák**
- API-nkénti DTO-k.
- Kiemelt DTO: AttachmentDto (Shared/) - proxy download pattern előkészítve
- FluentValidation: Project, Task, Label, Comment validátorok
- IProjectService + ProjectService: Create, GetAll, GetById, Update, Archive, Unarchive, Delete
  - CreateProjectAsync: auto board + Backlog/Done oszlop létrehozás
  - ProjectCounter-alapú TaskKey generálás (PM-1, PM-2...)
- ITaskService + TaskService: Create, GetAll, GetById, Update, Move, Delete
  - MoveTaskAsync: maps_to_status invariáns betartása (státusz frissítése move operáció esetén)
  - GetTasksAsync: opcionális boardId/sprintId szűrés, batch aggregáció (későbbi fejlesztésre szorul, lásd lentebb)
- ILabelService + LabelService: CRUD + task hozzárendelés/eltávolítás
- ICommentService + CommentService: CRUD + callerId ellenőrzés (csak saját komment törölhető)
- ProjectController, ProjectTaskController, LabelController, CommentController

**Tudatos döntések**
- ColumnId nem nullable: minden task mindig egy oszlopban van (Backlog is oszlop)
- SprintId nullable: task létrehozáskor opcionális sprint hozzárendelés (lehetőség projekt szervezés egyszerűsítésére)
- Board kötelező oszlopai: Backlog (position: 0) és Done (position: 99) auto-létrehozva
- PrUrl hozzáadva PrLink-hez, CommitUrl hozzáadva CommitLink-hez (webhook előkészítés)
- Attachment.SizeBytes: BigInteger -> long (PostgreSQL bigint kompatibilitás, hiba lehetőségek csökkentése)

**Kihagyott elemek (tudatos döntés)**
- GetTasksAsync: Pagination - jövőbeli fejlesztés, kommentben jelölve (Oka:nagyon sok adatot húzhat be egyetlen kérésben, magas DB Query leterhelés).

## Svelte Frontend Setup & Core Layout
Svelte SPA initialization with Vite. Client-side routing with svelte-spa-router. Core layout components: Navbar, project selector, tabbed navigation (Overview, Board, Team, Recent Activity, Statistics, Manager, Team Resources, Project Settings). API service layer with JWT header injection. Auth store and project store setup.

### Elvégzett munkák

**TypeScript migráció**
- Svelte projekt átállítva TypeScript-re
- tsconfig.json létrehozva strict módban, noEmit (Vite kezeli a buildet)
- Összes fájl .ts kiterjesztésre átnevezve, lang="ts" hozzáadva

**API Service Layer**
- client.ts: axios instance JWT request interceptor + 401 response interceptor
- authApi.ts: loginAsync, registerAsync, refreshAsync, logoutAsync, meAsync, changePasswordAsync, updateProfileAsync
- projectApi.ts: getProjectsAsync, createProjectAsync, updateProjectAsync, archiveProjectAsync, unarchiveProjectAsync, deleteProjectAsync, getProjectByIdAsync

**Stores**
- authStore.ts: token, user, isAuthenticated + login/logout helper függvények
- projectStore.ts: projects, activeProject, labels + setProjects, setActiveProject, setLabels, clearProjects

**Validators (validators.ts)**
- validateEmail, validateDisplayName, validatePassword, validateProjName, validateDescription
- validateColumnName, validateColumnStatus, validateTaskTitle, validateTaskDescription, validateTaskDueDate
- validateBoardName, validateBoardDescription, validateCommentBody
- Újrafelhasználható, minden komponensből importálható

**Oldalak**
- Login.svelte: form, JWT auth, redirect Főoldalra, regisztráció gomb
- Register.svelte: form, validáció (email, displayName, jelszó erősség + megerősítés), redirect Login-ra
- AppLayout.svelte: fő layout, Discord-szerű single-page design, aktív projekt alapján dinamikus tartalom

**Komponensek**
- CreateProjectModal.svelte: projekt létrehozás, validáció, overlay, Escape bezárás
- ConfirmModal.svelte: újrafelhasználható megerősítő modal (title, message, confirmText prop-ok)
- ProjectOverview.svelte: projekt alapadatok megjelenítése
- ProjectSettings.svelte: projekt szerkesztés, archiválás, törlés + Label kezelés
- UserSettingsModal.svelte: profil megtekintés/szerkesztés, jelszó változtatás

**Backend kiegészítések**
- CORS konfiguráció: AllowFrontend policy (localhost:5173)
- UpdateProfileDto, UpdateUserValidator, UserProfileDto
- PATCH /api/auth/profile endpoint + ChangeUserProfileAsync

## UI architektúrával kapcsolatos döntések:

**Layout**
- Discord-szerű single-page layout: bal oldali sidebar + jobb oldali dinamikus tartalom
- Nincs oldalváltás - aktív nézet változóval vezérelt tartalom, vagy modal felugrik
- Route-ok: "/" (Login), "/register" (Register), "/app" (AppLayout)

**Navbar opciók:**
  Overview | Board | Sprints | Team | Git | Statistics | Team Resources | Project Settings

**Nézetek tartalma**
- Overview - projekt alapadatok (név, kulcs, leírás, tulajdonos, dátumok, státusz)
- Board - Kanban tábla, oszlop létrehozás modal-ban ("+ Oszlop hozzáadása")
- Sprints - sprint kezelés + backlog taskok + task létrehozás
- Team - tagok listája/kezelése + Recent Activity feed
- Git - összekapcsolt commitok/PR-ek + manuálisan hozzáköthető össze nem kötött commitok
- Statistics - grafikonok, metrikák
- Team Resources - erőforrás elosztás
- Project Settings - projekt beállítások, archiválás, törlés

**Modal pattern**
- Projekt létrehozás -> CreateProjectModal
- Oszlop létrehozás -> CreateColumnModal (Board fülön)
- Task részletes nézet -> TaskDetailModal (kommentek, commitok, PR-ek, labelek)
- Label hozzáadás taskhoz -> TaskDetailModal-on belül
- stb

## Kanban Board & Task Management (Ez fejezet leírás frissítve lett)
Interactive Kanban board with drag-and-drop functionality (svelte-dnd-action). Board columns with task cards. Task detail modal with comments, labels, (késöbb: commit/PR links, assignees (git és team crud után)).

### TO-DO:
- Overdue task visual indicator

### Elvégzett backend munkák

**Board CRUD**
- BoardResponseDto, CreateBoardDto, UpdateBoardDto + validátorok
- IBoardService + BoardService: GetBoards, CreateBoard, UpdateBoard, DeleteBoard
- CreateBoardAsync: auto-létrehozza a Backlog (pos:0), To Do (pos:1), Done (pos:99) oszlopokat
- IsDefault kezelés: új default board beállításakor a régi automatikusan false-ra vált
- BoardController: GET/POST/PATCH/DELETE /api/projects/{projectId}/boards

**Column CRUD**
- ColumnResponseDto, CreateColumnDto, UpdateColumnDto, ColumnOrderDto + validátorok
- IColumnService + ColumnService: GetColumns, CreateColumn, UpdateColumn, DeleteColumn, OrderColumns
- DeleteColumn: csak üres oszlop törölhető, Backlog oszlop (Position=0) nem törölhető
- OrderColumnsAsync: két fázisú update (-1 dummy érték -> végleges pozíció)
- Backlog oszlop (Position=0) pozíciója nem változtatható reorder során sem
- Position > 0 kötelező Column létrehozáskor és reordernél (Backlog védelem)
- ColumnDefinitionController: GET/POST/PATCH/DELETE + POST reorder

**Sprint CRUD**
- SprintResponseDto, CreateSprintDto, UpdateSprintDto + validátorok
- State management: "Planning" | "Active" | "Completed"
- State csak dedikált endpointokon változtatható
- Sprint.BoardId eltávolítva - sprint több boardhoz is tartozhat
- ISprintService + SprintService:
  - GetSprints, CreateSprint, UpdateSprint, DeleteSprint
  - ActivateSprintAsync: Planning -> Active + taskok az első nem-Backlog oszlopba kerülnek boardonként
  - PlanSprintAsync: Active -> Planning + taskok visszakerülnek a Board Backlog oszlopba
  - CompleteSprintAsync: Active -> Completed
    - Csak akkor engedélyezett ha minden task CompletedAt != null
    - Befejezetlen taskok -> Backlogba (SprintId=null) VAGY következő sprintbe (targetSprintId)
    - ClosedAt beállítása minden sprint taskján lezáráskor
  - GetUnfinishedTasksAsync: sprint lezárás előtti befejezetlen task lista
- AssignTaskToSprintAsync: task hozzárendelése sprinthez
- RemoveTaskFromSprintAsync: task eltávolítása sprintből (Backlogba kerül)
- SprintController: GET/POST/PUT/DELETE + activate/plan/complete/unfinished/tasks endpointok

**Refaktorálás**
- Data Annotations eltávolítva az összes DTO-ból -> csak FluentValidation
- XML dokumentáció hozzáadva a DTO-khoz Swagger UI-hoz
- MoveTaskDtoValidator: NotEmpty() eltávolítva Position-ről (0 érték valid)

**Swagger + FluentValidation integráció**
A Swagger UI jelenleg nem jeleníti meg a FluentValidation szabályokat. (pl.: Name: maxvalue, min., illetve hogy kötelező mezőröl van e szó, stb)
Telepítendő csomag: MicroElements.Swashbuckle.FluentValidation
- Automatikusan beolvassa a validációs szabályokat
- minLength, maxLength, pattern megjelenik a Swagger UI-ban

### Elvégzett frontend munkák

**API Service Layer**
- boardApi.ts, columnApi.ts, taskApi.ts, commentApi.ts, labelApi.ts, sprintApi.ts

**Stores**
- boardStore.ts: boards, activeBoard, columns
- taskStore.ts: tasks, activeTask
- sprintStore.ts: sprints, activeSprint (auto-detektált)

**Komponensek**
- BoardView.svelte: board toolbar, dropdown board választó, oszlop/task hozzáadás, átrendezés lock
- ColumnCard.svelte: oszlop + task dndzone + drag handle
- TaskCard.svelte: task kártya (key, cím, prioritás, határidő, labelek)
- CreateColumnModal.svelte: oszlop létrehozás, pozíció reorder-rel kezelve, Backlog kizárva a pozíció selectorból
- ColumnDetailModal.svelte: oszlop szerkesztés és törlés
- CreateBoardModal.svelte + UpdateBoardModal.svelte: board kezelés
- CreateTaskModal.svelte: isBacklogMode támogatás (boardId/columnId null), Backlog oszlop kizárva az oszlop selectorból
- TaskDetailModal.svelte: két oszlopos layout, szerkesztés/törlés, label kezelés, board név + sprint név megjelenítés, null boardId/columnId kezelés
- CommentSection.svelte: komment kezelés
- LabelCard.svelte + CreateLabelModal.svelte: label kezelés
- SprintsView.svelte: sprint lista (aktív kiemelve, planning/completed collapselhető), fix toolbar + scrollolható tartalom
- SprintCard.svelte: sprint kártya board-csoportosított task megjelenítéssel, default board először, BacklogTaskCard komponens használata
- ProjectBacklog.svelte: projekt szintű backlog (collapselhető), task létrehozás + részletes nézet támogatás
- BacklogTaskCard.svelte: backlog task kártya (sprint/board hozzárendelés hamburger menüből, törlés, detail megnyitás)
- CreateSprintModal.svelte: sprint létrehozás névvel, céllal, dátumokkal
- UpdateSprintModal.svelte: sprint szerkesztés
- CompleteSprintModal.svelte: sprint lezárás, befejezetlen taskok kezelése (Backlog vagy következő sprint)

**Drag & Drop**
- svelte-dnd-action: oszlop átrendezés (lock gombbal) + task mozgatás
- Lexorank string pozíció: localeCompare alapú rendezés
- columnTasks Record: oszloponkénti task kezelés
- isDragging flag: store felülírás megakadályozása

### Sprint & Task Modell Architektúra Döntések
**Task modell refaktorálás**
- Task.Status eltávolítva - computed property: ColumnDefinition?.MapsToStatus ?? "Backlog"
- Task.BoardId nullable: ha null -> Projekt Backlogban van
- Task.ColumnId nullable: ha null -> Projekt Backlogban van
- Task.CompletedAt: amikor a task az utolsó oszlopba ér (MoveTaskAsync állítja be), visszamozgatáskor nullázódik
- Task.ClosedAt: amikor a sprint le lett zárva (CompleteSprintAsync állítja be, csak befejezett taskokon)
- Backlog definíció: BoardId=null AND ColumnId=null AND SprintId=null

**AssignTaskToBoardAsync logika**
- BoardId=null -> Projekt Backlogba (BoardId, ColumnId, CompletedAt nullázva)
- BoardId megadva + nincs sprint -> Board Backlog oszlopba (Position=0)
- BoardId megadva + aktív sprint -> Első valódi oszlopba (Position>0)
- BoardId megadva + nem aktív sprint -> Board Backlog oszlopba
- Board váltáskor CompletedAt mindig nullázódik

**AssignTaskToSprintAsync logika**
- SprintId=null -> Backlogba visszarakás (SprintId null, ColumnId Board Backlogba, CompletedAt null)
- Aktív sprinthez adás + van board -> Első valódi oszlopba kerül
- Nem aktív sprinthez adás -> Pozíció nem változik

**CompleteSprintAsync logika**
- Csak akkor engedélyezett ha minden task CompletedAt != null
- Befejezetlen taskok -> Backlogba (BoardId, ColumnId, SprintId nullázva) VAGY következő sprintbe
- ClosedAt CSAK a befejezett (CompletedAt != null) taskokon kerül beállításra
- Sprint State -> Completed

**Backlog oszlop védelem**
- Position=0 oszlop: nem törölhető, pozíciója nem változtatható
- CreateColumnDto: Position > 0 kötelező
- ColumnOrderDto: Position > 0 kötelező
- BoardView: Backlog oszlop elrejtve (csak Position>0 oszlopok láthatók)
- CreateColumnModal: Backlog kizárva a pozíció selectorból
- CreateTaskModal: Backlog kizárva az oszlop selectorból

**Sprint modell refaktorálás**
- Sprint.BoardId eltávolítva - egy sprint több boardhoz is tartozhat
- Indok: egy projekten belül több board is lehet (pl. Frontend Board + Backend Board)
  és egy sprint mindkét board taskjait tartalmazhatja

**Task életciklus**
Létrehozás -> Projekt Backlog (BoardId=null, ColumnId=null, SprintId=null)
           -> AssignTaskToBoardAsync -> Board Backlog oszlop (Position=0)
           -> AssignTaskToSprintAsync -> SprintId beállítva
           -> ActivateSprintAsync -> Első valódi oszlop (Position>0)
           -> MoveTaskAsync -> Oszlopok között mozog
           -> Utolsó oszlopba ér -> CompletedAt beállítva
           -> CompleteSprintAsync -> ClosedAt beállítva, sprint Completed

**Új endpoint: Sprint-Task hozzárendelés**
- POST /tasks/{taskId}/board -> AssignTaskToBoardAsync (Board Backlog oszlopba)
- POST /sprints/{sprintId}/tasks/{taskId} -> sprinthez adás
- DELETE /sprints/{sprintId}/tasks/{taskId} -> Backlogba visszarakás

**Tervezett Sprint UI**
- Sprint lista időrendben, aktív sprint kiemelve
- Sprint kártyán: név, dátum, cél, státusz, taskok listája
- Backlog szekció: sprint nélküli taskok drag & drop-pal sprinthez adhatók
- Gombos hozzáadás: melyik sprintbe dropdown választóval
- Eltávolítás sprintből: task visszakerül Backlogba
- CompleteSprintModal: befejezetlen taskok -> Backlog vagy következő sprint

### Technikai döntések
- Backlog oszlop: minden boardon fix Position=0, nem törölhető, nem rendezhető
- BoardView: Backlog oszlop elrejtve (Position>0 oszlopok láthatók csak) - TODO
- Label kezelés: Project Settings-ben
- Column törlés: csak üres, nem-Backlog oszlop törölhető
- Sprint aktiválás: taskok automatikusan az első valódi oszlopba kerülnek
- Sprint visszatervezés: taskok visszakerülnek a Board Backlog oszlopba
- Sprint lezárás: csak ha minden task CompletedAt != null


### Task/Column Pozíció Kezelési Módszerek - Döntési Dokumentáció

#### Vizsgált megoldások

**1. Szekvenciális frissítés (1, 2, 3, 4...)**
Minden mozgatásnál az összes utána lévő task pozícióját frissítjük sorozatosan.
- pozitívum: Egyszerű implementáció, könnyen érthető
- pozitívum: Kis projekteknél (< 100 task/oszlop) elfogadható teljesítmény
- negatívum: N darab task mozgatásakor N db UPDATE szükséges
- negatívum: Párhuzamos mozgatásnál könnyen ütközések keletkeznek

**Megjegyzés:** Konzulens tanár visszajelzése alapján ennél a use-case-nél
(projekt menedzsment tool, nem Twitter skála) ez a megoldás is elfogadható
lett volna. Az egyszerűség szempontjából valóban versenyképes alternatíva.

**2. Linked List (nextTaskId mutató)**
Minden tasknak van egy nextTaskId mutatója a következő taskra.
- pozitívum: Közbeszúrás mindig csak 2 sor frissítése
- pozitívum: Sosem fogy el a "hely"
- negatívum: ORDER BY nem működik egyszerűen - rekurzív CTE szükséges (O(n))
- negatívum: Indexeléssel sem oldható meg a sorrendezési probléma
- negatívum: Lánc integritás sérülhet (FK constraint + tranzakció részben megoldja)
- negatívum: Párhuzamos mozgatásnál optimistic locking szükséges
- negatívum: Iparági szinten nem elfogadott megoldás erre a problémára

**3. Float alapú pozíció (első implementált megoldás)**
Közbeszúrásnál a két szomszéd átlaga lesz az új pozíció (pl. 1 és 2 közé -> 1.5).
- pozitívum: Egyszerű implementáció
- pozitívum: ORDER BY egyszerű és gyors
- pozitívum: Közbeszúráshoz csak 1 sor frissítése kell
- negatívum: IEEE 754 float precizitási korlát - sok közbeszúrás után elfogy a hely
- negatívum: Renormalizálás szükséges (manuális trigger)
- negatívum: Párhuzamos ütközésnél nem determinisztikus sorrend

**Megjegyzés:** A Lexorank elvében nagyon hasonló a float megoldáshoz
(mindkettő átlagot számít közbeszúrásnál) - a fő különbség a precizitási
korlát hiánya és az öngyógyító bucket rendszer.

**4. Lexorank (Jira megoldása) - VÁLASZTOTT MEGOLDÁS**
String alapú pozíció Base36 karakterkészlettel, bucket rendszerrel.
Például: "0|a", "0|am", "0|b" - közbeszúráskor: "0|a" és "0|b" közé -> "0|am"

**Bucket rendszer:**
- 3 bucket (0, 1, 2) körkörösen - a prefix jelzi melyik bucketben van az elem
- Ütközés esetén (két azonos pozíció) az egész oszlop átkerül a következő bucketbe
- String hossz > 50 karakter esetén automatikus rebalancing triggerelődik
- Bucket 2 exhaustion után visszaáll bucket 0-ra (öngyógyító, végtelen ciklus)
- Inicializálás "0|i"-vel (Base36 közép) - mindkét irányban egyenlő hely

**Base36 kapacitás:**
- 1 karakter: 36^1 = 36 pozíció
- 2 karakter: 36^2 = 1,296 pozíció
- 3 karakter: 36^3 = 46,656 pozíció
- String csak hosszabbodik, sosem fogy el a hely

- pozitívum: Végtelen közbeszúrás - sosem fogy el a hely
- pozitívum: ORDER BY egyszerű és gyors (localeCompare / ABC sorrend)
- pozitívum: SignalR barát - csak a position stringet kell broadcastolni
- pozitívum: Öngyógyító bucket rendszer - automatikus rebalancing ütközéskor
- pozitívum: Iparági standard (Jira, Linear)
- pozitívum: Adatbázis szinten hatékony index támogatás
- negatívum: Komplexebb implementáció mint a float vagy int megoldás
- negatívum: Párhuzamos dupla mozgatás esetén a task "kicsit más helyre" kerülhet
  (nem adatvesztés, csak nem determinisztikus sorrend - elfogadható tradeoff)

#### Döntés
A **Lexorank** implementálása mellett döntöttünk. Konzulens tanár visszajelzése
alapján az egyszerűbb int/szekvenciális megoldás is elfogadható lett volna
ennél a méretskálánál - azonban a Lexorank mellett szóló érvek:

1. **Skálázhatóság:** Közbeszúrás O(1) - mérettől függetlenül 1 UPDATE
2. **Precizitási korlát hiánya:** A float megoldással ellentétben sosem fogy el a hely
3. **Öngyógyító rendszer:** Automatikus rebalancing, nincs manuális beavatkozás
4. **Iparági standard:** Jira, Linear ugyanezt az algoritmust használja
5. **Szakmai megalapozottság:** A védésen jól indokolható tudatos technikai döntés

A komplexitás növekedés elfogadható tradeoff a fenti előnyökért -
különösen mivel a Redis backplane + SignalR architektúrával együtt
egy production-ready skálázható megoldást alkot.

#### Implementáció
- Position mező típusa: float -> string (migration elvégezve)
- LexorankService: GetInitialPosition, GetMiddle, RebalancePositions, HasCollision
- MoveTaskAsync: AfterTaskId alapú pozicionálás (backend számítja)
- RebalanceColumnAsync: automatikus rebalancing ütközés vagy hossz túllépés esetén
- Frontend: sort by position -> localeCompare alapú rendezés

## SignalR Real-Time Updates & Notifications
Spring break week dedicated to the real-time layer - the most complex cross-cutting feature. SignalR hub implementation on the backend (BoardHub, NotificationHub) for real-time task movement, status changes, and new comments. SignalR client connection manager with automatic reconnection. Nginx WebSocket proxy configuration for the /hubs/* route. In-app notification system: notification bell in navbar, unread count, notification list. SignalR-based real-time notification delivery for task assignment, comments, and sprint changes.

### Elvégzett munkák

**Backend**
- ProjectHub implementáció: JoinProject, LeaveProject, JoinBoard, LeaveBoard metódusok
- JWT WebSocket token kezelés Program.cs-ben (OnMessageReceived event)
- IHubContext<ProjectHub> injection a következő service-ekbe:
  - TaskService, SprintService, CommentService, LabelService, ProjectService
  - ColumnService, BoardService

**Broadcastolt események:**

| Esemény | Trigger | Csoport |
|---------|---------|---------|
| TaskMoved | MoveTaskAsync | board-{boardId} |
| TaskCreated | CreateTaskAsync | project-{projectId} |
| TaskUpdated | UpdateTaskAsync, AssignTaskToBoardAsync, AssignTaskToSprintAsync | project-{projectId} |
| TaskDeleted | DeleteTaskAsync | project-{projectId} |
| TasksRebalanced | RebalanceColumnAsync | board-{boardId} |
| TaskLabelAdded | AddLabelToTaskAsync | project-{projectId} |
| TaskLabelRemoved | RemoveLabelFromTaskAsync | project-{projectId} |
| ColumnCreated | CreateColumnAsync | board-{boardId} |
| ColumnUpdated | UpdateColumnAsync | board-{boardId} |
| ColumnDeleted | DeleteColumnAsync | board-{boardId} |
| ColumnsReordered | OrderColumnsAsync | board-{boardId} |
| BoardCreated | CreateBoardAsync | project-{projectId} |
| BoardUpdated | UpdateBoardAsync | project-{projectId} |
| BoardDeleted | DeleteBoardAsync | project-{projectId} |
| SprintCreated | CreateSprintAsync | project-{projectId} |
| SprintUpdated | ActivateSprintAsync, PlanSprintAsync, CompleteSprintAsync | project-{projectId} |
| SprintDeleted | DeleteSprintAsync | project-{projectId} |
| CommentAdded | CreateCommentAsync | project-{projectId} |
| CommentDeleted | DeleteCommentAsync | project-{projectId} |
| LabelCreated | CreateLabelAsync | project-{projectId} |
| LabelDeleted | DeleteLabelAsync | project-{projectId} |
| ProjectUpdated | UpdateProjectAsync | project-{projectId} |
| ProjectArchived | ArchiveProjectAsync | project-{projectId} |
| ProjectUnarchived | UnarchiveProjectAsync | project-{projectId} |

**Frontend**
- signalRService.ts: connect, disconnect, joinProject, leaveProject, joinBoard, leaveBoard, on, off, getConnectionId
- @microsoft/signalr npm csomag
- AppLayout.svelte: connect on mount, joinProject on active project change
- Komponens szintű eseménykezelők:
  - BoardView.svelte: Task, Column, Board, Sprint, Label események
  - SprintsView.svelte: Sprint, Task, Label események
  - CommentSection.svelte: Comment események
  - AppLayout.svelte: Label, Project események

**Technikai döntések**
- JWT token WebSocket handshake-nél query string-en keresztül kerül átadásra
- Room rendszer: project-{id} és board-{id} csoportok
- Komponens szintű subscription: minden komponens saját maga iratkozik fel/le
- TaskLabelAdded/Removed: ha a TaskDetailModal nyitva van az adott taskra -> kihagyjuk a store frissítést
- LabelIds használata LabelNames helyett a TaskResponseDto-ban (pontosabb azonosítás)
- closedAt szűrés: BoardView csak nem lezárt taskokat jelenít meg

**Ismert limitációk**
- Notification rendszer (in-app értesítési bell) nincs implementálva
- Komponens szintű subscription miatt ha egy nézet nincs megnyitva akkor az esemény nem kerül feldolgozásra

### Tervezett fejlesztések

**Activity Log alapú Notification rendszer**

A jelenlegi komponens szintű SignalR megközelítés megtartása mellett
egy Activity Log rendszer kerül bevezetésre amely:

1. Perzisztálja a projekt eseményeket az adatbázisban
2. Notification bell UI komponenst biztosít az AppLayout-ban
3. Egyetlen SignalR eseményre épül: ActivityCreated

**Működési elv:**
Service metódus fut (pl. CreateTaskAsync)
-> Activity bejegyzés DB-be írva
-> ActivityCreated broadcast -> project-{projectId} csoport
-> AppLayout notification bell frissül
-> Team fül Recent Activity feed frissül

**Tervezett Activity típusok:**
- TaskCreated, TaskUpdated, TaskDeleted
- SprintCreated, SprintActivated, SprintCompleted
- CommentAdded
- LabelCreated
- ProjectUpdated, ProjectArchived
- BoardCreated

**Előnyök a centralizált SignalR megközelítéssel szemben:**
- Perzisztens értesítések (DB-ben tárolva)
- Egyetlen SignalR esemény az AppLayout-ban
- Nem igényel komponens szintű refaktort
- Recent Activity feed és Notification bell ugyanazt az adatot használja
- Olvasott/olvasatlan állapot kezelés lehetséges

**Implementáció a Team fül fejlesztésekor kerül sorra**

### Tervezett SignalR Optimalizáció & Refaktor

#### 1. Redis Backplane integráció
**Backend változtatások:**
- NuGet csomag: Microsoft.AspNetCore.SignalR.StackExchangeRedis
- Program.cs: AddSignalR().AddStackExchangeRedis(...) - egyetlen sor változtatás
- Docker Compose: Redis service hozzáadása

**Frontend változtatások:** Semmi - teljesen transzparens!

---

#### 2. Broadcast payload optimalizáció
**Jelenlegi probléma:**
N user × M event = N×M felesleges DB lekérdezés
(minden SignalR eventnél teljes task lista újralekérése)

**Backend változtatások:**
- TaskCreated broadcast: teljes TaskResponseDto küldése payload-ban
- TaskUpdated broadcast: teljes TaskResponseDto küldése payload-ban
- SprintCreated broadcast: teljes SprintResponseDto küldése
- SprintUpdated broadcast: teljes SprintResponseDto küldése
- Többi event payload-ja már elegendő adatot tartalmaz

**Frontend változtatások:**
TaskCreated:  payload tartalmazza a teljes TaskResponse-t -> store-ba rakjuk API kérés nélkül
TaskUpdated:  payload tartalmazza a teljes TaskResponse-t -> store-ban frissítjük
TaskDeleted:  már optimális (csak taskId kell)
TaskMoved:    már optimális (taskId, columnId, position)
SprintCreated: payload tartalmazza a teljes SprintResponse-t
SprintUpdated: payload tartalmazza a teljes SprintResponse-t

**Kivételek ahol API kérés maradhat:**
- SprintUpdated -> loadAll() maradhat (komplex state változás, taskok is mozognak)
- BoardDeleted -> getBoardsAsync() maradhat (ritka esemény)

---

#### 3. Centralizált SignalR architektúra
**Jelenlegi probléma:**
BoardView   -> saját SignalR feliratkozások
SprintsView -> saját SignalR feliratkozások
AppLayout   -> saját SignalR feliratkozások
-> Duplikált kód, nehéz követni, notification rendszer nehezen megvalósítható

**Tervezett megoldás:**
AppLayout -> ÖSSZES SignalR esemény kezelése
-> Store frissítések
-> Komponensek store-ból olvasnak reaktívan

**Frontend változtatások:**
- signalRStore.ts létrehozása (kapcsolat állapot kezelés)
- AppLayout.svelte: centralizált eseménykezelők
- BoardView.svelte: saját SignalR kód eltávolítása
- SprintsView.svelte: saját SignalR kód eltávolítása  
- CommentSection.svelte: saját SignalR kód eltávolítása

**Ismert kihívások:**
- Komponens belső state frissítése (pl. columnTasks Record, isDragging flag)
- Explicit Svelte reaktivitás kényszerítés szükséges ([...arr], {...obj})
- Drag & Drop közbeni store frissítések kezelése

---

#### 4. Activity Log alapú Notification rendszer
*(Részletesen a Team Management fejezetben)*
- ActivityCreated SignalR event -> notification bell frissítése
- Egyetlen feliratkozás az AppLayout-ban
- Recent Activity feed és Notification bell ugyanazt az adatot használja

---

#### Implementációs sorrend

1. Redis Backplane (deployment fázisban)
2. Broadcast payload optimalizáció (backend + frontend együtt)
3. Centralizált SignalR architektúra refaktor
4. Activity Log + Notification rendszer (Team Management fejezet)

## Sprint Management & Team Management
Sprint lifecycle API: creation, activation, closing. Sprint-to-task assignment. Constraint enforcement (one active sprint per project). Sprint manager interface with sprint status transitions (Open - Active - Closed). 
Team management interface: member list, role display, member invitation (invite link). Permission-based UI rendering based on user roles.

**Megjegyzés:** A Sprint Management frontend és backend implementációja 
a "Kanban Board & Task Management" fejezetben került megvalósításra, 
mivel szorosan kapcsolódik a task életciklushoz és a board kezeléshez.

Az ott elvégzett Sprint munkák összefoglalója:
- Sprint CRUD + státusz kezelés (Planning/Active/Completed)
- Sprint aktiválás/visszatervezés/lezárás logika
- Task életciklus Sprint kontextusban (CompletedAt, ClosedAt)
- Sprint UI: SprintsView, SprintCard, ProjectBacklog, BacklogTaskCard
- CompleteSprintModal, CreateSprintModal, UpdateSprintModal

### Tervezett technikai fejlesztések

#### CurrentUserService - Egységes felhasználó azonosítás

Jelenleg a controllerekben egyedi megoldások vannak a bejelentkezett felhasználó azonosítására.

Tervezett egységes megoldás interface + service class-al.
**Előnyök:**
- Nem kell callerId paramétert átadni Controller -> Service hívásokban
- Service-ekbe közvetlenül injektálható
- Activity Log implementációhoz szükséges
- Egységes megoldás az egész projektben
- Tesztelhető

**Implementáció az Activity Log rendszerrel együtt kerül sorra**

#### ProjectNotArchivedFilter - Archivált projekt védelem

Jelenleg az archivált projektek nem tiltják meg a műveletek elvégzését
- csak a státusz jelzésére szolgál.

Tervezett megoldás - `ProjectNotArchivedFilter` Action Filter:
- Automatikusan ellenőrzi hogy a projekt archivált-e
- Ha archivált -> 403 Forbidden visszaadása
- Alkalmazható controller osztály szinten vagy metódus szinten
- Nem kell minden service metódusba külön ellenőrzés

**Érintett controllerek:**
- ProjectTaskController
- ColumnDefinitionController
- BoardController
- SprintController
- LabelController
- CommentController

**Kivételek (ahol archivált projekten is engedélyezett):**
- GET metódusok - olvasás archivált projekten is megengedett
- UnarchiveProjectAsync - dearchiválás nyilván engedélyezett
- ProjectController GET végpontjai

**Implementáció a Team Management fejlesztésekor kerül sorra**

**Activity Log i18n terv (jövőbeli fejlesztés)**
Jelenleg a Description mező magyarul tartalmazza a leírást.
Jövőbeli fejlesztésként az i18n támogatáshoz:
- Description mező opcionálissá tétele
- Action + Payload alapján frontend fordítás
- Nyelvi fájlok: hu.json, en.json
- Példa: Action="TaskCreated", Payload={"taskKey":"TP-5"}
  -> hu: "{actorName} létrehozta a {taskKey} taskot"
  -> en: "{actorName} created task {taskKey}"

### Elvégzendő ebben a fejezetben:
- Team Management UI: tagok listája, szerepkörök megjelenítése
- Tag meghívás (meghívó link generálás)
- Recent Activity feed (Activity Log rendszer alapjai)
- Jogosultság alapú UI renderelés (szerepkör alapján)

### Elvégzett munkák
**Backend**
- ProjectInvite model + migration (Token, ExpiresAt, MaxUses, UseCount)
- Team DTO-k: ProjectMemberResponseDto, UpdateMemberRoleDto, GenerateInviteLinkDto, InviteLinkResponseDto
- ITeamService + TeamService: GetMembers, RemoveMember, UpdateMemberRole, GenerateInviteLink, JoinProject
- TeamController: GET/DELETE/PATCH members + POST invite
- ProjectJoinController: POST /api/projects/join/{token}
- SignalR broadcasts: MemberAdded, MemberRemoved, MemberRoleUpdated
- ProjectNotArchivedFilter: archivált projekten nem engedélyezett módosítás
- CurrentUserService: egységes JWT identity kinyerés
- AddAssigneeAsync + RemoveAssigneeAsync: task hozzárendelés service + controller
- AssigneeIds refaktor: TaskResponseDto AssigneeNames -> AssigneeIds
- SignalR broadcasts: TaskAssigneeAdded, TaskAssigneeRemoved

- Activity model kiegészítés: Description mező hozzáadása
- AddDescriptionToActivity migration
- ActivityResponseDto: ActorName, EntityType, Action, Description, Payload, CreatedAt
- IActivityService + ActivityService: LogActivityAsync (pagination), GetActivitiesAsync
- ActivityController: GET /api/projects/{projectId}/activities (page, pageSize query paraméterek)
- LogActivityAsync hívások service metódusokban:
  - TaskService: CreateTask, UpdateTask, DeleteTask, AssignTaskToBoard, AssignTaskToSprint, AddAssignee, RemoveAssignee
  - SprintService: CreateSprint, UpdateSprint, ActivateSprint, PlanSprint, CompleteSprint, DeleteSprint
  - CommentService: CreateComment, DeleteComment
  - TeamService: JoinProject, RemoveMember, UpdateMemberRole
  - ProjectService: CreateProject, UpdateProject, ArchiveProject, UnarchiveProject
  - BoardService: CreateBoard, UpdateBoard, DeleteBoard
  - ColumnService: CreateColumn, UpdateColumn, DeleteColumn
- ActivityCreated SignalR broadcast minden LogActivityAsync hívás után
- Minden activity logging try/catch-ben - másodlagos nem kritikus funkció

**Frontend**
- teamApi.ts: getMembersAsync, removeMemberAsync, updateMemberRoleAsync, generateInviteLinkAsync, joinProjectAsync
- teamStore.ts: members, refreshTrigger, setMembers, triggerTeamRefresh, clearTeam
- MemberCard.svelte: tag megjelenítés, szerepkör módosítás, eltávolítás
- InviteModal.svelte: meghívó link generálás MaxUses + ExpiresInDays beállítással
- TeamView.svelte: tagok listája rendezve (Owner -> Admin -> Member -> Viewer)
- InvitePage.svelte: token alapú csatlakozás, pending invite token kezelés
- SignalR: MemberAdded, MemberRemoved, MemberRoleUpdated kezelés AppLayout-ban
- taskApi.ts: AssigneeIds, addAssigneeAsync, removeAssigneeAsync
- TaskDetailModal: assignee view + edit mód
- TaskCard: assignee initials megjelenítés
- BacklogTaskCard: assignee initials + labelek egy sorban
- SignalR: TaskAssigneeAdded/Removed kezelés BoardView + SprintsView-ban
- AppLayout: tagok betöltése projekt váltáskor (teamStore)

- activityApi.ts: ActivityResponse interface, getActivitiesAsync (pagination)
- ActivityFeed.svelte: pagination, real-time SignalR frissítés, relatív idő formázás
- Entity típus ikonok: Task, Sprint, Comment, Board, Column, Member, Project
- Actor név kiemelés: fehér félkövér
- Leírás és idő egy sorban, idő jobbra igazítva
- TeamView: Recent Activity placeholder -> ActivityFeed komponens

### Tervezett jövőbeli fejlesztések
**Invitation kezelés a TeamView-on**
- Létrehozott meghívó linkek listázása
- Link másolás újra
- Meghívó törlés
- Lejárat és használati szám megjelenítése

**Activity Log i18n terv**
Jelenleg a Description mező magyarul tartalmazza a leírást.
Jövőbeli fejlesztésként az i18n támogatáshoz:
- Description mező opcionálissá tétele
- Action + Payload alapján frontend fordítás
- Nyelvi fájlok: hu.json, en.json
- Példa: Action="TaskCreated", Payload={"taskKey":"TP-5"}
  -> hu: "{actorName} létrehozta a {taskKey} taskot"
  -> en: "{actorName} created task {taskKey}"

**Actor szerepkör megjelenítés az Activity Feed-ben**
- ActorRole visszaadása az ActivityResponseDto-ban
- Rang alapú színkiemelés (Owner=narancs, Admin=kék, Member=zöld, Viewer=szürke)
- Jelenleg fehér félkövér kiemelés - szerepkör nem egyértelműen azonosítható csak névből

## Git Webhook Integration & MinIO File Storage
GitHub/GitLab webhook receiver endpoint (POST /api/git/webhook) with secret validation. Commit and pull request event parsing, task matching by identifier pattern (e.g., PM-123). Git activity display on task detail view. MinIO integration via IFileStorageService interface: file upload, download, and deletion. Attachment metadata storage in PostgreSQL, binary files in MinIO. Team Resources page for shared project documents.

### Architekturális döntések

**Environment Variables**
- Érzékeny adatok (JWT secret, MinIO credentials) .env fájlban
- ASP.NET Core AddEnvironmentVariables() + Docker Compose env átadás
- PostgreSQL credentials is environment variable-ökből

**Git Webhook - Per-Integration Secret**
- Projekt + repo specifikus token alapú URL: POST /api/git/webhook/{webhookToken}
- Minden integrációhoz egyedi WebhookToken és WebhookSecret generálódik
- WebhookSecret DB-ben tárolva (per-integration) - nem globális env variable!
- HMAC-SHA256 validáció az integration-specifikus secrettel
- Secret soha nem kerül vissza a frontend felé - csak beállításkor adja meg a user
- Secret reset lehetséges - régi secret érvénytelen, új secret megadása szükséges
- Token regenerálás: régi webhook URL érvénytelen -> GitHub/GitLab-on frissíteni kell
- Ping event kezelés: IsVerified flag beállítása + SignalR broadcast

**WebhookSecret tárolás biztonsági terv:**
Jelenlegi: Plain text DB tárolás (fejlesztési fázis)
Tervezett (production):
1. AES-256 titkosítás + ENCRYPTION_KEY environment variable
- DB-ben titkosított string
- Szerver oldali visszafejtés webhook érkezésekor
- DB breach esetén értéktelen adatok
2. HashiCorp Vault (nagy skálán):
- Self-hosted, Docker-ben futtatható
- Centralizált secret kezelés
- Audit log, automatikus rotation
- Fine-grained access control

**File Storage - Streaming megközelítés**
- Fájl letöltés backend streaminggel (nem presigned URL)
- Biztonsági indok: minden letöltésnél auth ellenőrzés történik
- Tag eltávolítás után azonnal elvész a hozzáférés
- IFileStorageService interface mögé bújtatva -> könnyen cserélhető

**Skálázhatóság - Monolith first**
- Jelenlegi: Monolith + IFileStorageService interface
- Jövőbeli optimalizáció szükség esetén:
  - File Service kiszervezése külön microservice-be
  - Redis backplane SignalR-hez
  - Kubernetes ha szükséges
- Performance tesztelés után döntés a kiszervezésről

**Integration modell**
- Egy projekthez több integráció is tartozhat (több repo, több provider)
- Provider: GitHub | GitLab
- Ismeretlen token -> request ignorálva
- IsEnabled flag -> integráció bármikor letiltható
- IsVerified flag -> GitHub ping event után true

### Elvégzett munkák
#### Backend

**File Storage:**

IFileStorageService - fájltárolás absztrakt interfész:
- `UploadFileAsync` - fájl feltöltése MinIO-ra
- `GetFileStreamAsync` - fájl streamelése letöltéshez
- `DeleteFileAsync` - fájl törlése MinIO-ról
- `GenerateStorageKey` - egyedi tárolási kulcs generálása (projectId/taskId/uuid_filename)

MinIOFileStorageService - MinIO implementáció:
- Bucket automatikus létrehozás ha nem létezik
- StorageKey alapú objektum kezelés
- Environment variable-ökből konfiguráció (MINIO_ENDPOINT, MINIO_ACCESS_KEY stb.)

IAttachmentService / AttachmentService - attachment kezelés:
- `UploadTaskAttachmentAsync` - task szintű fájl feltöltés
- `UploadProjectAttachmentAsync` - projekt szintű fájl feltöltés (Team Resources)
- `GetTaskAttachmentsAsync` - task attachmentjeinek lekérése
- `GetProjectAttachmentsAsync` - projekt szintű attachmentek lekérése
- `DownloadAttachmentAsync` - fájl stream visszaadása letöltéshez
- `DeleteAttachmentAsync` - attachment törlése DB-ből és MinIO-ról
- `GetAttachmentType` - content type alapján típus meghatározása (image/pdf/spreadsheet/document/other)

TaskAttachmentController - task szintű attachment endpointok:
- `GET /api/projects/{projectId}/tasks/{taskId}/attachments`
- `POST /api/projects/{projectId}/tasks/{taskId}/attachments`
- `GET /api/projects/{projectId}/tasks/{taskId}/attachments/{attachmentId}/download`
- `DELETE /api/projects/{projectId}/tasks/{taskId}/attachments/{attachmentId}`

ProjectAttachmentController - projekt szintű attachment endpointok (Team Resources):
- `GET /api/projects/{projectId}/attachments`
- `POST /api/projects/{projectId}/attachments`
- `GET /api/projects/{projectId}/attachments/{attachmentId}/download`
- `DELETE /api/projects/{projectId}/attachments/{attachmentId}`

---

**Git Integration:**

Integration modell - Git integráció tárolása:
- `Provider` - GitHub vagy GitLab
- `RepoFullName` - owner/repo formátum
- `WebhookToken` - egyedi URL token (publikus endpoint azonosításához)
- `WebhookSecret` - HMAC-SHA256 validációhoz (per-integration, plain text jelenleg)
- `IsVerified` - ping event után true
- `IsEnabled` - integráció on/off kapcsoló
- `AccessToken` - opcionális, jövőbeli API hívásokhoz

CommitLink modell - commit <=> task kapcsolat:
- `TaskId` - nullable (null = unmatched)
- `IntegrationId` - melyik integrációból érkezett
- `CommitSha` - commit hash
- `CommitUrl` - GitHub/GitLab commit URL
- `Message` - commit üzenet
- `AuthorName`, `AuthorEmail` - commit szerzője
- `CommittedAt` - commit időpontja

PrLink modell - PR <=> task kapcsolat:
- `TaskId` - nullable (null = unmatched)
- `IntegrationId` - melyik integrációból érkezett
- `PrNumber` - PR sorszáma
- `PrUrl` - GitHub/GitLab PR URL
- `Title` - PR cím (frissül edited eventnél)
- `State` - open/closed/merged
- `AuthorName` - PR szerzője
- `MergedAt` - merge időpontja (nullable)

IIntegrationService / IntegrationService - integráció kezelés:
- `GetIntegrationsAsync` - projekt integrációinak lekérése
- `CreateIntegrationAsync` - új integráció létrehozása WebhookToken generálással
- `DeleteIntegrationAsync` - integráció törlése
- `RegenerateWebhookTokenAsync` - új token generálása (régi URL érvénytelen lesz)
- `GetByWebhookTokenAsync` - token alapján integráció keresése (webhook fogadáshoz)
- `EnableDisableIntegrationAsync` - integráció engedélyezése/letiltása
- `VerifyIntegrationAsync` - IsVerified = true beállítása ping event után
- `ResetWebhookSecretAsync` - új secret beállítása (IsVerified visszaáll false-ra)

IntegrationController - integráció endpointok:
- `GET /api/projects/{projectId}/integrations`
- `POST /api/projects/{projectId}/integrations`
- `DELETE /api/projects/{projectId}/integrations/{integrationId}`
- `POST /api/projects/{projectId}/integrations/{integrationId}/regenerate`
- `PATCH /api/projects/{projectId}/integrations/{integrationId}/toggle`
- `POST /api/projects/{projectId}/integrations/{integrationId}/reset-secret`

IGitWebhookService / GitWebhookService - webhook feldolgozás:
- `ValidateGitHubSignature` - HMAC-SHA256 validáció integration-specifikus secrettel
- `ValidateGitLabSignature` - GitLab token validáció
- `ProcessPushEventAsync` - push event feldolgozása:
  - Commitok iterálása
  - Task matching regex alapján
  - CommitLink létrehozás (matched TaskId-val, unmatched null TaskId-val)
  - Force push kezelés (meglévő SHA frissítése)
  - Duplicate check (unique constraint védelem)
  - Activity log + SignalR broadcast
- `ProcessPullRequestEventAsync` - PR event feldolgozása:
  - opened/closed/merged/reopened/edited action kezelés
  - Létező PR frissítése (state, mergedAt, title)
  - Új PR task matching + PrLink létrehozás
  - Activity log + SignalR broadcast
- `MatchTasksAsync` - task kulcs keresés regex-szel:
  - Pattern: `/(?:^|[\s\[(\#])({projKey}-\d+)(?:$|[\s\])\.,!])/gi`
  - PM-123, #PM-123, [PM-123], (PM-123) mind felismeri

WebhookController - publikus webhook fogadó endpoint:
- `POST /api/git/webhook/{webhookToken}` - nincs JWT auth
- Token alapján integráció azonosítás
- Provider alapján signature validáció
- GitHub: `X-GitHub-Event` header alapján event routing
- GitLab: `X-Gitlab-Event` header alapján event routing
- Ping event -> `VerifyIntegrationAsync`
- Push event -> `ProcessPushEventAsync`
- PR/MR event -> `ProcessPullRequestEventAsync`

IGitService / GitService - unmatched kezelés és manuális hozzárendelés:
- `GetUnmatchedCommitsAsync` - TaskId=null commitok lekérése projekt alapján
- `GetUnmatchedPrsAsync` - TaskId=null PR-ok lekérése projekt alapján
- `AssignCommitToTaskAsync` - commit manuális hozzárendelése taskhoz
- `AssignPrToTaskAsync` - PR manuális hozzárendelése taskhoz

GitController - git kezelő endpointok:
- `GET /api/projects/{projectId}/git/unmatched-commits`
- `GET /api/projects/{projectId}/git/unmatched-prs`
- `POST /api/projects/{projectId}/git/commits/{commitId}/assign/{taskId}`
- `POST /api/projects/{projectId}/git/prs/{prId}/assign/{taskId}`

**Activity Log kiegészítések:**
- `Activity.ActorId` nullable - system eventeknél null
- `LogSystemActivityAsync` - actor nélküli activity logolás (GitHub/GitLab eventekhez)
- `GetActivitiesAsync` - null ActorId esetén "System" ActorName

**SignalR broadcasts:**
- `CommitLinked` - commit taskhoz kapcsolva
- `PrLinked` - PR taskhoz kapcsolva
- `IntegrationCreated` - új integráció létrehozva
- `IntegrationDeleted` - integráció törölve
- `IntegrationVerified` - ping event után verified
- `IntegrationUpdated` - token regenerálás / enable/disable / secret reset

**MinIO bucket struktúra:**
  project-manager/
  attachments/
  {projectId}/
  tasks/
    {taskId}/
      {fileId}{fileName}
  shared/
  {fileId}{fileName}  <- Team Resources

#### Frontend

**File Storage:**
- `attachmentApi.ts` - upload, download, delete task és projekt szintű attachmentekhez
- `AttachmentCard.svelte` - egy attachment megjelenítése (ikon, fájlnév, méret, feltöltő, letöltés, törlés)
- `TaskDetailModal` - Attachment szekció (feltöltés + AttachmentCard lista)
- `TeamResources.svelte` - projekt szintű dokumentumok + task attachmentek szekciókra bontva

**Git Integration:**
- `integrationApi.ts` - CRUD, regenerate, toggle, reset-secret endpointok
- `integrationStore.ts` - integrációk store (setIntegrations, updateIntegration, addIntegration, removeIntegration, clearIntegrations)
- `gitApi.ts` - unmatched commitok/PR-ok lekérése, manuális task hozzárendelés
- `CreateIntegrationModal.svelte` - provider select (GitHub/GitLab), repoFullName, webhookSecret input
- `IntegrationCard.svelte` - webhook URL másolás, beállítási útmutató, toggle, token regenerálás, secret reset, törlés, verified/unverified badge
- `CommitCard.svelte` - egy commit megjelenítése (SHA, üzenet, szerző, dátum, link)
- `PrCard.svelte` - egy PR megjelenítése (szám, cím, state badge, szerző, dátum, merge dátum, link)
- `GitView.svelte` - Git nézet: integráció státuszok, unmatched commitok és PR-ok manuális task hozzárendeléssel
- `TaskDetailModal` - Git szekció (CommitCard + PrCard lista)
- `ProjectSettings.svelte` - Git Integration szekció
- `AppLayout` - IntegrationCreated/Deleted/Verified/Updated SignalR kezelés, loadIntegrations projekt váltáskor

**Git Webhook Routing & Tesztelés**

*Routing:*
- Publikus endpoint: POST /api/git/webhook/{webhookToken} - nincs JWT auth
- Token alapú biztonság: csak érvényes, engedélyezett integration token fogadható el
- Elkülönített controller (WebhookController) - nem tartozik a projekt RBAC rendszeréhez
- ProjectNotArchivedFilter nem szükséges - webhook fogadás archivált projekten is működhet

*Lokális tesztelés ngrok-kal:*
- GitHub/GitLab nem éri el a localhost-ot -> ngrok tunnel szükséges
- `ngrok http 5178` -> ideiglenes publikus URL generálódik
- `API_BASE_URL` environment variable-be kell az ngrok URL
- Backend újraindítás szükséges az új URL felvételéhez
- Ingyenes ngrok fiók: minden indításkor új URL -> `API_BASE_URL` frissítendő
- Production-ban fix URL lesz -> ngrok nem szükséges

*Webhook verifikáció flow:*
- Integration létrehozás -> WebhookToken + WebhookSecret generálva
- User beállítja GitHub-on: Payload URL + Secret
- GitHub küld ping ->
- Backend validálja HMAC-SHA256 signature-t
- Sikeres -> IsVerified = true -> SignalR IntegrationVerified broadcast
- IntegrationCard-on Verified badge megjelenik

---

### Jövőbeli fejlesztések

**Biztonsági fejlesztések:**
- AES-256 titkosítás WebhookSecret tárolásához
  - `ENCRYPTION_KEY` environment variable
  - `AesEncryptionService` helper class
  - Visszafejtés webhook érkezésekor
- HashiCorp Vault integráció (production skálán)
- TOTP 2FA:
  - Bejelentkezéskor opcionális
  - Kritikus műveleteknél (secret reset, integráció törlés) kötelező
  - Google Authenticator kompatibilis (`Otp.NET` NuGet)

**Git fejlesztések:**
- GitLab webhook tesztelés
- PR státusz szinkronizálás AccessToken alapján (opcionális)
- Task matching kiterjesztése PR body-ra is
- Webhook edge case-ek:
  - PR title változtatás body-ban is keresés
  - Large push (50+ commit) batch optimalizáció
  - PR review events kezelése

## Statistics Dashboard & ECharts Integration
Statistics view with ECharts visualizations: task status distribution (pie chart), sprint burndown and burnup charts (line chart), team workload distribution (bar chart), sprint velocity over time, cumulative flow diagram (stacked area chart). Backend reporting endpoints using PostgreSQL views for efficient aggregation. Filterable by sprint, user, and date range.

## Statistics Dashboard & ECharts Integration

Statistics view with ECharts visualizations: task status distribution (pie chart), sprint burndown and burnup charts (line chart), team workload distribution (bar chart), sprint velocity over time, cumulative flow diagram (stacked area chart). Backend reporting endpoints using EF Core ORM queries. Filterable by sprint and date range.

### Elvégzett munkák

#### Backend

**TaskStatusHistory tábla — CFD alapja:**
- `TaskId`, `ColumnId` (nullable = Backlog), `Status`, `CreatedAt`
- `MoveTaskAsync`-ban minden oszlopváltásnál bejegyzés létrehozva
- History bejegyzések NEM törlődnek sprint lezáráskor — visszamenőleges statisztika megőrzése
- `ClosedAt` alapú védelem megakadályozza az újabb bejegyzések keletkezését lezárt taskoknál
- Migration: `AddTaskStatusHistory`

**ClosedAt alapú task védelem:**
- `MoveTaskAsync`: `ClosedAt != null` -> exception — lezárt sprint taskja nem mozgatható
- Konzisztens history megőrzés — lezárt sprint után nem keletkezhet új bejegyzés

**TaskMoved Activity Log:**
- Csak az utolsó oszlopba kerüléskor logolunk (`Action: "Completed"`)
- Közbülső mozgatások nem logolódnak — túl frequent esemény
- `isLastColumn` flag alapján döntés a `SaveChangesAsync` után

**IStatisticsService / StatisticsService:**

- `GetTaskStatusDistributionAsync(projectId, sprintId?)` 
  -> Task státusz eloszlás GroupBy MapsToStatus alapján
  -> Opcionális sprint szűrés

- `GetBurndownAsync(projectId, sprintId)`
  -> Naponta: RemainingTasks, TotalTasks, CompletedTasks
  -> Sprint.StartDate -> EndDate intervallum
  -> CompletedAt alapján számított napi előrehaladás
  -> Burndown és Burnup is ugyanebből az adatból frontend oldalon

- `GetWorkloadAsync(projectId, sprintId?)`
  -> Ki hány aktív (ClosedAt=null) taskot kezel
  -> TaskAssignment + User GroupBy
  -> Opcionális sprint szűrés

- `GetVelocityAsync(projectId)`
  -> Befejezett sprintek velocity adatai
  -> CompletedTasks count sprintenként
  -> Átlag velocity számítás frontend oldalon

- `GetCumulativeFlowAsync(projectId, dateFrom, dateTo)`
  -> Naponta státuszonként hány task volt adott állapotban
  -> TaskStatusHistory alapján
  -> Minden taskra az adott napig érvényes utolsó státusz

**StatisticsController:**
- `GET /api/projects/{projectId}/statistics/task-status?sprintId=`
- `GET /api/projects/{projectId}/statistics/burndown?sprintId=`
- `GET /api/projects/{projectId}/statistics/workload?sprintId=`
- `GET /api/projects/{projectId}/statistics/velocity`
- `GET /api/projects/{projectId}/statistics/cumulative-flow?dateFrom=&dateTo=`

**Döntések:**
- Burnup eltávolítva mint külön endpoint — ugyanaz az adat mint burndown, frontend kezeli
- DateTime UTC konverzió szükséges a CFD és burndown endpointoknál (PostgreSQL timestamp with time zone)
- Sprint + projekt szintű szűrés opcionális `sprintId` query paraméterrel

---

#### Frontend

**Telepített csomag:** `echarts`

**statisticsApi.ts:**
- `getTaskStatusDistributionAsync(projectId, sprintId?)`
- `getBurndownAsync(projectId, sprintId)`
- `getWorkloadAsync(projectId, sprintId?)`
- `getVelocityAsync(projectId)`
- `getCumulativeFlowAsync(projectId, dateFrom, dateTo)`

**Chart komponensek:**
- `TaskStatusPieChart.svelte` — donut chart, legend jobb oldalon vertikálisan, hash alapú szín generálás ismeretlen státuszokhoz
- `SprintBurndownChart.svelte` — burndown/burnup mód toggle, ideális vonal, ResizeObserver
- `TeamWorkloadChart.svelte` — gradient bar chart, felső label
- `VelocityChart.svelte` — gradient bar chart, átlag velocity vonal
- `CumulativeFlowChart.svelte` — stacked area chart, hash alapú szín generálás

**StatisticsView.svelte:**
- Sprint szűrő (task-status, burndown, workload)
- Dátum intervallum szűrő (CFD)
- Burndown/Burnup mód toggle
- Loading state minden chart-hoz külön
- Empty state kezelés (pl. "Válassz sprintet a burndown megjelenítéséhez")
- 2 oszlopos layout (pie + workload), teljes szélességű sorok (burndown, velocity, CFD)

**Technikai döntések:**
- Store nem szükséges — statisztikák csak StatisticsView-ban használtak
- ResizeObserver minden chart komponensben — reszponzív átméretezés
- `echarts.dispose()` onDestroy-ban — memory leak megelőzés
- Hash alapú szín generálás: ismeretlen státuszokhoz

### Jövőbeli fejlesztési lehetőség:
- Exportálás (PDF/PNG) ECharts beépített funkcióval

### Lexorank Hibás logika javítása! (sajnos hibás logika lett bemutatva az MVP előadáson)
#### A probléma
A `GetBetween` metódus long alapú számítással dolgozott, ami két helyen is hibás viselkedést okozott:
- Ha két szomszédos pozíció közé kellett beszúrni (pl. 0|a és 0|b), a különbség 1 volt, így nem fért be közbülső érték – ilyenkor a kód egyszerűen hozzáfűzte a `MIDDLE_CHAR`-t a stringhez (aa, aaa, stb.), ahelyett hogy numerikusan interpolált volna
- A long típus ~12 karakter hosszú pozíciónál túlcsordult (`36^12 ≈ long.MaxValue`), miközben a `MAX_POSITION_LENGTH = 50` volt beállítva

#### Javítások – LexorankService

**`GetBetween` – teljes újraírás**
String hozzáfűzés helyett hossz-növeléses BigInteger interpoláció: ha az aktuális hosszon nincs hely két érték között, a metódus eggyel hosszabb stringként próbálkozik, egészen `MAX_POSITION_LENGTH`-ig. Ha még így sem talál helyet, `InvalidOperationException`-t dob (rebalancing szükséges).
// Példa: 0|a és 0|b között
// Régi logika: 0|ai  (string hozzáfűzés)
// Új logika:   0|ai  (len=2: a0=360, b0=396, mid=378 -> "ai")

**`long` -> `BigInteger`**
A `ToBase10` / `ToBase36` metódusok ki lettek cserélve `ToBigInt` / `ToBigBase36` metódusokra, amelyek BigInteger-t használnak – így tetszőleges pozíció hosszon túlcsordulás nélkül működnek.

**`RebalancePositions` – javítás**
Az előző implementáció `step = 36 / (count+1)` képlettel dolgozott, ami 36+ task esetén duplikált pozíciókat generált. Az új verzió fix 6 karakteres (`REBALANCE_POSITION_LENGTH = 6`) pozíciókat generál `BigInteger`-alapú lépésszámítással (`36^6 ≈ 2 milliárd slot`).

**Bucket öröklés**
A `GetInitialPosition` és `GetMiddle` metódusok mostantól a meglévő pozíciók bucketjét öröklik át az explicit paraméter helyett, így a bucket rotáció konzisztensen megmarad mozgatáskor.

**`ToBigInt` – defenszív kisbetűsítés**
A Base36 karakter keresés előtt `.ToLower()` hívás történik, hogy nagybetűs karakter esetén se térjen vissza `-1` az IndexOf.

#### Javítások – `MoveTaskAsync`

**`try/catch` a pozíció számítás köré**
Extrém edge case-ben (ha `GetBetween` mégis `InvalidOperationException`-t dobna) a metódus automatikusan rebalance-ol és újra megpróbálja a pozíció számítást, így a felhasználó felé soha nem kerül ki a kivétel.

#### Javítások – `RebalanceColumnAsync`
**`Count == 0` early return**
Ha az oszlop üres, a metódus azonnal visszatér `SaveChanges` és SignalR hívás nélkül.

---

## Search/Filter, UI Polish & Responsive Design
Task search and filtering on the board (by assignee, priority, keyword, label). Activity log view with filtering by user and event type. Responsive UI refinements for desktop and notebook resolutions. Consistent spacing, color scheme, and typography across all views. Dark/light mode toggle in profile settings. Overview dashboard with personal task summary, overdue items, and recent activity.

### Elvégzett munkák

#### Backend

**Activity Log szűrés:**
- `GetActivitiesAsync` kiegészítve opcionális szűrő paraméterekkel: `entityType`, `actorName`, `dateFrom`, `dateTo`
- `IActivityService` interfész frissítve
- `ActivityController` frissítve query paraméterekkel
- DateTime UTC konverzió a dátum szűrőknél

---

#### Frontend

**Task Search & Filter (BoardView):**
- Kulcsszó keresés (title + taskKey alapján, real-time)
- Assignee szűrő (teamStore alapján)
- Prioritás szűrő (low/medium/high/critical)
- Label szűrő (projectStore.labels alapján)
- Határidő szűrő (lejárt / hamarosan lejár)
- Szűrők törlése gomb (csak aktív szűrő esetén látszik)
- Frontend only — nincs új backend endpoint
- Reaktív `filteredTasks` számítás AND logikával
- `distributeTasks(filteredTasks)` reaktív blokk

**Overdue / Due Soon / Completed vizuális jelzés:**
- `isOverdue` — piros border-left
- `isDueSoon` — sárga border-left
- `isCompleted` — zöld border-left + ikon
- Implementálva: `TaskCard.svelte`, `BacklogTaskCard.svelte`
- Dátum megjelenítés: dátum + óra:perc formátum

**Activity Log szűrés (ActivityFeed.svelte):**
- Felhasználó szűrő (actorName alapján)
- Entitás típus szűrő (Task/Sprint/Comment/Board/Column/Member/Project/Commit/PullRequest/Integration)
- Dátum intervallum szűrő (dateFrom/dateTo)
- Mai nap szűrő gomb toggle-lel
- Szűrők törlése gomb (isTodayFilter reset)
- Relatív időmegjelenítés pontos idővel (pl. "2 órája (14:35)")
- Régi bejegyzéseknél teljes dátum + idő

**Overview Dashboard (ProjectOverview.svelte):**
- Üdvözlő fejléc (displayName, projekt státusz, meta infók)
- Összefoglaló statisztika kártyák (OverviewStatCard.svelte):
  - Összes/kész/folyamatban taskok száma
  - Lejárt taskok száma
  - Aktív sprint progress
- Hozzám rendelt taskok lista (BacklogTaskCard showMenu=false)
  - Rendezés: overdue -> due-soon -> normal -> kész -> ABC
  - Scrollolható szekció
- Recent Activity szekció (ActivityFeed komponens újrafelhasználás)
  - Scrollolható szekció
- SignalR: TaskMoved, TaskCreated, TaskUpdated, TaskDeleted, SprintUpdated events
- Frontend only — meglévő store-ok alapján

**UI Strukturális változások:**
- `ProjectSettings.svelte` — füles navigáció: Általános / Labelek / Git, veszélyzóna szekció
- `TaskDetailModal.svelte` — füles navigáció: Részletek / Csatolmányok / Git / Kommentek, scrollolható szerkesztő mód, min-height az összeugrás ellen
- `UserSettingsModal.svelte` — témaváltó gomb sidebar aljára, aktív nézet kiemelése
- `SprintsView.svelte` / `ProjectBacklog.svelte` — összecsukható szekciók ChevronDown/ChevronRight ikonnal

**AppLayout megújítás:**
- Lucide ikonok a sidebar és topbar navigációban
- Összecsukható sidebar collapse gombbal
- Aktív nézet és aktív projekt kiemelése
- Téma toggle gomb (Sun/Moon) a sidebarban
- CSS variables használata hardcoded színek helyett
- Reszponzív sidebar: 220px -> 180px (1366px) -> 60px (768px)

**Theme System:**
- `themeStore.ts` — dark/light toggle, localStorage mentés
- `global.css` — CSS variables dark/light témához
- `App.svelte` — theme class alkalmazása `<html>` tagre
- `UserSettingsModal` — Megjelenés szekció dark/light toggle gombokkal
- `cssVars.ts` — utility helper ECharts CSS variable olvasáshoz
- ECharts komponensek: `getChartColors()` + `themeStore` reaktivitás témaváltáskor

**Responsive Design alapozás:**
- CSS variables breakpointonként (1366px, 768px, 480px):
  - `--sidebar-width`, `--topbar-height`, `--content-padding`
  - `--font-size-base`, `--font-size-sm`, `--font-size-xs`
  - `--modal-width`, `--modal-width-lg`
  - `--gap-sm`, `--gap-md`, `--gap-lg`
- Helper classok: `hide-mobile`, `show-mobile-only`, `hide-tablet`, `stack-mobile`, `full-width-mobile`

**UI Polish alapozás (global.css):**
- Egységes focus state-ek (`outline: 2px solid var(--accent-blue)`)
- Input/select/textarea focus box-shadow
- Smooth transitions buttonokon és form elemekon
- `.empty-state` és `.loading-state` helper classok
- Scrollbar stílus
- Alap tipográfia (h1-h4)

**Technikai döntések:**
- Szűrések frontend only ahol lehetséges (BoardView) — nincs extra backend load
- Reaktív `distributeTasks` blokk: `$: distributeTasks(filteredTasks)`
- `cssVars.ts`: ECharts nem fér hozzá CSS variable-okhoz direkten (mert az echarts options-ön keresztüli stílust használ) -> DOM API-n keresztül olvassuk
- Reszponzív CSS variables: csak `global.css` módosítás szükséges, később a teljes reszponzív designhoz
- Fokozatos UI megújítás: `global.css`-jelenleg mindenhol, de a responzibilitást megvalósítás még nincs kész.

### Jövőbeli fejlesztések

**UI Polish (következő iteráció):**
- `.empty-state` és `.loading-state` classok alkalmazása az összes komponensben
- Tablet/mobile layout finomítás (csak alapozás van)
- Focus state-ek tesztelése accessibility szempontból
- Transition animációk finomítása

**Funkcionális fejlesztések:**
- Activity Log: lapozás helyett infinite scroll
- BoardView: keresési eredmények számlálója

## Testing, Bug Fixes & Final Integration
(Régi leírás:)
Unit tests for relevant API endpoints and service layers (xUnit). Integration tests for database operations. End-to-end testing of critical user flows (task creation, board interaction, sprint management). Bug fixing and edge case handling. Full system integration testing across all components (frontend, backend, SignalR, MinIO, PostgreSQL). Performance review and query optimization where needed.
(Új átgondolt leírás:)
Unit tests for critical service layer components (xUnit). Bug fixes for known issues. Documentation of known limitations and planned improvements. Manual integration testing of critical user flows.

### Tervezett implementáció

#### Unit Tesztek (xUnit)

**LexorankService tesztek — KRITIKUS:**
Tesztelendő metódusok:

- GetMiddle: bucket öröklés prevPosition-ből
- GetMiddle: bucket öröklés nextPosition-ből
- GetMiddle: null prev és next esetén
- GetMiddle: csak prev esetén (increment)
- GetMiddle: csak next esetén (getbefore)
- GetBetween: pozíció közé kerülés
- GetBetween: hossz növelés ha nincs hely
- GetBetween: InvalidOperationException ha kimerül
- GetInitialPosition: bucket öröklés lastPosition-ből
- GetInitialPosition: null lastPosition esetén
- RebalancePositions: egyenletes elosztás
- RebalancePositions: helyes bucket használat
- NeedsRebalancing: hosszú pozíció esetén true
- HasCollision: egyező pozíciók esetén true

Elkészült (28 teszt):
- GetInitialPosition: null input, increment, bucket öröklés
- GetMiddle: null prev/next, két pozíció közé kerülés, bucket öröklés, közeli pozíciók hossznövelése
- GetBetween: legrosszabb eset (mindig ugyanoda szúr be 50-szer), soha nem dob kivételt
- NeedsRebalancing: rövid pozíció false, hosszú pozíció true, határesetek (50/51 karakter)
- HasCollision: egyező és különböző pozíciók
- RebalancePositions: helyes darabszám, rendezettség, helyes bucket, 100 task egyediség
- GetBucket / GetNextBucket: helyes értékek, wrap around

**Validátor tesztek:**
**CreateTaskDtoValidator:**

- Title: üres, túl hosszú
- Priority: érvényes/érvénytelen értékek
- EstimateInMinutes: negatív érték

**CreateProjectDtoValidator:**

- Name: üres, túl hosszú
- ProjKey: kisbetű, speciális karakter, túl rövid/hosszú

**CreateIntegrationDtoValidator:**

- Provider: érvénytelen provider
- RepoFullName: helytelen formátum
- WebhookSecret: túl rövid

Elkészült:
**CreateProjectDtoValidator tesztek:**
- Name: üres, túl hosszú, valid, pontosan 120 karakter
- ProjKey: üres, túl rövid/hosszú, kisbetű, speciális karakter, szóköz, valid esetek
- Description: túl hosszú, null, pontosan 1000 karakter

**CreateIntegrationDtoValidator tesztek:**
- Provider: üres, érvénytelen, GitHub, GitLab
- RepoFullName: üres, slash nélkül, csak slash, valid, kötőjel és pont
- WebhookSecret: üres, túl rövid, pontosan 15/16 karakter határeset, valid

**CreateSprintDtoValidator tesztek:**
- Name: üres, túl hosszú, valid
- Dátum logika: EndDate < StartDate, EndDate == StartDate, EndDate > StartDate
- Null dátumok kezelése
- Goal: túl hosszú, null, pontosan 500 karakter

#### Ismert Bugfixek (Észlelt és javított)

**1. LexorankService BigInteger refactor:**
- long -> BigInteger alapú számítás
- GetMiddle és GetInitialPosition bucket öröklés fix
- MoveTaskAsync try/catch InvalidOperationException fallback
- RebalanceColumnAsync Count == 0 early return

**2. ProjKey uniqueness fix:**
- Unique constraint eltávolítva
- CreateProjectAsync service szintű check eltávolítva

**3. TaskMoved completedAt fix:**
- completedAt hozzáadva board és projekt szintű broadcasthoz
- BoardView TaskMoved handler: null completedAt helyesen törli az értéket
- BoardView és SprintsView handler frissítve

**4. CreateTaskModal alapértelmezett oszlop fix:**
- Hiányzó alapértelmezett érték beállítva reaktív blokkal
- Első oszlop automatikusan kiválasztva modal megnyitáskor

**5. Login redirect bug:**
- Duplicate push('/app') eltávolítva
- Pending invite token kezelés try blokkba helyezve
- Sikertelen bejelentkezés nem irányít át

**6. TeamView végtelen loop fix:**
- lastRefreshTrigger változó hozzáadva
- loadMembers csak refreshTrigger értékváltozáskor fut
- Recursive loop megszüntetve: loadMembers -> setMembers -> subscribe -> loadMembers

**7. CompletedAt beállítás task létrehozáskor:**
- Ha task közvetlenül az utolsó oszlopba kerül létrehozáskor
- CompletedAt = DateTime.UtcNow beállítva
- Konzisztens MoveTaskAsync viselkedéssel

**8. Archivált projekt jelzés:**
- Sárga banner megjelenítése archivált projektnél a topbar alatt
- Egyértelmű visszajelzés hogy csak olvasható hozzáférés

**9. Projekt váltáskor automatikus Overview megnyitás:**
- activeView = 'overview' beállítva projekt váltáskor
- Overview automatikusan betölti az adott projekt adatait

#### Optimalizálások

**N+1 query javítások:**
- GetVelocityAsync: foreach -> egyetlen LINQ projekció
- GetTaskStatusDistributionAsync: in-memory GroupBy -> DB szintű GroupBy
- GetCumulativeFlowAsync: Include -> Select projekció, előre csoportosított history
- GetUnmatchedCommitsAsync / GetUnmatchedPrsAsync: két lekérdezés -> JOIN

**ColumnDefinition Soft Delete:**
- IsDeleted és DeletedAt mezők hozzáadva
- DeleteColumnAsync: hard delete -> soft delete
- Minden ColumnDefinition lekérdezés szűri a törölt oszlopokat
- TaskStatusHistory megőrzi a törölt oszlopok adatait a CFD-hez

**TaskStatusHistory pótlások:**
- CreateTaskAsync: kezdeti bejegyzés task létrehozáskor
- AssignTaskToBoardAsync: bejegyzés minden oszlopváltozáskor
- AssignTaskToSprintAsync: bejegyzés oszlopváltozáskor
- ActivateSprintAsync: bejegyzés minden sprint task mozgatásakor
- PlanSprintAsync: bejegyzés backlogba visszarakáskor
- CompleteSprintAsync: bejegyzés befejezetlen taskok backlogba kerülésekor

**TaskStatusHistory refactor:**
- Status mező eltávolítva — Column navigation property alapján számított
- Konzisztens adatok oszlop státusz változásakor
- Törölt oszlopok adatai megmaradnak a CFD-ben

**CFD Board szűrő:**
- Opcionális boardId paraméter hozzáadva GetCumulativeFlowAsync-hoz
- Statuses és histories egyaránt szűrve boardId alapján
- History bejegyzések a Column boardja alapján szűrődnek (nem task jelenlegi boardja)
- Board selector hozzáadva a StatisticsView CFD szekciójában

#### Ismert Limitációk & Tervezett Fejlesztések

##### SignalR Centralizálás (post-deployment):
Jelenlegi probléma:

Komponensenkénti SignalR regisztráció:
onDestroy leiratkozás -> nézetek közötti race condition
Teljes getTasksAsync() újratöltés sok eventnél
-> Felesleges DB lekérdezések és hálózati forgalom

Tervezett megoldás:

Centralizált event kezelés AppLayout szinten
Store-alapú kommunikáció komponensekkel
Event payload közvetlen feldolgozása store-ban
-> Csak a változott adat frissül
-> Jelentősen kevesebb hálózati forgalom


##### File Feltöltés Optimalizálás (post-deployment):
Jelenlegi problémák:

Fájl méret limit (ASP.NET Core alapból 30MB)
Content-Type validáció hiányosságok
Nagyobb fájloknál timeout lehetséges

Tervezett megoldás:

Méret limit konfiguráció .env-ből
Engedélyezett content-type-ok explicit listája
Chunked upload vagy presigned URL megközelítés


##### Egyéb tervezett fejlesztések:

- TOTP 2FA (bejelentkezés + kritikus műveletek)
- AES-256 WebhookSecret titkosítás
- HashiCorp Vault (production skálán)
- Redis backplane SignalR horizontális skálázáshoz
- PR body alapú task matching (pull_request.body mező)
- Invitation management TeamView-ban
- Team Workload szétválasztás:
  - Aktív load: aktív sprintben + boardon lévő taskok (ténylegesen folyamatban)
  - Tervezett load: sprinthez rendelt de még nem aktív, illetve backlogban lévő hozzárendelt taskok
  - Pontosabb képet ad a csapat tényleges vs tervezett terheléséről
- Git View sprint nézet:
  - Sprintenként csoportosított taskok megjelenítése
  - Taskokhoz rendelt commitok és PR-ok listája (CommitCard, PrCard)
  - Commit/PR átrendelés másik taskhoz ha hibásan lett hozzárendelve
  - Sprint szűrő selector a Git View-ban
  - Megvalósítható a meglévő TaskResponse.commitLinks/prLinks alapján
  - Új backend endpoint nem szükséges
- GitLab webhook teljes támogatás és tesztelés
- Git provider absztrakció refactor:
  - IGitProvider interface
  - GitHubProvider, GitLabProvider implementációk
  - Könnyen bővíthető új providerekkel (Bitbucket, Gitea stb.)

#### Manuális Tesztelési Területek
Kritikus user flow-k:

Task létrehozás -> board hozzárendelés -> sprint hozzárendelés -> mozgatás -> lezárás
Sprint lifecycle: Planning -> Active -> Completed
Git webhook: push -> commit matching -> PR -> merge
File feltöltés: task és projekt szintű
Team management: meghívó -> csatlakozás -> role változtatás
Statisztikák: burndown, velocity, CFD adatok helyessége

Checklist a tesztekről: TESTING.md (repó root-ban)

## Deployment, Documentation & Presentation Preparation
(Régi leírás:)
Docker Compose production configuration with HTTPS (Let's Encrypt) and optimized Nginx config. Final (MVP level) README with setup instructions, architecture overview, and API reference. Project documentation update (Functional and Technical Specification alignment with implemented features). Demo data preparation for presentation. Final smoke testing in production-like environment.
(Új átgondolt leírás:)
Hetzner VPS alapú production deployment Dokploy PaaS platformon. Cloudflare DNS + SSL (Full Strict) konfiguráció. SignalR WebSocket keepalive a Cloudflare timeout kezeléshez. README frissítés production deployment instrukciókkal. Final smoke testing production környezetben.

### Infrastruktúra döntések

**Platform:**
- **Szerver:** Hetzner VPS
- **Domain & DNS:** Cloudflare
- **PaaS:** Dokploy (self-hosted, Docker Compose alapú)

**Cloudflare konfiguráció:**
- Proxy: BE (Orange Cloud) — DDoS védelem, IP elrejtés
- SSL mód: Full Strict
- Dokploy automatikusan kezeli a Let's Encrypt tanúsítványt

**Miért Dokploy:**
- Beépített Traefik reverse proxy (WebSocket / SignalR támogatás)
- Automatikus Let's Encrypt megújítás
- Git-based auto deploy (push -> deploy)
- Environment variables kezelés UI-ból
- Docker Compose közvetlen import
- Beépített monitoring és logok
- Nginx és Certbot manuális konfiguráció NEM szükséges

**Domain struktúra:**
- app.trunkpeter.com -> Frontend (Svelte SPA)
- api.trunkpeter.com -> Backend API + SignalR hub

**Cloudflare + Let's Encrypt kombináció:**
- Cloudflare Proxy ON -> DDoS védelem, IP elrejtés
- Full Strict SSL mód -> Dokploy Let's Encrypt tanúsítvány szükséges
- certresolver neve Dokploy Traefik-ben: letsencrypt

---

### Tervezett implementáció

#### Docker Compose Production konfiguráció

**Szolgáltatások:**
services:
- api:          # ASP.NET Core production build
- db:           # PostgreSQL production konfiguráció
- minio:        # MinIO object storage
- Nginx és Certbot NEM kell — Dokploy Traefik kezeli

**Environment Variables production értékek:**
- JWT
JWT_SECRET=
- PostgreSQL
DB_HOST=
DB_PORT=
DB_NAME=
DB_USER=
DB_PASSWORD=
- MinIO
MINIO_ENDPOINT=
MINIO_ACCESS_KEY=
MINIO_SECRET_KEY=
MINIO_BUCKET=
- API
API_BASE_URL=
- SignalR keepalive
VITE_SIGNALR_KEEPALIVE_ENABLED=true
VITE_SIGNALR_KEEPALIVE_SECONDS=15
- Jövőbeli
ENCRYPTION_KEY=
REDIS_CONNECTION=

**ASP.NET Core production build:**
- ASPNETCORE_ENVIRONMENT=Production
- Health check endpoint (`/health`)
- Production logging konfiguráció

#### SignalR WebSocket keepalive

**Probléma:** Cloudflare 100 másodperces WebSocket timeout
Megoldás:
- 15mp-enként ping -> Cloudflare timeout sosem következik be
- Backend konfiguráció NEM szükséges
- Environment variable alapú kapcsoló

---

#### README kiegészítés a régebbi haladásra építve

**Tartalom:**
- Projekt áttekintés és funkciók listája
- Architektúra áttekintés (Backend, Frontend, Infrastructure)
- Development setup instrukciók
- Production deployment instrukciók
- Environment variables referencia
- API végpontok áttekintése
- Ismert limitációk és tervezett fejlesztések

---

#### Production Smoke Testing

**Tesztelendő:**
- SSL tanúsítvány érvényessége (Full Strict)
- SignalR WebSocket kapcsolat HTTPS-en
- Cloudflare timeout nem következik be (keepalive teszt)
- MinIO fájl feltöltés/letöltés
- PostgreSQL kapcsolat és migrációk
- Git webhook fogadás éles URL-lel
- TESTING.md kritikus flow-k production környezetben

### Implementációs sorrend

1. Hetzner VPS bérlés + Dokploy telepítés (kész)
  - Dokploy telepítés: curl -sSL https://dokploy.com/install.sh | sh
  - Docker Swarm manuális init szükséges volt: docker swarm init --advertise-addr {IP}
2. Cloudflare DNS beállítás (A record -> Hetzner IP) (kész)
  - app.trunkpeter.com -> Frontend
  - api.trunkpeter.com -> Backend API
3. Docker Compose production konfiguráció kiegészítése
  - Network konfiguráció: dokploy-network (Traefik) + default (belső kommunikáció)
  - Minden service saját redirect middleware-t kap (Traefik Docker provider limitáció)
4. SignalR keepalive implementálás (frontend) (kész)
  - VITE_API_URL environment variable hub URL-hez
  - VITE_SIGNALR_KEEPALIVE_ENABLED / VITE_SIGNALR_KEEPALIVE_SECONDS
  - ImportMetaEnv type declaration (vite-env.d.ts)
5. Health check endpoint hozzáadása (backend)
6. Production .env összeállítása
7. Dokploy-ba Docker Compose import + env vars beállítás
  - Minden env var explicit megadandó a docker-compose environment szekciójában
8. First-time setup: DB migráció, MinIO bucket létrehozás
9. Cloudflare SSL Full Strict beállítás
10. Git webhook URL frissítés éles domain-re (kész volt, invite-esetében volt elmaradás)
11. Production smoke testing (TESTING.md alapján)
12. README production deployment instrukciók megírása

#### Build & Deployment stratégia

**Megközelítés: Dokploy beépített build**
- Dokploy figyeli a GitHub repót
- Push esetén Dokploy lehúzza a kódot és buildelel Dockerfile alapján
- Újraindítja a service-t automatikusan
- Minden konfiguráció Dokploy UI-ból kezelhető
- GitHub Actions nem szükséges (később könnyen hozzáadható)

**Infrastruktúra döntések:**
- Lokális: `docker-compose.yml` változatlan (postgres + minio)
- Production: `docker-compose.prod.yml` (postgres + minio + api + Traefik)
- Production fájlok NEM kerülnek git-be (.gitignore)
- Production fájlok közvetlenül a szerveren hozandók létre

**Docker fájlok:**
- `backend/Dockerfile` — ASP.NET Core production build
- `frontend/Dockerfile` — Svelte production build + static serving

#### Ismert gotchák és megoldások

**Dokploy environment variables:**
- Minden változót explicit kell a docker-compose environment szekciójában megadni
- Csak a docker-compose-ban felsorolt változók kerülnek a konténerbe, ami nincs azt a környezet nem teszi elérhetővé az instance számára

**Traefik middleware scope:**
- Docker provider esetén middleware-ek service-specifikusak
- Minden service saját redirect middleware-t kap egyedi névvel
- Pl.: api-https-redirect, frontend-https-redirect

**Docker network:**
- API-nak mindkét network-ön kell lennie:
  - dokploy-network: Traefik látja
  - default: postgres/minio belső kommunikáció

**PostgreSQL migration:**
- depends_on healthcheck
- Retry logika szükséges, ne egyből induljon újra (10 retry)

**ASP.NET Core .env betöltés:**
- Production-ban NE töltsük be a .env fájlt
- ASPNETCORE_ENVIRONMENT=Production esetén kihagyandó
- Docker environment variables közvetlenül elérhetők

**Invite link URL:**
- FRONTEND_URL environment variable alapján generálandó
- Ne legyen hardcode-olva localhost:5173

**(After MVP - starting point)**
## SignalR Architecture Refactor
Centralized SignalR event handling at AppLayout level. Direct store updates from event payloads instead of full API reloads. Redis backplane support for horizontal scaling.

### Tervezett implementáció

#### Backend változások

**Broadcast egységesítés:**
- Összes event projekt szintű csoportba kerül (project-{projectId})
- board-{boardId} csoport eltávolítva
- joinBoard / leaveBoard Hub metódusok eltávolítva
- TaskCreated projekt szintű broadcast hozzáadása
- TaskMoved payload kiegészítése (boardId, sprintId, completedAt)
- TaskUpdated payload teljes TaskResponseDto-val

**Endpoint optimalizálások:**
- `GET /api/projects/{id}/tasks?activeSprintOnly=true&includeBacklog=true`
  -> Csak aktív sprint + backlog taskok projekt megnyitáskor
- `GET /api/projects/{id}/sprints?state=Active,Planning`
  -> Csak aktív és tervezett sprintek projekt megnyitáskor
  -> Completed sprintek lazy load SprintsView-ban
- `GET /api/projects/{id}/boards?includeColumns=true`
  -> Board + oszlopok egy kérésben

**Redis Backplane (production):**
- Docker Compose kiegészítése image-el és port-al

- Program.cs configuráció hozzáadása

- Fejlesztésben nem szükséges — csak production horizontális skálázásnál
- Environment variable alapú kapcsoló: REDIS_CONNECTION jelenlétében aktiválódik

---

#### Frontend változások

**Új event store-ok:**
taskEventStore.ts    -> Task események (created/updated/moved/deleted/rebalanced/label/assignee)
sprintEventStore.ts  -> Sprint események (created/updated/deleted)
columnEventStore.ts  -> Oszlop események (created/updated/deleted/reordered)
boardEventStore.ts   -> Board események (created/updated/deleted)
activityEventStore.ts -> Activity események (created)

Minden store struktúrája:
- Típus, Payload, Timestamp (Duplikánsok kihagyása)

**AppLayout — Centralizált event kezelés:**
- Összes SignalR event az AppLayout-ban regisztrálva
- Event érkezésekor -> megfelelő store emit
- Komponensek NEM regisztrálnak SignalR eventeket
- `joinBoard` / `leaveBoard` hívások eltávolítva

**Komponensek frissítése:**
BoardView:

signalRService.on() hívások -> taskEventStore + columnEventStore + boardEventStore subscribe
Board váltáskor nincs API hívás -> store szűrés boardId alapján
Összes task már a store-ban van projekt megnyitáskor

SprintsView:
signalRService.on() hívások -> taskEventStore + sprintEventStore subscribe
Completed sprintek lazy load: csak SprintsView-ban lévő szekció megnyitáskor kérjük le

ProjectOverview:
signalRService.on() hívások -> taskEventStore + sprintEventStore subscribe

ActivityFeed:
signalRService.on() -> activityEventStore subscribe

GitView:
signalRService.on() -> gitEventStore subscribe

CommentSection:
signalRService.on() -> commentEventStore subscribe


**Projekt megnyitáskor párhuzamos betöltés:**
- A frontenden egy Primise-ban kérjük le a szükséges alap adatokat (Nem egy GOD endpoint hanem a meglévő endpointok hívása párhuzamosan).

**Lazy loading stratégia:**
Azonnal (projekt megnyitáskor):
- Aktív sprint taskjai
- Projekt backlog taskjai
- Aktív + Planning sprintek
- Összes board + oszlop
- Labelek, tagok, integrációk
Igény szerint:
- Completed sprint taskjai (SprintsView megnyitáskor)
- Completed sprintek listája (SprintsView megnyitáskor)
- Statistics adatok (StatisticsView megnyitáskor)

---

### Implementációs sorrend
Backend:

Endpoint optimalizálások (activeSprintOnly, includeColumns stb.)
board-{boardId} broadcast csoport eltávolítása
Összes event projekt szintű csoportba migrálása
Payload kiegészítések (TaskMoved, TaskCreated, TaskUpdated)
Redis backplane konfiguráció (production)

Frontend:
6. Event store-ok létrehozása
7. AppLayout: SignalR -> store emit centralizálás
8. BoardView refactor: signalRService.on -> store subscribe
9. SprintsView refactor
10. ProjectOverview refactor
11. ActivityFeed refactor
12. GitView refactor
13. CommentSection refactor
14. Párhuzamos initial load implementálása
15. Lazy loading SprintsView completed sprintekhez

### Várható előnyök
- Board váltás azonnali (nincs API hívás)
- Nincs race condition nézetek váltásakor
- Kevesebb hálózati forgalom
- Komponensek könnyen cserélhetők/tesztelhetők
- Horizontálisan skálázható (Redis backplane)
- Egységes event kezelés egy helyen
- Lazy loading -> gyorsabb kezdeti betöltés

## Security Hardening (TOTP 2FA) 
AES-256 encryption for WebhookSecret storage with server-side master key. TOTP 2FA implementation for login and critical operations (Google Authenticator compatible). Considering HashiCorp Vault integration for production-scale secret management.

## File Upload Improvements
Configurable file size limits via environment variables. Explicit content-type allowlist. Chunked upload or presigned URL approach for large files.

## Git Webhook Enhancements
PR body-based task matching in addition to title matching. GitLab webhook full support and testing. Git provider abstraction using Factory Pattern (IGitProvider interface, GitHubProvider, GitLabProvider) for easy extension with new providers (Bitbucket, Gitea etc.).

## Git View Sprint Overview
Sprint-based task grouping in Git View with associated commits and PRs. Manual commit/PR reassignment between tasks. Sprint selector filter. Built on existing TaskResponse.commitLinks/prLinks — no new backend endpoints required.

## Git Intelligence – Branches & Insights

Extended Git integration providing branch tracking, developer activity insights, and sprint-level git analytics. All data derived exclusively from incoming webhook payloads — no access token required.

### Tervezett implementáció

#### Branches

**Cél:**
Átlátható képet adni arról melyik branch-en folyik munka és az melyik taskhoz tartozik — anélkül hogy a GitHubon vagy GitLaben kellene keresgélni.

**Új modell: Branch**
Id, ProjectId, IntegrationId, Name, TaskId (null = unmatched), LastPushedBy, LastPushedAt, CreatedAt

**Stale detektálás:**
Lekérdezéskor számított — nem tárolt State mező (Nem a pontos statisztika a lényeg, a PM tudja hogy miért nem volt Kód feltöltve a csapata által, egy figyelem felhívó metrika)
Threshold: LastPushedAt + X nap (projekt szinten konfigurálható)
Alapértelmezett: 5 nap
Stale + task Done státusz -> külön kiemelés ("branch cleanup szükséges")

**Webhook feldolgozás bővítések:**

Push event:
A payload ref mezője alapján branch név kinyerése (refs/heads/ prefix levágása)
TaskKey matching a branch névben (pl. feature/GTP-1-login-fix)
Branch record létrehozás vagy LastPushedAt frissítés

Delete event (új):
ref_type == "branch" -> Branch record soft delete / Deleted state
ref_type == "tag" -> ignorálva
Merged state meghatározása: volt-e merge-elt PR ehhez a branch-hez?

**UI — Git View Branches tab:**

Branch lista: név, kapcsolódó task, utolsó push, fejlesztő
Stale branchek kiemelve (sárga jelzés)
Stale + Done task -> piros jelzés ("cleanup szükséges")
Manuális task-branch összerendelés unmatched brancheknél
Info üzenet: "Csak az integráció bekapcsolása után érkezett push-ok láthatók"
TaskDetailModal: kapcsolódó branchek megjelenítése

**Provider absztrakció (Factory Pattern):**
IGitProvider interface:

ValidateSignature(payload, headers)
GetBranchName(payload)
GetEventType(headers)
ProcessPushEvent(payload)
ProcessPullRequestEvent(payload)
ProcessDeleteEvent(payload)

GitHubProvider : IGitProvider
GitLabProvider : IGitProvider
GitProviderFactory -> provider alapján helyes implementáció
Könnyen bővíthető: Bitbucket, Gitea stb.

---

#### Insights

**Cél:**
Objektív, git aktivitáson alapuló sprint analitika — automatikus, nem manipulálható manuálisan.

**Nincs új modell szükséges:**
CommitLink: TaskId, AuthorName, CommittedAt -> fejlesztői aktivitás
PrLink: TaskId, AuthorName, CreatedAt, MergedAt, State -> PR analitika
Sprint + Task kapcsolat -> sprint szintű szűrés

**InsightsService metódusok:**
GetSprintGitActivityAsync(projectId, sprintId):
-> Hány taskhoz érkezett legalább egy commit
-> Hány task van nulla git aktivitással (stale taskok)
-> Git aktivitás nélküli taskok listája
GetDeveloperActivityAsync(projectId, sprintId):
-> Fejlesztőnként commit szám
-> Fejlesztőnként érintett taskok száma
-> Fejlesztőnként PR szám
GetPrAnalyticsAsync(projectId, sprintId):
-> Merged PR-ok átlagos cycle time (CreatedAt -> MergedAt)
-> Closed PR-ok száma és aránya (visszautasítási arány)
-> Nyitott PR-ok száma
GetSprintSummaryAsync(projectId, sprintId):
-> Összes commit a sprintben
-> Merged PR-ek száma
-> Closed (visszautasított) PR-ek száma
-> Legtöbbet commitoló fejlesztő
-> Legtöbb aktivitást kapott task
-> Stale taskok száma (nyitva + nulla git aktivitás)
-> Átlagos PR cycle time
GetMostActiveTasksAsync(projectId, sprintId):
-> Legtöbb commitot kapott taskok
-> Segít a jövőbeli sprint planning becslésekben

**Sprint összehasonlítás (később — min. 3-4 sprint adat szükséges):**
-> PR cycle time trend sprintről sprintre
-> Stale task arány változása
-> Csapat git aktivitás változása
-> Csak akkor jelenik meg ha elegendő historikus adat áll rendelkezésre

**UI — Git View Insights tab:**
Sprint szűrő selector
Fejlesztői szűrő (adott fejlesztő aktivitása)
Megjelenítés:

Sprint Git Activity kártya (committal rendelkező vs stale taskok)
Fejlesztői aktivitás táblázat (commit szám, érintett taskok, PR-ok)
PR Analytics kártya (cycle time, merged/closed arány)
Sprint Summary blokk (exportálható/megosztható)
Stale taskok listája (nyitva + nulla git aktivitás)
Legtöbb aktivitást kapott taskok listája
---

### Közös technikai megjegyzések

- Minden adat kizárólag webhook payloadokból — access token nem szükséges
- Branch-task matching: TaskKey a branch névben (pl. feature/GTP-1-login)
- "Fejlesztő" azonosítása: commit/PR author mező alapján Opcionálisan összeköthető a saját user rendszerrel
- Insights adatok nem real-time: webhook feldolgozáskor frissülnek
- Stale branch threshold: projekt szinten konfigurálható (alapértelmezett: 5 nap)
- PR cycle time: csak merged PR-oknál számított átlag
- Closed PR-ok visszautasítási arányként külön mutatva
- Sprint összehasonlítás: minimum 3-4 sprint után érhető el

### Implementációs sorrend

- Branch modell + migration
- GitProviderFactory + IGitProvider interface
- GitHubProvider + GitLabProvider implementációk
- Push event bővítés: branch tracking
- Delete event kezelés
- IBranchService + BranchService
- BranchController endpointok
- IInsightsService + InsightsService
- InsightsController endpointok
- Git View Branches tab frontend
- Git View Insights tab frontend
- TaskDetailModal: kapcsolódó branchek
- Sprint összehasonlítás (ha elegendő az adat)

## Team & Project Improvements
Invitation management in TeamView (list, copy, delete invites). Team Workload split: active load (tasks in active sprint on board) vs planned load (sprint-assigned or backlog tasks). 

## Multi-Sprint Analytics
Sprint comparison charts after minimum 3-4 sprints of data. PR cycle time trends, stale task ratios, and team git activity over time.