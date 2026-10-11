using Domain.Entitites.ApplicationContextDb;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Infrastructure.Context
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public readonly IHttpContextAccessor _contextAccessor;

        public DbSet<AppFile> AppFile { get; set; }
        public DbSet<AppStoredFile> AppStoredFile { get; set; }
        public DbSet<StoredFile> StoredFile { get; set; }
        public DbSet<UserApiKey> UserApiKey { get; set; }
        public DbSet<ApplicationLog> ApplicationLog { get; set; }
        public DbSet<Trace> Trace { get; set; }
        public DbSet<ContextTrace> ContextTrace { get; set; }

        // DateTime columns become "timestamp without time zone" and accept DateTime.Now, as they did
        // in SQLite. Set here so the app and the dotnet ef tools build the same model.
        static ApplicationDbContext()
        {
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        }

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, IHttpContextAccessor contextAccessor) : base(options)
        {
            _contextAccessor = contextAccessor;
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<AppFile>(entity =>
            {
                entity.ToTable("AppFile");

                entity.HasKey(x => x.Id);

                entity.Property(x => x.Status)
                      .IsRequired()
                      .HasDefaultValue(0);

                entity.Property(x => x.StatusDetails)
                      .HasMaxLength(2048)
                      .IsRequired(false);

                entity.Property(x => x.StatusMessage)
                      .HasMaxLength(256)
                      .IsRequired(false);

                entity.Property(x => x.Deleted)
                      .IsRequired()
                      .HasDefaultValue(false);
            });

            modelBuilder.Entity<AppStoredFile>(entity =>
            {
                entity.ToTable("AppStoredFiles");

                entity.HasKey(x => x.Id);

                entity.Property(x => x.AppFileId)
                      .IsRequired();

                entity.Property(x => x.StoredFileId)
                      .IsRequired(false);

                entity.Property(x => x.UpdateDate)
                      .IsRequired();

                entity.HasOne(x => x.AppFile)
                      .WithMany()
                      .HasForeignKey(x => x.AppFileId)
                      .OnDelete(DeleteBehavior.Cascade)
                      .IsRequired(true);

                entity.HasOne(x => x.StoredFile)
                      .WithMany()
                      .HasForeignKey(x => x.StoredFileId)
                      .OnDelete(DeleteBehavior.SetNull)
                      .IsRequired(false);
            });

            modelBuilder.Entity<UserApiKey>(entity =>
            {
                entity.ToTable("UserApiKeys");

                entity.HasKey(x => x.Id);

                entity.Property(x => x.ApiKey)
                      .HasMaxLength(256)
                      .IsRequired();

                entity.Property(x => x.UserId)
                      .HasMaxLength(450)
                      .IsRequired();

                entity.Property(x => x.IsActive)
                      .IsRequired()
                      .HasDefaultValue(true);

                entity.Property(x => x.LastUsed)
                      .IsRequired();

                entity.HasIndex(x => x.ApiKey)
                      .IsUnique();

                entity.HasOne(x => x.User)
                      .WithMany()
                      .HasForeignKey(x => x.UserId)
                      .OnDelete(DeleteBehavior.Cascade)
                      .IsRequired(true);
            });

            modelBuilder.Entity<Trace>(entity =>
            {
                entity.ToTable("Traces");

                entity.HasKey(x => x.Id);

                entity.Property(x => x.Name)
                      .HasMaxLength(200)
                      .IsRequired(false);

                entity.Property(x => x.Description)
                      .HasMaxLength(1000)
                      .IsRequired(false);

                entity.Property(x => x.CreateDate)
                      .IsRequired();

                entity.Property(x => x.UpdateDate)
                      .IsRequired();

                entity.HasMany(x => x.Logs)
                      .WithOne(x => x.Trace)
                      .HasForeignKey(x => x.TraceId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(x => x.ContextTraces)
                      .WithOne(x => x.Trace)
                      .HasForeignKey(x => x.TraceId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<ApplicationLog>(entity =>
            {
                entity.ToTable("ApplicationLogs");

                entity.HasKey(x => x.Id);

                entity.Property(x => x.Message)
                      .HasMaxLength(4000)
                      .IsRequired();

                entity.Property(x => x.TraceId)
                      .IsRequired();

                entity.Property(x => x.Type)
                      .HasMaxLength(50)
                      .IsRequired();

                entity.Property(x => x.Action)
                      .HasMaxLength(50)
                      .IsRequired();

                entity.Property(x => x.CreateDate)
                      .IsRequired();

                entity.Property(x => x.UpdateDate)
                      .IsRequired();

                entity.HasOne(x => x.Trace)
                      .WithMany(x => x.Logs)
                      .HasForeignKey(x => x.TraceId)
                      .OnDelete(DeleteBehavior.Cascade)
                      .IsRequired(true);
            });

            modelBuilder.Entity<ContextTrace>(entity =>
            {
                entity.ToTable("ContextTraces");

                entity.HasKey(x => x.Id);

                entity.Property(x => x.TraceId)
                      .IsRequired();

                entity.Property(x => x.EntityName)
                      .HasMaxLength(100)
                      .IsRequired();

                entity.Property(x => x.EntityId)
                      .HasMaxLength(100)
                      .IsRequired();

                entity.Property(x => x.CreateDate)
                      .IsRequired();

                entity.Property(x => x.UpdateDate)
                      .IsRequired();

                entity.HasOne(x => x.Trace)
                      .WithMany(x => x.ContextTraces)
                      .HasForeignKey(x => x.TraceId)
                      .OnDelete(DeleteBehavior.Cascade)
                      .IsRequired(true);

                entity.HasIndex(x => new { x.EntityName, x.EntityId });
            });

            modelBuilder.Entity<StoredFile>(entity =>
            {
                entity.ToTable("StoredFile");

                entity.HasKey(x => x.Id);

                entity.Property(x => x.Deleted)
                      .IsRequired()
                      .HasDefaultValue(false);

            });
        }
    }
}
