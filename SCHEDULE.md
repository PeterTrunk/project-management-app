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

## Development Environment Setup & Database Design
Docker Compose environment setup with PostgreSQL, MinIO (S3-compatible object storage), and Nginx reverse proxy. PostgreSQL schema design using dbdiagram.io, defining all core entities (users, projects, tasks, sprints, labels, comments, attachments, activity_log) with proper relations, constraints, and indexing strategy. Project repository initialization with backend (ASP.NET Core) and frontend (Svelte) folder structure.

**Kihagyott elemek (tudatos döntés)**
- MinIO és Nginx konfiguráció — a Git Webhook & MinIO héten kerül sorra
- Svelte frontend inicializálás — még nincs kipróbálható funkció

## EF Core Models, Migrations & Database Infrastructure
Entity Framework Core Code First model classes and initial migration. Npgsql provider configuration for PostgreSQL. Database triggers for automated fields (updated_at, task_key generation). Statistical views for reporting queries. Seed data for development and testing.

**Kihagyott elemek (tudatos döntés)**
- Statisztikai view-ok — a Statistics Dashboard héten (10. hét) kerül sorra
- task_key generálás — a CRUD API héten kerül sorra a ProjectCounter alapján

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
- Svelte frontend JWT token handling (localStorage) — 
  frontend inicializálás a CRUD API hét után kerül sorra
- Role nevek eltérnek az eredetitől — általánosabb, 
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
- GetTasksAsync: Pagination — jövőbeli fejlesztés, kommentben jelölve (Oka:nagyon sok adatot húzhat be egyetlen kérésben, magas DB Query leterhelés).

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
- Nincs oldalváltás — aktív nézet változóval vezérelt tartalom, vagy modal felugrik
- Route-ok: "/" (Login), "/register" (Register), "/app" (AppLayout)

**Navbar opciók:**
  Overview | Board | Sprints | Team | Git | Statistics | Team Resources | Project Settings

**Nézetek tartalma**
- Overview — projekt alapadatok (név, kulcs, leírás, tulajdonos, dátumok, státusz)
- Board — Kanban tábla, oszlop létrehozás modal-ban ("+ Oszlop hozzáadása")
- Sprints — sprint kezelés + backlog taskok + task létrehozás
- Team — tagok listája/kezelése + Recent Activity feed
- Git — összekapcsolt commitok/PR-ek + manuálisan hozzáköthető össze nem kötött commitok
- Statistics — grafikonok, metrikák
- Team Resources — erőforrás elosztás
- Project Settings — projekt beállítások, archiválás, törlés

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
- Sprint UI frontend befejezése

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
- Sprint.BoardId eltávolítva — sprint több boardhoz is tartozhat
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
- CreateColumnModal.svelte: oszlop létrehozás, pozíció reorder-rel kezelve
- ColumnDetailModal.svelte: oszlop szerkesztés és törlés
- CreateBoardModal.svelte + UpdateBoardModal.svelte: board kezelés
- CreateTaskModal.svelte: isBacklogMode támogatás (boardId/columnId null)
- TaskDetailModal.svelte: két oszlopos layout, szerkesztés/törlés, label kezelés
- CommentSection.svelte: komment kezelés
- LabelCard.svelte + CreateLabelModal.svelte: label kezelés
- SprintsView.svelte: sprint lista (aktív kiemelve, planning/completed collapselhető)
- SprintCard.svelte: sprint kártya board-csoportosított task megjelenítéssel
- ProjectBacklog.svelte: projekt szintű backlog (collapselhető)
- BacklogTaskCard.svelte: backlog task kártya (sprint/board hozzárendelés, hamburger menü)
- CreateSprintModal.svelte: sprint létrehozás

**Drag & Drop**
- svelte-dnd-action: oszlop átrendezés (lock gombbal) + task mozgatás
- Lexorank string pozíció: localeCompare alapú rendezés
- columnTasks Record: oszloponkénti task kezelés
- isDragging flag: store felülírás megakadályozása

