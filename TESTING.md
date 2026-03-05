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

##
