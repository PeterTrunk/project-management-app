# Project Schedule
- 2026-02-22: Development Environment Setup & Database Design
- 2026-03-01: EF Core Models, Migrations & Database Infrastructure
- 2026-03-08: Authentication & Authorization (JWT + RBAC)
- 2026-03-15: Project & Task CRUD API
- 2026-03-22: Svelte Frontend Setup & Core Layout
- 2026-03-29: Kanban Board & Drag-and-Drop
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

**API Service Layer**
- client.ts: axios instance JWT request interceptor + 401 response interceptor
- authApi.ts: loginAsync, registerAsync, refreshAsync, logoutAsync, meAsync, changePasswordAsync, updateProfileAsync
- projectApi.ts: getProjectsAsync, createProjectAsync, updateProjectAsync, archiveProjectAsync, unarchiveProjectAsync, deleteProjectAsync, getProjectByIdAsync

**Stores**
- authStore.ts: token, user, isAuthenticated + login/logout helper függvények
- projectStore.ts: projects, activeProject + setProjects, setActiveProject, clearProjects

**Validators**
- Validátorok kiszervezve, validators.ts
- validateEmail, validateDisplayName, validatePassword, validateProjName, validateDescription 
- újrafelhasználhatóság

**Oldalak**
- Login.svelte: form, JWT auth, login után redirect Főoldalra-ra, regisztáció gomb redirect registerációs oldalra
- Register.svelte: form, validáció (email, displayName, jelszó erősség), redirect Login-ra
- AppLayout.svelte: fő layout, Discord-szerű single-page design

**Komponensek**
- CreateProjectModal.svelte: projekt létrehozás, validáció, overlay, Escape bezárás
- ConfirmModal.svelte: újrafelhasználható megerősítő modal (archive, unarchive, delete, update)
- ProjectOverview.svelte: projekt alapadatok megjelenítése
- ProjectSettings.svelte: projekt szerkesztés, archiválás, törlés
- UserSettingsModal.svelte: profil megtekintés, profil szerkesztés, jelszó változtatás

**Backend kiegészítések**
- CORS konfiguráció: AllowFrontend policy (localhost:5173)
- UpdateProfileDto, UpdateUserValidator, UserProfileDto
- PATCH /api/auth/profile endpoint
- ChangeUserProfileAsync service metódus

### UI architektúrával kapcsolatos döntések:
**Layout**
- Discord-szerű single-page layout: bal oldali sidebar + jobb oldali dinamikus tartalom
- Nincs oldalváltás — aktív nézet változóval vezérelt tartalom, vagy modal felugrik
- Route-ok: "/" (Login), "/register" (Register), "/app" (AppLayout)
**Navbar opciók:**
  Overview | Board | Sprints | Team | Labels | Git | Statistics | Team Resources | Project Settings
**Nézetek tartalma**
- Overview — projekt alapadatok (név, kulcs, leírás, tulajdonos, dátumok, státusz)
- Board — Kanban tábla, oszlop létrehozás modal-ban ("+ Oszlop hozzáadása")
- Sprints — sprint kezelés + backlog taskok + task létrehozás
- Team — tagok listája/kezelése + Recent Activity feed
- Labels — projekt szintű label CRUD
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

## Kanban Board & Drag-and-Drop
Interactive Kanban board with drag-and-drop functionality (svelte-dnd-action). Board columns (To Do, In Progress, Review, Done) with task cards displaying key information (title, priority, assignee, due date, labels). Task detail modal with full information, comment panel, and attachment list. Overdue task visual indicators with color-coded due dates.

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