### Sprint & Task Modell Architektúra Döntések
**Task modell refaktorálás**
- Task.Status eltávolítva — computed property: ColumnDefinition?.MapsToStatus ?? "Backlog"
- Task.BoardId nullable: ha null -> Projekt Backlogban van
- Task.ColumnId nullable: ha null -> Projekt Backlogban van
- Task.CompletedAt: amikor a task az utolsó oszlopba ér (MoveTaskAsync állítja be)
- Task.ClosedAt: amikor a sprint le lett zárva (CompleteSprintAsync állítja be)
- Backlog definíció: BoardId=null AND ColumnId=null AND SprintId=null

**Sprint modell refaktorálás**
- Sprint.BoardId eltávolítva — egy sprint több boardhoz is tartozhat
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
- BoardView: Backlog oszlop elrejtve (Position>0 oszlopok láthatók csak) — TODO
- Label kezelés: Project Settings-ben
- Column törlés: csak üres, nem-Backlog oszlop törölhető
- Sprint aktiválás: taskok automatikusan az első valódi oszlopba kerülnek
- Sprint visszatervezés: taskok visszakerülnek a Board Backlog oszlopba
- Sprint lezárás: csak ha minden task CompletedAt != null


### Task/Column Pozíció Kezelési Módszerek — Döntési Dokumentáció

#### Vizsgált megoldások

**1. Szekvenciális frissítés (1, 2, 3, 4...)**
Minden mozgatásnál az összes utána lévő task pozícióját frissítjük sorozatosan.
- pozitívum: Egyszerű implementáció
- pozitívum: Könnyen érthető
- negatívum: Skálázási szempontból: N darab task mozgatásakor N db UPDATE szükséges -> teljesítmény problémásabb
- negatívum: Párhuzamos mozgatásnál könnyen ütközések keletkeznek

**2. Linked List (nextTaskId mutató)**
Minden tasknak van egy nextTaskId mutatója a következő taskra.
- pozitívum: Közbeszúrás mindig csak 2 sor frissítése
- pozitívum: Sosem fogy el a "hely"
- negatívum: ORDER BY nem működik egyszerűen — rekurzív CTE szükséges -> lassú
- negatívum: Indexeléssel sem oldható meg a sorrendezési probléma (O(n))
- negatívum: Lánc integritás sérülhet (FK constraint + tranzakció részben megoldja)
- negatívum: Párhuzamos mozgatásnál optimistic locking szükséges
- (negatívum: Iparági szinten nem elfogadott megoldás erre a problémára)

**3. Float alapú pozíció(az első megoldás volt a problémára.)** 
Közbeszúrásnál a két szomszéd átlaga lesz az új pozíció (pl. 1 és 2 közé -> 1.5).
- pozitívum: Egyszerű implementáció 
- pozitívum: ORDER BY egyszerű és gyors
- pozitívum: Közbeszúráshoz csak 1 sor frissítése kell és két sort csak olvasunk
- negatívum: IEEE 754 float precizitási korlát — sok közbeszúrás után elfogy a hely
- negatívum: Renormalizálás szükséges (manuális trigger, ellenörzés hogy a float kifáradás közeli e.)
- negatívum: Párhuzamos ütközésnél (két user egyszerre mozgat) nem determinisztikus

**4. Lexorank (Jira megoldása) — VÁLASZTOTT MEGOLDÁS**
String alapú pozíció Base36 karakterkészlettel, bucket rendszerrel. (hasonló kissé a float alapúhoz, átlagot számít alap esetben)
Például: "0|a", "0|am", "0|b" — közbeszúráskor: "0|a" és "0|b" közé -> "0|am"

Bucket rendszer:
- 3 bucket (0, 1, 2) körkörösen — a prefix jelzi melyik bucketben van az elem
- Ütközés esetén (két azonos pozíció) az egész oszlop átkerül a következő bucketbe
- Bucket 2 exhaustion után visszaáll bucket 0-ra (öngyógyító, végtelen ciklus)

