using Microsoft.EntityFrameworkCore;
using ProjectManager.API.Data;
using ProjectManager.API.Model;

namespace ProjectManager.API.Data
{
    public class DbSeeder
    {
        public static async Task SeedAsync(AppDbContext context)
        {
            // Ha már van adat, ne fusson le újra
            if (await context.Users.AnyAsync()) return;

            // =====================================================
            // USERS
            // =====================================================
            // Jelszó mindkét usernél: "Password123!"
            // BCrypt hash — a 3. héten az AuthService fogja generálni élesben
            var owner = new User
            {
                Id = Guid.NewGuid(),
                Email = "owner@example.com",
                DisplayName = "Project Owner",
                PasswordHash = "$2a$11$TvZCPOH3fFNfomiegbovi.5DEbDdWsRpBqRMKGKjEJLlOBNqMHIWG",
                IsActive = true
            };

            var developer = new User
            {
                Id = Guid.NewGuid(),
                Email = "developer@example.com",
                DisplayName = "Developer One",
                PasswordHash = "$2a$11$TvZCPOH3fFNfomiegbovi.5DEbDdWsRpBqRMKGKjEJLlOBNqMHIWG",
                IsActive = true
            };

            var viewer = new User
            {
                Id = Guid.NewGuid(),
                Email = "viewer@example.com",
                DisplayName = "Viewer User",
                PasswordHash = "$2a$11$TvZCPOH3fFNfomiegbovi.5DEbDdWsRpBqRMKGKjEJLlOBNqMHIWG",
                IsActive = true
            };

            context.Users.AddRange(owner, developer, viewer);
            await context.SaveChangesAsync();

            // =====================================================
            // ROLES
            // =====================================================
            var adminRole = new Role
            {
                Id = Guid.NewGuid(),
                Name = "Admin",
                Description = "Full system access"
            };

            var developerRole = new Role
            {
                Id = Guid.NewGuid(),
                Name = "Developer",
                Description = "Standard developer access"
            };

            context.Roles.AddRange(adminRole, developerRole);
            await context.SaveChangesAsync();

            // =====================================================
            // USER ROLES
            // =====================================================
            context.UserRoles.AddRange(
                new UserRole { UserId = owner.Id, RoleId = adminRole.Id },
                new UserRole { UserId = developer.Id, RoleId = developerRole.Id },
                new UserRole { UserId = viewer.Id, RoleId = developerRole.Id }
            );
            await context.SaveChangesAsync();

            // =====================================================
            // PROJECT
            // =====================================================
            var project = new Project
            {
                Id = Guid.NewGuid(),
                Name = "Project Manager App",
                ProjKey = "PM",
                Description = "A full-stack project management application built with ASP.NET Core and Svelte.",
                OwnerId = owner.Id,
                IsArchived = false
            };

            context.Projects.Add(project);
            await context.SaveChangesAsync();

            // =====================================================
            // PROJECT COUNTER
            // =====================================================
            // Ezt azonnal fel kell venni a projekt után!
            // A task key generáláshoz szükséges
            var projectCounter = new ProjectCounter
            {
                ProjectId = project.Id,
                LastNum = 0
            };

            context.ProjectCounters.Add(projectCounter);
            await context.SaveChangesAsync();

            // =====================================================
            // PROJECT MEMBERS
            // =====================================================
            context.ProjectMembers.AddRange(
                new ProjectMember
                {
                    ProjectId = project.Id,
                    UserId = owner.Id,
                    ProjectRole = "Owner"
                },
                new ProjectMember
                {
                    ProjectId = project.Id,
                    UserId = developer.Id,
                    ProjectRole = "Member"
                },
                new ProjectMember
                {
                    ProjectId = project.Id,
                    UserId = viewer.Id,
                    ProjectRole = "Viewer"
                }
            );
            await context.SaveChangesAsync();

            // =====================================================
            // BOARD
            // =====================================================
            var board = new Board
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                Name = "Main Board",
                Description = "Default board for the PM project",
                IsDefault = true
            };

            context.Boards.Add(board);
            await context.SaveChangesAsync();

            // =====================================================
            // COLUMN DEFINITIONS
            // maps_to_status invariáns:
            // task.Status mindig egyezik a hozzá tartozó column MapsToStatus értékével
            // =====================================================
            var colTodo = new ColumnDefinition
            {
                Id = Guid.NewGuid(),
                BoardId = board.Id,
                Name = "To Do",
                MapsToStatus = "todo",
                WipLimit = null,    // nincs limit
                Position = 0
            };

