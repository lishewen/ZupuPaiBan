using System.IO;
using Microsoft.EntityFrameworkCore;
using ZupuPaiBan.Models;

namespace ZupuPaiBan.Data;

public class ZupuDbContext : DbContext
{
    public DbSet<FamilyMember> Members => Set<FamilyMember>();

    private readonly string _dbPath;

    public ZupuDbContext()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "ZupuPaiBan");
        Directory.CreateDirectory(folder);
        _dbPath = Path.Combine(folder, "zupu.db");
    }

    public ZupuDbContext(DbContextOptions<ZupuDbContext> options) : base(options)
    {
        _dbPath = string.Empty;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseSqlite($"Data Source={_dbPath}");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FamilyMember>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(50);
            entity.Property(e => e.BirthDate).HasMaxLength(20);
            entity.Property(e => e.DeathDate).HasMaxLength(20);
            entity.Property(e => e.SpouseName).HasMaxLength(50);
            entity.Property(e => e.PhotoPath).HasMaxLength(500);
            entity.Property(e => e.Biography).HasMaxLength(2000);

            entity.HasOne(e => e.Father)
                  .WithMany(e => e.Children)
                  .HasForeignKey(e => e.FatherId)
                  .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
