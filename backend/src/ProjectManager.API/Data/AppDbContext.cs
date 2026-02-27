using Microsoft.EntityFrameworkCore;
using ProjectManager.API.Model;

namespace ProjectManager.API.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        modelBuilder.Entity<UserRole>(entity =>
        {
            // Composite PK - nincs saját Id, a két FK együtt alkotja
            entity.HasKey(ur => new { ur.UserId, ur.RoleId });

            //UserRole - User kapcsolat
            entity.HasOne(ur => ur.User)
                .WithMany(u => u.UserRoles)
                .HasForeignKey(ur => ur.UserId);

            //UserRole - Role kapcsolat
            entity.HasOne(ur => ur.Role)
                .WithMany(r => r.UserRoles)
                .HasForeignKey(ur => ur.RoleId);
        });

        modelBuilder.Entity<ProjectMember>(entity =>
        {
            // Composite PK - nincs saját Id, a két FK együtt alkotja
            entity.HasKey(pm => new { pm.ProjectId, pm.UserId });

            // ProjectMember - Project kapcsolat
            entity.HasOne(pm => pm.Project)
                .WithMany(p => p.Members)
                .HasForeignKey(pm => pm.ProjectId);

            // ProjectMember - User kapcsolat
            entity.HasOne(pm => pm.User)
                .WithMany(u => u.ProjectMemberships)
                .HasForeignKey(pm => pm.UserId);

        });

        modelBuilder.Entity<Activity>(entity =>
        {
            //Activity - User (Actor)
            entity.HasOne(a => a.Actor)
                .WithMany()
                .HasForeignKey(a => a.ActorId);

            //Activity - Project
            entity.HasOne(a => a.Project)
                .WithMany()
                .HasForeignKey(a => a.ProjectId);
        });

        modelBuilder.Entity<ProjectCounter>(entity =>
        {
            //PK a ProjectId PK-ja
            entity.HasKey(pc => pc.ProjectId);

            //FK is a ProjectId: ProjectCounter - Project 
            entity.HasOne(pc => pc.Project)
                .WithOne(p => p.ProjectCounter)
                .HasForeignKey<ProjectCounter>(pc => pc.ProjectId);
        });

        modelBuilder.Entity<Integration>(entity =>
        {
            //Integration - Project
            entity.HasOne(i => i.Project)
                .WithMany(p => p.Integrations)
                .HasForeignKey(i => i.ProjectId);
        });

        modelBuilder.Entity<Board>(entity =>
        {
            //Board - Project
            entity.HasOne(b => b.Project)
                .WithMany(p => p.Boards)
                .HasForeignKey(b => b.ProjectId);
        });

        modelBuilder.Entity<ColumnDefinition>(entity =>
        {
            //CD - Board
            entity.HasOne(cf => cf.Board)
                .WithMany(b => b.ColumnDefinitions)
                .HasForeignKey(cf => cf.BoardId);
        });

        modelBuilder.Entity<Sprint>(entity =>
        {
            //Sprint - Project
            entity.HasOne(s => s.Project)
                .WithMany(p => p.Sprints)
                .HasForeignKey(s => s.ProjectId);

            //Sprint - Board
            entity.HasOne(s => s.Board)
                .WithMany(b => b.Sprints)
                .HasForeignKey(s => s.BoardId);
        });

        modelBuilder.Entity<ProjectTask>(entity =>
        {
            // Project - Tasks
            entity.HasOne(t => t.Project)
                .WithMany(p => p.ProjectTasks)
                .HasForeignKey(t => t.ProjectId);

            // Board - Tasks
            entity.HasOne(t => t.Board)
                .WithMany(b => b.ProjectTasks)
                .HasForeignKey(t => t.BoardId);

            // ColumnDefinition - Tasks (nullable!)
            entity.HasOne(t => t.ColumnDefinition)
                .WithMany(c => c.ProjectTasks)
                .HasForeignKey(t => t.ColumnId);

            // Sprint - Tasks (nullable!)
            entity.HasOne(t => t.Sprint)
                .WithMany(s => s.ProjectTasks)
                .HasForeignKey(t => t.SprintId);

            // CreatedBy User - Tasks
            entity.HasOne(t => t.CreatedByUser)
                .WithMany(u => u.CreatedTasks)
                .HasForeignKey(t => t.CreatedById);
        });

        modelBuilder.Entity<TaskAssignment>(entity =>
        {
            //Composite PK
            entity.HasKey(ta => new { ta.TaskId, ta.UserId });

            //TA - User
            entity.HasOne(ta => ta.User)
                .WithMany(u => u.Assignments)
                .HasForeignKey(ta => ta.UserId);

            //TA - Task
            entity.HasOne(ta => ta.ProjectTask)
                .WithMany(t => t.TaskAssignments)
                .HasForeignKey(ta => ta.TaskId);
        });

        modelBuilder.Entity<Label>(entity =>
        {
            //Label - Project
            entity.HasOne(l => l.Project)
                .WithMany(p => p.Labels)
                .HasForeignKey(l => l.ProjectId);
        });

        modelBuilder.Entity<LabelTask>(entity =>
        {
            //Composite PK
            entity.HasKey(lt => new { lt.TaskId, lt.LabelId });

            //LT - Label
            entity.HasOne(lt => lt.Label)
                .WithMany(l => l.LabelTasks)
                .HasForeignKey(lt => lt.LabelId);

            //LT - Task
            entity.HasOne(lt => lt.ProjectTask)
                .WithMany(t => t.AssignedLabels)
                .HasForeignKey(lt => lt.TaskId);
        });

        modelBuilder.Entity<Comment>(entity =>
        {
            //Comment - Task
            entity.HasOne(c => c.ProjectTask)
                .WithMany(t => t.CommentsOnTask)
                .HasForeignKey(c => c.TaskId);

            //Comment - User
            entity.HasOne(c => c.User)
                .WithMany(t => t.Comments)
                .HasForeignKey(c => c.UserId);
        });

        modelBuilder.Entity<Attachment>(entity =>
        {
            //Attachment - Project
            entity.HasOne(a => a.Project)
                .WithMany(p => p.Attachments)
                .HasForeignKey(a => a.ProjectId);

            //Attachment - Task (nullable!)
            entity.HasOne(a => a.ProjectTask)
                .WithMany(t => t.Attachments)
                .HasForeignKey(a => a.TaskId);

            //Attachment - User
            entity.HasOne(a => a.UploadedBy)
                .WithMany(u => u.UploadedFiles)
                .HasForeignKey(a => a.UploadedById);
        });

        modelBuilder.Entity<CommitLink>(entity =>
        {
            //CL - Task (nullable!)
            entity.HasOne(cl => cl.ProjektTask)
                .WithMany(t => t.CommitLinks)
                .HasForeignKey(cl => cl.TaskId);
        });

        modelBuilder.Entity<PrLink>(entity =>
        {
            //PRL - Task (nullable!)
            entity.HasOne(prl => prl.ProjectTask)
                .WithMany(t => t.PrLinks)
                .HasForeignKey(prl => prl.TaskId);
        });
    }
}