            var colInProgress = new ColumnDefinition
            {
                Id = Guid.NewGuid(),
                BoardId = board.Id,
                Name = "In Progress",
                MapsToStatus = "inprogress",
                WipLimit = 3,       // max 3 task egyszerre
                Position = 1
            };

            var colReview = new ColumnDefinition
            {
                Id = Guid.NewGuid(),
                BoardId = board.Id,
                Name = "Review",
                MapsToStatus = "review",
                WipLimit = 2,
                Position = 2
            };

            var colDone = new ColumnDefinition
            {
                Id = Guid.NewGuid(),
                BoardId = board.Id,
                Name = "Done",
                MapsToStatus = "done",
                WipLimit = null,    // nincs limit
                Position = 3
            };

            context.ColumnDefinitions.AddRange(colTodo, colInProgress, colReview, colDone);
            await context.SaveChangesAsync();

            // =====================================================
            // SPRINT
            // =====================================================
            var sprint = new Sprint
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                BoardId = board.Id,
                Name = "Sprint 1",
                Goal = "Set up core backend infrastructure, database schema and authentication.",
                State = "active",
                StartDate = new DateTime(2026, 2, 22, 0, 0, 0, DateTimeKind.Utc),
                EndDate = new DateTime(2026, 3, 8, 0, 0, 0, DateTimeKind.Utc)
            };

            context.Sprints.Add(sprint);
            await context.SaveChangesAsync();

