# Manual Test Results

## RBAC Authorization (2026-03-05)
Endpoint: GET /api/auth/test-rbac/{projectId}

| Eset | User | Szerepkör | Eredmény |
|------|------|-----------|----------|
| Nincs token | - | - | 401 Unauthorized |
| Van token, nincs tagság | test@example.com | - | 403 Forbidden |
| Van token, van tagság | test@example.com | Owner | 200 OK |

Teszt menete: 
1. register endpointon uj user
2. bearer token auth swaggerben
3. ideiglenes TestRbac endpoint-ra projectId-val küldünk egy kérést: Forbidden (Nincs project tagság - ProjectMember kapcsolótábla kapcsolat)
4. pgsql parancsal insertelünk egy projecttagságot.
5. ujonnan tesztelés: engedélyezve

Ideiglenes teszt endpoint (AuthController-ből törölve a teszt után):
```csharp
[HttpGet("test-rbac/{projectId}")]
[Authorize(Policy = "ProjectMaintainer")]
public ActionResult TestRbac(Guid projectId)
{
    return Ok("Hozzáférés engedélyezve!");
}
```
## Auth Endpoints (2026-03-08)
Endpoints: POST /api/auth/register, login, refresh, logout, changepassword | GET /api/auth/me

| Eset | Endpoint | Eredmény |
|------|----------|----------|
| Regisztráció | POST /api/auth/register | 201 Created |
| Bejelentkezés | POST /api/auth/login | 200 OK |
| Token megújítás | POST /api/auth/refresh | 200 OK |
| Kijelentkezés | POST /api/auth/logout | 200 OK |
| Profil lekérése | GET /api/auth/me | 200 OK |
| Jelszó változtatás | POST /api/auth/changepassword | 200 OK |
| Hibás jelszó | POST /api/auth/login | 400 Bad Request |
| Lejárt token | POST /api/auth/refresh | 400 Bad Request |
| Unauthorized | GET /api/auth/me (token nélkül) | 401 Unauthorized |


## Project CRUD Endpoints (2026-03-09)
Endpoints: POST, GET, PUT, PATCH, DELETE /api/project

| Eset | Endpoint | Eredmény |
|------|----------|----------|
| Projekt létrehozás | POST /api/project | 201 Created |
| Projektek listázása | GET /api/project | 200 OK |
| Projekt lekérése | GET /api/project/{id} | 200 OK |
| Projekt frissítése | PATCH /api/project/{id} | 200 OK |
| Projekt archiválása | PATCH /api/project/{id}/archive | 200 OK |
| Projekt törlése | DELETE /api/project/{id} | 204 NoContent |

## Task CRUD Endpoints (2026-03-09)
Endpoints: POST, GET, PATCH, DELETE /api/projects/{projectId}/tasks

| Eset | Endpoint | Eredmény |
|------|----------|----------|
| Task létrehozás | POST /api/projects/{projectId}/tasks | 201 Created |
| Taskok listázása | GET /api/projects/{projectId}/tasks | 200 OK |
| Task lekérése | GET /api/projects/{projectId}/tasks/{taskId} | 200 OK |
| Task frissítése | PATCH /api/projects/{projectId}/tasks/{taskId} | 200 OK |
| Task mozgatás | PATCH /api/projects/{projectId}/tasks/{taskId}/move | 200 OK |
| Task törlése | DELETE /api/projects/{projectId}/tasks/{taskId} | 204 NoContent |

##

