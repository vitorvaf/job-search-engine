using Jobs.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Jobs.Infrastructure.Data;

public sealed class JobsDbContext : DbContext
{
    public JobsDbContext(DbContextOptions<JobsDbContext> options) : base(options) { }

    public DbSet<JobPostingEntity> JobPostings => Set<JobPostingEntity>();
    public DbSet<SourceEntity> Sources => Set<SourceEntity>();
    public DbSet<IngestionRunEntity> IngestionRuns => Set<IngestionRunEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<JobPostingEntity>(e =>
        {
            e.ToTable("job_postings");
            e.HasKey(x => x.Id);

            e.Property(x => x.SourceName).HasMaxLength(120).IsRequired();
            e.Property(x => x.SourceUrl).HasMaxLength(1024).IsRequired();
            e.Property(x => x.SourceJobId).HasMaxLength(255);
            e.Property(x => x.Title).HasMaxLength(400).IsRequired();
            e.Property(x => x.CompanyName).HasMaxLength(240).IsRequired();
            e.Property(x => x.CompanyWebsite).HasMaxLength(1024);
            e.Property(x => x.CompanyIndustry).HasMaxLength(120);
            e.Property(x => x.LocationText).HasMaxLength(240).IsRequired();
            e.Property(x => x.Country).HasMaxLength(8);
            e.Property(x => x.State).HasMaxLength(80);
            e.Property(x => x.City).HasMaxLength(120);
            e.Property(x => x.SalaryCurrency).HasMaxLength(10);
            e.Property(x => x.SalaryPeriod).HasMaxLength(32);
            e.Property(x => x.Fingerprint).HasMaxLength(80).IsRequired();
            e.Property(x => x.ClusterId).HasMaxLength(80);
            e.Property(x => x.MetadataJson).HasColumnType("jsonb").IsRequired();

            e.Property(x => x.Tags).HasColumnType("text[]");
            e.Property(x => x.Languages).HasColumnType("text[]");

            e.HasIndex(x => x.Fingerprint);
            e.HasIndex(x => x.SourceUrl)
                .IsUnique()
                .HasFilter("\"source_url\" IS NOT NULL AND \"source_url\" <> ''");

            // Evita duplicar a mesma vaga na mesma fonte (se SourceJobId existir)
            e.HasIndex(x => new { x.SourceName, x.SourceJobId })
                .IsUnique()
                .HasFilter("\"source_job_id\" IS NOT NULL");
        });

        modelBuilder.Entity<SourceEntity>(e =>
        {
            e.ToTable("sources");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(120).IsRequired();
            e.Property(x => x.BaseUrl).HasMaxLength(1024);
            e.Property(x => x.RateLimitPolicyJson).HasColumnType("jsonb").IsRequired();
            e.HasIndex(x => new { x.Name, x.Type }).IsUnique();
        });

        modelBuilder.Entity<IngestionRunEntity>(e =>
        {
            e.ToTable("ingestion_runs");
            e.HasKey(x => x.Id);
            e.Property(x => x.Status).HasMaxLength(40).IsRequired();
            e.Property(x => x.ErrorSample).HasMaxLength(4000);
            e.HasIndex(x => x.SourceId);
        });

        ApplySnakeCaseColumnNaming(modelBuilder);
    }

    private static void ApplySnakeCaseColumnNaming(ModelBuilder modelBuilder)
    {
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entity.GetProperties())
            {
                property.SetColumnName(ToSnakeCase(property.Name));
            }
        }
    }

    private static string ToSnakeCase(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        var chars = new List<char>(name.Length + 8);
        for (var i = 0; i < name.Length; i++)
        {
            var current = name[i];
            if (char.IsUpper(current))
            {
                var addSeparator = i > 0 &&
                                   (char.IsLower(name[i - 1]) ||
                                    (i + 1 < name.Length && char.IsLower(name[i + 1])));
                if (addSeparator)
                {
                    chars.Add('_');
                }

                chars.Add(char.ToLowerInvariant(current));
            }
            else
            {
                chars.Add(current);
            }
        }

        return new string(chars.ToArray());
    }
}