            // =====================================================
            // LABELS
            // =====================================================
            var labelBackend = new Label
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                Name = "backend",
                Color = "#3B82F6"   // kék
            };

            var labelFrontend = new Label
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                Name = "frontend",
                Color = "#8B5CF6"   // lila
            };

            var labelBug = new Label
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                Name = "bug",
                Color = "#EF4444"   // piros
            };

            var labelFeature = new Label
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                Name = "feature",
                Color = "#10B981"   // zöld
            };

            context.Labels.AddRange(labelBackend, labelFrontend, labelBug, labelFeature);
            await context.SaveChangesAsync();

            // =====================================================
            // TASKS
            // ProjectCounter.LastNum-ot manuálisan növeljük seed közben
            // Élesben az AtomicNextNum service metódus fogja kezelni
            // =====================================================

            // Task 1 — Done
            var task1 = new ProjectTask
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                BoardId = board.Id,
                ColumnId = colDone.Id,
                SprintId = sprint.Id,
                TaskKey = "PM-1",
                Title = "Docker Compose environment setup",
                Description = "Set up PostgreSQL container with volumes, networking and health checks.",
                Status = "done",            // egyezik colDone.MapsToStatus-szal
                Priority = "high",
                Position = 1.0f,
                EstimateInMinutes = 120,
                CreatedById = owner.Id,
                ClosedAt = new DateTime(2026, 2, 23, 0, 0, 0, DateTimeKind.Utc)
            };

            // Task 2 — Done
            var task2 = new ProjectTask
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                BoardId = board.Id,
                ColumnId = colDone.Id,
                SprintId = sprint.Id,
                TaskKey = "PM-2",
                Title = "Database schema design",
                Description = "Design full relational schema using dbdiagram.io. Define all entities, relations, indexes and constraints.",
                Status = "done",
                Priority = "high",
                Position = 2.0f,
                EstimateInMinutes = 180,
                CreatedById = owner.Id,
                ClosedAt = new DateTime(2026, 2, 25, 0, 0, 0, DateTimeKind.Utc)
            };

            // Task 3 — Done
            var task3 = new ProjectTask
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                BoardId = board.Id,
                ColumnId = colDone.Id,
                SprintId = sprint.Id,
                TaskKey = "PM-3",
                Title = "EF Core entity models and initial migration",
                Description = "Create all Code First entity classes, configure Fluent API constraints, indexes and relationships. Run initial migration.",
                Status = "done",
                Priority = "high",
                Position = 3.0f,
                EstimateInMinutes = 240,
                CreatedById = owner.Id,
                ClosedAt = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc)
            };

            // Task 4 — In Progress
            var task4 = new ProjectTask
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                BoardId = board.Id,
                ColumnId = colInProgress.Id,
                SprintId = sprint.Id,
                TaskKey = "PM-4",
                Title = "JWT Authentication implementation",
                Description = "Implement registration and login endpoints with BCrypt password hashing and JWT token generation.",
                Status = "inprogress",
                Priority = "high",
                Position = 1.0f,
                EstimateInMinutes = 180,
                CreatedById = owner.Id
            };

            // Task 5 — To Do
            var task5 = new ProjectTask
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                BoardId = board.Id,
                ColumnId = colTodo.Id,
                SprintId = sprint.Id,
                TaskKey = "PM-5",
                Title = "RBAC authorization policies",
                Description = "Implement role-based access control for project roles: Owner, Maintainer, Member, Viewer.",
                Status = "todo",
                Priority = "normal",
                Position = 1.0f,
                EstimateInMinutes = 120,
                CreatedById = owner.Id,
                DueDate = new DateTime(2026, 3, 8, 0, 0, 0, DateTimeKind.Utc)
            };

            // Task 6 — Backlog (nincs sprinthez rendelve)
            var task6 = new ProjectTask
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                BoardId = board.Id,
                ColumnId = null,        // backlog — nincs oszlop
                SprintId = null,        // backlog — nincs sprint
                TaskKey = "PM-6",
                Title = "Project and Task CRUD API",
                Description = "RESTful endpoints for project and task management with FluentValidation.",
                Status = "todo",
                Priority = "normal",
                Position = 1.0f,
                EstimateInMinutes = 300,
                CreatedById = owner.Id
            };

            context.ProjectTasks.AddRange(task1, task2, task3, task4, task5, task6);
            await context.SaveChangesAsync();

            // ProjectCounter frissítése — 6 task lett létrehozva
            projectCounter.LastNum = 6;
            await context.SaveChangesAsync();

            // =====================================================
            // TASK ASSIGNMENTS
            // =====================================================
            context.TaskAssignments.AddRange(
                new TaskAssignment { TaskId = task1.Id, UserId = owner.Id },
                new TaskAssignment { TaskId = task2.Id, UserId = owner.Id },
                new TaskAssignment { TaskId = task3.Id, UserId = owner.Id },
                new TaskAssignment { TaskId = task4.Id, UserId = developer.Id },
                new TaskAssignment { TaskId = task5.Id, UserId = developer.Id }
            );
            await context.SaveChangesAsync();

            // =====================================================
            // LABEL - TASK kapcsolatok
            // =====================================================
            context.LabelTasks.AddRange(
                new LabelTask { TaskId = task1.Id, LabelId = labelBackend.Id },
                new LabelTask { TaskId = task2.Id, LabelId = labelBackend.Id },
                new LabelTask { TaskId = task3.Id, LabelId = labelBackend.Id },
                new LabelTask { TaskId = task3.Id, LabelId = labelFeature.Id },
                new LabelTask { TaskId = task4.Id, LabelId = labelBackend.Id },
                new LabelTask { TaskId = task4.Id, LabelId = labelFeature.Id },
                new LabelTask { TaskId = task5.Id, LabelId = labelBackend.Id },
                new LabelTask { TaskId = task6.Id, LabelId = labelBackend.Id },
                new LabelTask { TaskId = task6.Id, LabelId = labelFrontend.Id }
            );
            await context.SaveChangesAsync();

            // =====================================================
            // COMMENTS
            // =====================================================
            context.Comments.AddRange(
                new Comment
                {
                    Id = Guid.NewGuid(),
                    TaskId = task3.Id,
                    UserId = owner.Id,
                    Body = "Migration completed successfully. All constraints and indexes applied."
                },
                new Comment
                {
                    Id = Guid.NewGuid(),
                    TaskId = task4.Id,
                    UserId = developer.Id,
                    Body = "BCrypt package added, starting on the AuthService implementation."
                }
            );
            await context.SaveChangesAsync();

            // =====================================================
            // ACTIVITY LOG
            // =====================================================
            context.Activities.AddRange(
                new Activity
                {
                    Id = Guid.NewGuid(),
                    ProjectId = project.Id,
                    ActorId = owner.Id,
                    EntityType = "Project",
                    EntityId = project.Id,
                    Action = "created",
                    Payload = "{\"name\": \"Project Manager App\"}"
                },
                new Activity
                {
                    Id = Guid.NewGuid(),
                    ProjectId = project.Id,
                    ActorId = owner.Id,
                    EntityType = "Task",
                    EntityId = task1.Id,
                    Action = "closed",
                    Payload = "{\"taskKey\": \"PM-1\", \"title\": \"Docker Compose environment setup\"}"
                },
                new Activity
                {
                    Id = Guid.NewGuid(),
                    ProjectId = project.Id,
                    ActorId = owner.Id,
                    EntityType = "Task",
                    EntityId = task4.Id,
                    Action = "moved",
                    Payload = "{\"from\": \"todo\", \"to\": \"inprogress\", \"taskKey\": \"PM-4\"}"
                }
            );
            await context.SaveChangesAsync();
        }
    }
}
