using ConnectoDb.Server.Models.Data;
using Microsoft.EntityFrameworkCore;

namespace ConnectoDb.Server.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(u => u.Id);
            entity.Property(u => u.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(u => u.FirstName).HasColumnName("firstname").HasMaxLength(256);
            entity.Property(u => u.LastName).HasColumnName("lastname").HasMaxLength(256);
            entity.Property(u => u.Username).HasColumnName("username").HasMaxLength(1024).IsRequired();
            entity.Property(u => u.PasswordHash).HasColumnName("passwordhash").HasMaxLength(2048).IsRequired();
            entity.Property(u => u.CreatedAt).HasColumnName("createdat").IsRequired();
            entity.Property(u => u.LastLoggedInAt).HasColumnName("lastloggedinat");
            entity.HasIndex(u => u.Username).IsUnique();
        });
    }
}