Base36 kapacitás:
- 1 karakter: 36^1 = 36 pozíció
- 2 karakter: 36^2 = 1,296 pozíció
- 3 karakter: 36^3 = 46,656 pozíció
- String csak hosszabbodik, sosem fogy el a hely

- pozitívum: Végtelen közbeszúrás — sosem fogy el a hely
- pozitívum: ORDER BY egyszerű és gyors (localeCompare / abc sorrend)
- pozitívum: SignalR barát — csak a position stringet kell broadcastolni
- pozitívum: Öngyógyító bucket rendszer — automatikus rebalancing ütközéskor
- pozitívum: Iparági standard (Jira, Linear)
- pozitívum: Adatbázis szinten hatékony index támogatás
- negatívum: Komplexebb implementáció mint a float
- negatívum: Párhuzamos dupla mozgatás esetén a task "kicsit más helyre" kerülhet
  (nem adatvesztés, csak nem determinisztikus sorrend — elfogadható tradeoff)

#### Döntés
A **Lexorank** implementálása melletti döntés mert a komplexitás 
növekedés elfogadható tradeoff a jelentős scaling és robusztussági előnyökért.
A float megoldás ideiglenesen implementálva marad amíg a Lexorank 
implementáció el nem készül.

#### Implementációs terv
- Position mező típusa: float -> string (migration szükséges)
- Új LexorankService a pozíció számításhoz
- MoveTaskAsync frissítése
- Frontend: sort by position -> sort by localeCompare

## SignalR Real-Time Updates & Notifications
Spring break week dedicated to the real-time layer - the most complex cross-cutting feature. SignalR hub implementation on the backend (BoardHub, NotificationHub) for real-time task movement, status changes, and new comments. SignalR client connection manager with automatic reconnection. Nginx WebSocket proxy configuration for the /hubs/* route. In-app notification system: notification bell in navbar, unread count, notification list. SignalR-based real-time notification delivery for task assignment, comments, and sprint changes.

## Sprint Management & Team Management
Sprint lifecycle API: creation, activation, closing. Sprint-to-task assignment. Constraint enforcement (one active sprint per project). Sprint manager interface with sprint status transitions (Open - Active - Closed). Team management interface: member list, role display, member invitation (invite link). Permission-based UI rendering based on user roles.

## Git Webhook Integration & MinIO File Storage
GitHub/GitLab webhook receiver endpoint (POST /api/git/webhook) with secret validation. Commit and pull request event parsing, task matching by identifier pattern (e.g., PM-123). Git activity display on task detail view. MinIO integration via IFileStorageService interface: file upload, download, and deletion. Attachment metadata storage in PostgreSQL, binary files in MinIO. Team Resources page for shared project documents.

## Statistics Dashboard & ECharts Integration
Statistics view with ECharts visualizations: task status distribution (pie chart), sprint burndown and burnup charts (line chart), team workload distribution (bar chart), sprint velocity over time, cumulative flow diagram (stacked area chart). Backend reporting endpoints using PostgreSQL views for efficient aggregation. Filterable by sprint, user, and date range.

## Search/Filter, UI Polish & Responsive Design
Task search and filtering on the board (by assignee, priority, keyword, label). Activity log view with filtering by user and event type. Responsive UI refinements for desktop and notebook resolutions. Consistent spacing, color scheme, and typography across all views. Dark/light mode toggle in profile settings. Overview dashboard with personal task summary, overdue items, and recent activity.

## Testing, Bug Fixes & Final Integration
Unit tests for API endpoints and service layer (xUnit). Integration tests for database operations. End-to-end testing of critical user flows (task creation, board interaction, sprint management). Bug fixing and edge case handling. Full system integration testing across all components (frontend, backend, SignalR, MinIO, PostgreSQL). Performance review and query optimization where needed.

## Deployment, Documentation & Presentation Preparation
Docker Compose production configuration with HTTPS (Let's Encrypt) and optimized Nginx config. Final README with setup instructions, architecture overview, and API reference. Project documentation update (Functional and Technical Specification alignment with implemented features). Demo data preparation for presentation. Final smoke testing in production-like environment.