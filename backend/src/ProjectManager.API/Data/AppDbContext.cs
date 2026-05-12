using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using ProjectManager.API.Model;

namespace ProjectManager.API.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    //CreatedAt és UpdatedAt automatikus kezelése - triggerek helyett
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        //Speciális működés
        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Added)
            {
                // Új entitásnál mindkét mező beállítódik
                TrySetProperty(entry, "CreatedAt", now);
                TrySetProperty(entry, "UpdatedAt", now);
            }
            else if (entry.State == EntityState.Modified)
            {
                //Különben csak UpdatedAt
                TrySetProperty(entry, "UpdatedAt", now);
            }
        }
        return await base.SaveChangesAsync(cancellationToken);
    }

    private static void TrySetProperty(EntityEntry entry, string propertyName, object value)
    {
        var property = entry.Properties
            .FirstOrDefault(p => p.Metadata.Name == propertyName);
        if (property != null)
        {
            property.CurrentValue = value;
        }
    }


    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();
    public DbSet<Board> Boards => Set<Board>();
    public DbSet<ColumnDefinition> ColumnDefinitions => Set<ColumnDefinition>();
    public DbSet<Sprint> Sprints => Set<Sprint>();
    public DbSet<ProjectTask> ProjectTasks => Set<ProjectTask>();
    public DbSet<TaskAssignment> TaskAssignments => Set<TaskAssignment>();
    public DbSet<Label> Labels => Set<Label>();
    public DbSet<LabelTask> LabelTasks => Set<LabelTask>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<Integration> Integrations => Set<Integration>();
    public DbSet<CommitLink> CommitLinks => Set<CommitLink>();
    public DbSet<PrLink> PrLinks => Set<PrLink>();
    public DbSet<ProjectCounter> ProjectCounters => Set<ProjectCounter>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<ProjectInvite> ProjectInvites => Set<ProjectInvite>();
    public DbSet<TaskStatusHistory> TaskStatusHistories => Set<TaskStatusHistory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        modelBuilder.Entity<User>(entity =>
        {
            //Constraints
            entity.Property(u => u.Email)
                  .HasMaxLength(255)
                  .IsRequired();

            entity.Property(u => u.DisplayName)
                  .HasMaxLength(120)
                  .IsRequired();

            entity.Property(u => u.PasswordHash)
                  .IsRequired();

            entity.Property(u => u.IsActive)
                  .IsRequired()
                  .HasDefaultValue(true);

            entity.Property(u => u.CreatedAt)
                  .IsRequired();

            entity.Property(u => u.UpdatedAt)
                  .IsRequired();

            entity.HasIndex(u => u.Email)
                  .IsUnique();
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            //Constraints
            entity.Property(rt => rt.IsRevoked)
                  .IsRequired()
                  .HasDefaultValue(false);

            entity.Property(rt => rt.CreatedAt)
                  .IsRequired();

            entity.Property(rt => rt.Token)
                  .IsRequired();

            entity.Property(rt => rt.ExpiresAt)
                  .IsRequired();

            entity.Property(rt => rt.UserId)
                  .IsRequired();

            entity.HasOne(rt => rt.User)
                  .WithMany(u => u.RefreshTokens)
                  .HasForeignKey(rt => rt.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            //Indexes
            entity.HasIndex(rt => rt.Token)
                  .IsUnique();

            //Foreign keys
            entity.HasOne(rt => rt.User)
                  .WithMany(u => u.RefreshTokens)
                  .HasForeignKey(rt => rt.UserId);
        });
        
        modelBuilder.Entity<Role>(entity =>
        {
            //Constraints
            entity.Property(r => r.Name)
                  .HasMaxLength(64)
                  .IsRequired();

            entity.Property(r => r.CreatedAt)
                  .IsRequired();

            //Indexes
            entity.HasIndex(r => r.Name)
                  .IsUnique();
        });
        
        modelBuilder.Entity<UserRole>(entity =>
        {
            //Composite PK
            entity.HasKey(ur => new { ur.UserId, ur.RoleId });

            //Constraints
            entity.Property(ur => ur.AssignedAt)
                  .IsRequired();

            //Foreign keys
            entity.HasOne(ur => ur.User)
                  .WithMany(u => u.UserRoles)
                  .HasForeignKey(ur => ur.UserId);

            entity.HasOne(ur => ur.Role)
                  .WithMany(r => r.UserRoles)
                  .HasForeignKey(ur => ur.RoleId);

            //Indexes
            entity.HasIndex(ur => ur.RoleId);
        });
        
        modelBuilder.Entity<Project>(entity =>
        {
            //Constraints
            entity.Property(p => p.Name)
                  .HasMaxLength(140)
                  .IsRequired();

            entity.Property(p => p.ProjKey)
                  .HasMaxLength(16)
                  .IsRequired();

            entity.Property(p => p.IsArchived)
                  .IsRequired()
                  .HasDefaultValue(false);

            entity.Property(p => p.CreatedAt)
                  .IsRequired();

            entity.Property(p => p.UpdatedAt)
                  .IsRequired();

            //Indexes
            entity.HasIndex(p => p.ProjKey)
                  .IsUnique();

            entity.HasIndex(p => p.OwnerId);

            entity.HasIndex(p => p.IsArchived);

            //Foreign keys
            entity.HasOne(p => p.Owner)
                  .WithMany()
                  .HasForeignKey(p => p.OwnerId)
                  .OnDelete(DeleteBehavior.Restrict);
        });
        
        modelBuilder.Entity<ProjectMember>(entity =>
        {
            //Composite PK
            entity.HasKey(pm => new { pm.ProjectId, pm.UserId });

            //Constraints
            entity.Property(pm => pm.ProjectRole)
                  .HasMaxLength(32)
                  .IsRequired();

            entity.Property(pm => pm.JoinedAt)
                  .IsRequired();

            //Indexes
            entity.HasIndex(pm => pm.UserId);
            entity.HasIndex(pm => pm.ProjectRole);

            //Foreign keys
            entity.HasOne(pm => pm.Project)
                  .WithMany(p => p.Members)
                  .HasForeignKey(pm => pm.ProjectId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(pm => pm.User)
                  .WithMany(u => u.ProjectMemberships)
                  .HasForeignKey(pm => pm.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
        
        modelBuilder.Entity<Board>(entity =>
        {
            //Constraints
            entity.Property(b => b.Name)
                  .HasMaxLength(120)
                  .IsRequired();

            entity.Property(b => b.Description)
                  .HasMaxLength(500);

            entity.Property(b => b.IsDefault)
                  .IsRequired()
                  .HasDefaultValue(false);

            entity.Property(b => b.CreatedAt)
                  .IsRequired();

            //Indexes
            entity.HasIndex(b => b.ProjectId);
            entity.HasIndex(b => b.IsDefault);

            //Foreign keys
            entity.HasOne(b => b.Project)
                  .WithMany(p => p.Boards)
                  .HasForeignKey(b => b.ProjectId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
        
        modelBuilder.Entity<ColumnDefinition>(entity =>
        {
            //Constraints
            entity.Property(c => c.Name)
                  .HasMaxLength(80)
                  .IsRequired();

            entity.Property(c => c.MapsToStatus)
                  .HasMaxLength(32)
                  .IsRequired();

            entity.Property(c => c.Position)
                  .IsRequired()
                  .HasDefaultValue(0);

            // WipLimit szándékosan nincs IsRequired() — null = nincs limit

            //Indexes
            entity.HasIndex(c => new { c.BoardId, c.Name })
                  .IsUnique();

            entity.HasIndex(c => c.BoardId);

            //Foreign keys
            entity.HasOne(c => c.Board)
                  .WithMany(b => b.ColumnDefinitions)
                  .HasForeignKey(c => c.BoardId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
        
        modelBuilder.Entity<Sprint>(entity =>
        {
            //Constraints
            entity.Property(s => s.Name)
                  .HasMaxLength(80)
                  .IsRequired();

            entity.Property(s => s.State)
                  .HasMaxLength(16)
                  .IsRequired()
                  .HasDefaultValue("planned");

            entity.Property(s => s.CreatedAt)
                  .IsRequired();

            //Indexes
            entity.HasIndex(s => new { s.ProjectId, s.Name })
                  .IsUnique();

            entity.HasIndex(s => new { s.ProjectId, s.State });

            entity.HasIndex(s => new { s.ProjectId, s.StartDate, s.EndDate });
        
            // Partial unique index: projektenként csak 1 active sprint lehet
            entity.HasIndex(s => s.ProjectId)
                  .IsUnique()
                  .HasFilter("\"State\" = 'active'")
                  .HasDatabaseName("IX_Sprints_OneActiveSprint");

            //Foreign keys
            entity.HasOne(s => s.Project)
                  .WithMany(p => p.Sprints)
                  .HasForeignKey(s => s.ProjectId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
        
        modelBuilder.Entity<ProjectTask>(entity =>
        {
            //Constraints
            entity.Property(t => t.TaskKey)
                  .HasMaxLength(32)
                  .IsRequired();

            entity.Property(t => t.Title)
                  .HasMaxLength(200)
                  .IsRequired();

            entity.Property(t => t.Priority)
                  .HasMaxLength(16)
                  .HasDefaultValue("normal");

            entity.Property(t => t.Position)
                  .IsRequired()
                  .HasDefaultValue(0.0);

            entity.Property(t => t.EstimateInMinutes)
                  .HasDefaultValue(0);

            entity.Property(t => t.CreatedAt)
                  .IsRequired();

            entity.Property(t => t.UpdatedAt)
                  .IsRequired();

            //Indexes
            entity.HasIndex(t => new { t.ProjectId, t.TaskKey })
                  .IsUnique();
            
            entity.HasIndex(t => new { t.ProjectId, t.SprintId });
            entity.HasIndex(t => new { t.ColumnId, t.Position });
            entity.HasIndex(t => t.DueDate);
            entity.HasIndex(t => t.CreatedById);

            //Foreign keys
            entity.HasOne(t => t.Project)
                  .WithMany(p => p.ProjectTasks)
                  .HasForeignKey(t => t.ProjectId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(t => t.Board)
                  .WithMany(b => b.ProjectTasks)
                  .HasForeignKey(t => t.BoardId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(t => t.ColumnDefinition)
                  .WithMany(c => c.ProjectTasks)
                  .HasForeignKey(t => t.ColumnId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(t => t.Sprint)
                  .WithMany(s => s.ProjectTasks)
                  .HasForeignKey(t => t.SprintId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(t => t.CreatedByUser)
                  .WithMany(u => u.CreatedTasks)
                  .HasForeignKey(t => t.CreatedById)
                  .OnDelete(DeleteBehavior.Restrict);
        });
        
        modelBuilder.Entity<TaskAssignment>(entity =>
        {
            //Composite PK
            entity.HasKey(ta => new { ta.TaskId, ta.UserId });

            //Constraints
            entity.Property(ta => ta.AssignedAt)
                  .IsRequired();

            //Indexes
            entity.HasIndex(ta => ta.UserId);

            //Foreign keys
            entity.HasOne(ta => ta.ProjectTask)
                  .WithMany(t => t.TaskAssignments)
                  .HasForeignKey(ta => ta.TaskId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(ta => ta.User)
                  .WithMany(u => u.Assignments)
                  .HasForeignKey(ta => ta.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
        
        modelBuilder.Entity<Label>(entity =>
        {
            //Constraints
            entity.Property(l => l.Name)
                  .HasMaxLength(40)
                  .IsRequired();

            entity.Property(l => l.Color)
                  .HasMaxLength(7)
                  .IsRequired();

            //Indexes
            entity.HasIndex(l => new { l.ProjectId, l.Name })
                  .IsUnique();

            entity.HasIndex(l => l.ProjectId);

            //Foreign keys
            entity.HasOne(l => l.Project)
                  .WithMany(p => p.Labels)
                  .HasForeignKey(l => l.ProjectId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
        
        modelBuilder.Entity<LabelTask>(entity =>
        {
            //Composite PK
            entity.HasKey(lt => new { lt.TaskId, lt.LabelId });

            //Indexes
            entity.HasIndex(lt => lt.LabelId);

            //Foreign keys
            entity.HasOne(lt => lt.ProjectTask)
                  .WithMany(t => t.AssignedLabels)
                  .HasForeignKey(lt => lt.TaskId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(lt => lt.Label)
                  .WithMany(l => l.LabelTasks)
                  .HasForeignKey(lt => lt.LabelId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
        
        modelBuilder.Entity<Comment>(entity =>
        {
            //Constraints
            entity.Property(c => c.Body)
                  .IsRequired();

            entity.Property(c => c.CreatedAt)
                  .IsRequired();

            //Indexes
            entity.HasIndex(c => c.TaskId);
            entity.HasIndex(c => c.UserId);
            entity.HasIndex(c => c.CreatedAt);

            //Foreign keys
            entity.HasOne(c => c.ProjectTask)
                  .WithMany(t => t.CommentsOnTask)
                  .HasForeignKey(c => c.TaskId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(c => c.User)
                  .WithMany(u => u.Comments)
                  .HasForeignKey(c => c.UserId)
                  .OnDelete(DeleteBehavior.Restrict);
        });
        
        modelBuilder.Entity<Attachment>(entity =>
        {
            //Constraints
            entity.Property(a => a.FileName)
                  .HasMaxLength(255)
                  .IsRequired();

            entity.Property(a => a.ContentType)
                  .HasMaxLength(120)
                  .IsRequired();

            entity.Property(a => a.SizeBytes)
                  .IsRequired();

            entity.Property(a => a.StorageKey)
                  .IsRequired();

            entity.Property(a => a.AttachmentType)
                  .HasMaxLength(32)
                  .IsRequired()
                  .HasDefaultValue("other");

            entity.Property(a => a.CreatedAt)
                  .IsRequired();

            //Indexes
            entity.HasIndex(a => a.TaskId);
            entity.HasIndex(a => a.UploadedById);
            entity.HasIndex(a => new { a.TaskId, a.AttachmentType });

            //Foreign keys
            entity.HasOne(a => a.Project)
                  .WithMany(p => p.Attachments)
                  .HasForeignKey(a => a.ProjectId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(a => a.ProjectTask)
                  .WithMany(t => t.Attachments)
                  .HasForeignKey(a => a.TaskId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(a => a.UploadedBy)
                  .WithMany(u => u.UploadedFiles)
                  .HasForeignKey(a => a.UploadedById)
                  .OnDelete(DeleteBehavior.Restrict);
        });
        
        modelBuilder.Entity<Activity>(entity =>
        {
            //Constraints
            entity.Property(a => a.EntityType)
                  .HasMaxLength(32)
                  .IsRequired();

            entity.Property(a => a.Action)
                  .HasMaxLength(32)
                  .IsRequired(false);

            entity.Property(a => a.Payload)
                  .HasColumnType("text");

            entity.Property(a => a.Description)
                  .HasMaxLength(500)
                  .IsRequired();

            entity.Property(a => a.CreatedAt)
                  .IsRequired();

            //Indexes
            entity.HasIndex(a => new { a.ProjectId, a.CreatedAt });
            entity.HasIndex(a => new { a.EntityType, a.EntityId });

            //Foreign keys
            entity.HasOne(a => a.Project)
                  .WithMany()
                  .HasForeignKey(a => a.ProjectId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(a => a.Actor)
                  .WithMany()
                  .HasForeignKey(a => a.ActorId)
                  .OnDelete(DeleteBehavior.Restrict);
        });
        
        modelBuilder.Entity<Integration>(entity =>
        {
            entity.Property(i => i.Provider)
          .HasMaxLength(16)
          .IsRequired();

            entity.Property(i => i.RepoFullName)
                  .HasMaxLength(200)
                  .IsRequired();

            entity.Property(i => i.AccessToken)
                  .IsRequired(false);

            entity.Property(i => i.WebhookSecret)
                  .IsRequired();

            entity.Property(i => i.WebhookToken)
                  .HasMaxLength(64)
                  .IsRequired();

            entity.Property(i => i.IsEnabled)
                  .IsRequired()
                  .HasDefaultValue(true);

            entity.Property(i => i.IsVerified)
                  .IsRequired()
                  .HasDefaultValue(false);

            entity.Property(i => i.CreatedAt)
                  .IsRequired();

            entity.Property(i => i.UpdatedAt)
                  .IsRequired();

            //Egy projekthez providerenként + reponként csak egy integráció
            entity.HasIndex(i => new { i.ProjectId, i.Provider, i.RepoFullName })
                  .IsUnique();

            //gyors lookup webhook érkezésekor
            entity.HasIndex(i => i.WebhookToken)  
                  .IsUnique();

            entity.HasOne(i => i.Project)
                  .WithMany(p => p.Integrations)
                  .HasForeignKey(i => i.ProjectId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
        
        modelBuilder.Entity<CommitLink>(entity =>
        {
            entity.Property(cl => cl.CommitSha)
          .HasMaxLength(40)
          .IsRequired();

            entity.Property(cl => cl.AuthorEmail)
                  .HasMaxLength(255);

            entity.Property(cl => cl.AuthorName)
                  .HasMaxLength(100);

            entity.Property(cl => cl.CommittedAt)
                  .IsRequired();

            //Unique: egy commit egy integrationhoz csak egyszer
            entity.HasIndex(cl => new { cl.IntegrationId, cl.CommitSha })
                  .IsUnique();

            entity.HasOne(cl => cl.ProjectTask)
                  .WithMany(t => t.CommitLinks)
                  .HasForeignKey(cl => cl.TaskId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(cl => cl.Integration)
                  .WithMany(i => i.CommitLinks)
                  .HasForeignKey(cl => cl.IntegrationId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
        
        modelBuilder.Entity<PrLink>(entity =>
        {
            entity.Property(pl => pl.Title)
          .HasMaxLength(240);

            entity.Property(pl => pl.State)
                  .HasMaxLength(24)
                  .IsRequired()
                  .HasDefaultValue("open");

            entity.Property(pl => pl.AuthorName)
                  .HasMaxLength(100);

            entity.Property(pl => pl.CreatedAt)
                  .IsRequired();

            //Unique: egy PR egy integrationhoz csak egyszer
            entity.HasIndex(pl => new { pl.IntegrationId, pl.PrNumber })
                  .IsUnique();

            entity.HasIndex(pl => pl.State);

            entity.HasOne(pl => pl.ProjectTask)
                  .WithMany(t => t.PrLinks)
                  .HasForeignKey(pl => pl.TaskId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(pl => pl.Integration)
                  .WithMany(i => i.PrLinks)
                  .HasForeignKey(pl => pl.IntegrationId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
        
        modelBuilder.Entity<ProjectCounter>(entity =>
        {
            //PK - ProjectId egyben a PK és az FK is
            entity.HasKey(pc => pc.ProjectId);

            //Constraints
            entity.Property(pc => pc.LastNum)
                  .IsRequired()
                  .HasDefaultValue(0L);

            //Foreign keys
            entity.HasOne(pc => pc.Project)
                  .WithOne(p => p.ProjectCounter)
                  .HasForeignKey<ProjectCounter>(pc => pc.ProjectId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProjectInvite>(entity =>
        {
            entity.Property(i => i.Token)
                  .HasMaxLength(64)
                  .IsRequired();

            entity.HasIndex(i => i.Token)
                  .IsUnique();

            entity.Property(i => i.CreatedAt)
                  .IsRequired();

            entity.HasOne(i => i.Project)
                  .WithMany(p => p.Invites)
                  .HasForeignKey(i => i.ProjectId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(i => i.CreatedBy)
                  .WithMany()
                  .HasForeignKey(i => i.CreatedById)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TaskStatusHistory>(entity =>
        {
            entity.Property(h => h.Status)
                  .HasMaxLength(32)
                  .IsRequired();

            entity.Property(h => h.CreatedAt)
                  .IsRequired();

            //Index a CFD lekérdezésekhez
            entity.HasIndex(h => new { h.TaskId, h.CreatedAt });
            entity.HasIndex(h => new { h.ColumnId, h.CreatedAt });

            entity.HasOne(h => h.Task)
                  .WithMany(t => t.StatusHistory)
                  .HasForeignKey(h => h.TaskId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(h => h.Column)
                  .WithMany()
                  .HasForeignKey(h => h.ColumnId)
                  .OnDelete(DeleteBehavior.SetNull);
        });
    }
}