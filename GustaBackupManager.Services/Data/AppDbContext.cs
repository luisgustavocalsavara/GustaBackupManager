using System.Security.Cryptography;
using GustaBackupManager.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GustaBackupManager.Services.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<FileTypeDefinition> FileTypes => Set<FileTypeDefinition>();
    public DbSet<FolderCategoryAppearance> CategoryAppearances => Set<FolderCategoryAppearance>();
    public DbSet<CustomIconDefinition> CustomIcons => Set<CustomIconDefinition>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Usuarios");
            entity.HasKey(u => u.Id);
            entity.HasIndex(u => u.Username).IsUnique();
            entity.Property(u => u.Username).IsRequired().HasMaxLength(50);
            entity.Property(u => u.FullName).IsRequired().HasMaxLength(100);
            entity.Property(u => u.Role).HasConversion<string>();
        });

        modelBuilder.Entity<FileTypeDefinition>(entity =>
        {
            entity.ToTable("TiposArquivo");
            entity.HasKey(f => f.Id);
            entity.HasIndex(f => f.Name).IsUnique();
            entity.Property(f => f.Name).IsRequired().HasMaxLength(120);
            entity.Property(f => f.Extensions).IsRequired().HasMaxLength(300);
            entity.Property(f => f.Description).HasMaxLength(300);
            entity.Property(f => f.IconKey).HasMaxLength(60);
        });

        modelBuilder.Entity<FolderCategoryAppearance>(entity =>
        {
            entity.ToTable("ConfiguracaoCategoriasPasta");
            entity.HasKey(c => c.Id);
            entity.HasIndex(c => c.CategoryKey).IsUnique();
            entity.Property(c => c.CategoryKey).IsRequired().HasMaxLength(80);
            entity.Property(c => c.Group).HasConversion<string>().IsRequired();
            entity.Property(c => c.SubCategoryName).HasMaxLength(80);
            entity.Property(c => c.DisplayName).IsRequired().HasMaxLength(140);
            entity.Property(c => c.IconKey).IsRequired().HasMaxLength(60);
            entity.Property(c => c.IconColor).IsRequired().HasMaxLength(40);
        });

        modelBuilder.Entity<CustomIconDefinition>(entity =>
        {
            entity.ToTable("IconesPersonalizados");
            entity.HasKey(i => i.Id);
            entity.HasIndex(i => i.Key).IsUnique();
            entity.Property(i => i.Key).IsRequired().HasMaxLength(80);
            entity.Property(i => i.Label).IsRequired().HasMaxLength(80);
            entity.Property(i => i.SvgMarkup).IsRequired();
            entity.Property(i => i.SourceType).IsRequired().HasMaxLength(30);
        });
    }

    private const int PasswordHashIterations = 600_000;
    private const int PasswordSaltSize = 16;
    private const int PasswordHashSize = 32;

    public static string HashPassword(string password)
    {
        ArgumentNullException.ThrowIfNull(password);

        var salt = RandomNumberGenerator.GetBytes(PasswordSaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            PasswordHashIterations,
            HashAlgorithmName.SHA256,
            PasswordHashSize);

        return $"PBKDF2-SHA256${PasswordHashIterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public static bool VerifyPassword(string password, string storedHash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(storedHash))
            return false;

        var parts = storedHash.Split('$');
        if (parts.Length != 4 || parts[0] != "PBKDF2-SHA256" ||
            !int.TryParse(parts[1], out var iterations) || iterations < 100_000)
            return false;

        try
        {
            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);
            if (salt.Length < PasswordSaltSize || expected.Length != PasswordHashSize)
                return false;

            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public static void InitializeDatabase(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        using var db = factory.CreateDbContext();

        db.Database.EnsureCreated();

        if (!db.Users.Any())
        {
            db.Users.AddRange(
                new User
                {
                    Username = "admin",
                    PasswordHash = HashPassword("admin123"),
                    FullName = "Administrador do Sistema",
                    Role = UserRole.Admin,
                    CreatedAt = DateTime.Now
                },
                new User
                {
                    Username = "operador",
                    PasswordHash = HashPassword("op123"),
                    FullName = "Engenharia / Operação",
                    Role = UserRole.Operacao,
                    CreatedAt = DateTime.Now
                },
                new User
                {
                    Username = "visualizador",
                    PasswordHash = HashPassword("vis123"),
                    FullName = "Técnico de Manutenção",
                    Role = UserRole.Manutencao,
                    CreatedAt = DateTime.Now
                });
            db.SaveChanges();
        }

        if (!db.FileTypes.Any())
        {
            db.FileTypes.AddRange(
                new FileTypeDefinition { Name = "Studio 5000", Extensions = ".acd;.l5x;.l5k", Description = "Rockwell Automation Studio 5000 Logix Designer", IconKey = "Memory" },
                new FileTypeDefinition { Name = "RSLogix 500", Extensions = ".rss;.ss5", Description = "Rockwell MicroLogix RSLogix 500", IconKey = "Memory" },
                new FileTypeDefinition { Name = "TIA Portal", Extensions = ".apxx;.zapxx;.s7p", Description = "Siemens TIA Portal", IconKey = "DeveloperBoard" },
                new FileTypeDefinition { Name = "CODESYS", Extensions = ".project;.projectarchive", Description = "CODESYS e projetos compatíveis", IconKey = "DeveloperBoard" },
                new FileTypeDefinition { Name = "FactoryTalk View", Extensions = ".apa;.mer", Description = "Rockwell FactoryTalk View", IconKey = "DesktopWindows" },
                new FileTypeDefinition { Name = "TIA WinCC", Extensions = ".apxx;.zapxx", Description = "WinCC no TIA Portal", IconKey = "TouchApp" },
                new FileTypeDefinition { Name = "RSNetwork", Extensions = ".rsn;.rsl", Description = "Rockwell RSNetwork", IconKey = "Hub" },
                new FileTypeDefinition { Name = "Inversores de Frequência", Extensions = ".ccwsln;.pf5;.dno;.par;.wps;.ssp;.xml;.csv", Description = "Parâmetros PowerFlex, SINAMICS, WEG CFW e Danfoss.", IconKey = "Speed" },
                new FileTypeDefinition { Name = "Robôs Industriais", Extensions = ".mod;.sys;.pgf;.cfg", Description = "Backups de robôs industriais.", IconKey = "PrecisionManufacturing" },
                new FileTypeDefinition { Name = "Documentos Diversos", Extensions = ".pdf;.docx;.xlsx;.txt", Description = "Documentação técnica diversa.", IconKey = "Description" },
                new FileTypeDefinition { Name = "Diagramas Elétricos", Extensions = ".pdf;.dwg;.zw1", Description = "Diagramas elétricos e arquivos EPLAN/CAD.", IconKey = "Architecture" },
                new FileTypeDefinition { Name = "Diagramas Pneumáticos", Extensions = ".pdf;.dwg", Description = "Diagramas pneumáticos.", IconKey = "Architecture" },
                new FileTypeDefinition { Name = "Manuais de Operação", Extensions = ".pdf;.docx", Description = "Manuais de operação.", IconKey = "MenuBook" },
                new FileTypeDefinition { Name = "Manuais Técnicos", Extensions = ".pdf;.docx", Description = "Manuais técnicos de equipamentos.", IconKey = "MenuBook" },
                new FileTypeDefinition { Name = "Folhetins e Informações", Extensions = ".pdf;.docx;.xlsx", Description = "Folhetins e informações técnicas.", IconKey = "Description" },
                new FileTypeDefinition { Name = "Lista de Spare Parts", Extensions = ".pdf;.xlsx;.csv", Description = "Listas de peças sobressalentes.", IconKey = "Inventory" },
                new FileTypeDefinition { Name = "Mídias", Extensions = ".jpg;.jpeg;.png;.mp4", Description = "Imagens e vídeos de referência.", IconKey = "PermMedia" });
            db.SaveChanges();
        }

        var existingKeys = db.CategoryAppearances
            .Select(c => c.CategoryKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missingCategories = FolderCategoryConfigService.BuildDefaultDefinitions()
            .Where(d => !existingKeys.Contains(d.CategoryKey))
            .ToList();

        if (missingCategories.Count > 0)
        {
            db.CategoryAppearances.AddRange(missingCategories);
            db.SaveChanges();
        }
    }

}
