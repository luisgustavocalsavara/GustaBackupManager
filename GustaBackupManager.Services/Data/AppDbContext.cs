using System.Security.Cryptography;
using System.Text;
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

    // New password hashes use PBKDF2 with an individual random salt.
    // The legacy SHA-256 format is still recognized by VerifyPassword so existing
    // installations can migrate transparently after a successful login.
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

    public static bool VerifyPassword(string password, string storedHash, out bool needsUpgrade)
    {
        needsUpgrade = false;
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(storedHash))
            return false;

        // Legacy v7 hashes: SHA-256 with a fixed salt.
        if (!storedHash.StartsWith("PBKDF2-SHA256$", StringComparison.Ordinal))
        {
            var legacy = SHA256.HashData(Encoding.UTF8.GetBytes($"GustaBackupSalt::{password}"));
            byte[] stored;
            try { stored = Convert.FromHexString(storedHash); }
            catch (FormatException) { return false; }

            var valid = stored.Length == legacy.Length && CryptographicOperations.FixedTimeEquals(stored, legacy);
            needsUpgrade = valid;
            return valid;
        }

        var parts = storedHash.Split('$');
        if (parts.Length != 4 || !int.TryParse(parts[1], out var iterations) || iterations < 100_000)
            return false;

        try
        {
            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);
            if (salt.Length < 16 || expected.Length != PasswordHashSize)
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

        // Garante criacao da tabela mesmo em bancos SQLite ja existentes de versoes anteriores
        db.Database.ExecuteSqlRaw(@"
            CREATE TABLE IF NOT EXISTS ""ConfiguracaoCategoriasPasta"" (
                ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_ConfiguracaoCategoriasPasta"" PRIMARY KEY AUTOINCREMENT,
                ""CategoryKey"" TEXT NOT NULL,
                ""Group"" TEXT NOT NULL,
                ""SubCategoryName"" TEXT NOT NULL,
                ""DisplayName"" TEXT NOT NULL,
                ""IconKey"" TEXT NOT NULL,
                ""IconColor"" TEXT NOT NULL,
                ""UpdatedAt"" TEXT NOT NULL
            );");

        db.Database.ExecuteSqlRaw(@"
            CREATE UNIQUE INDEX IF NOT EXISTS ""IX_ConfiguracaoCategoriasPasta_CategoryKey""
            ON ""ConfiguracaoCategoriasPasta"" (""CategoryKey"");");

        db.Database.ExecuteSqlRaw(@"
            CREATE TABLE IF NOT EXISTS ""IconesPersonalizados"" (
                ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_IconesPersonalizados"" PRIMARY KEY AUTOINCREMENT,
                ""Key"" TEXT NOT NULL,
                ""Label"" TEXT NOT NULL,
                ""SvgMarkup"" TEXT NOT NULL,
                ""SourceType"" TEXT NOT NULL,
                ""CreatedAt"" TEXT NOT NULL
            );");

        db.Database.ExecuteSqlRaw(@"
            CREATE UNIQUE INDEX IF NOT EXISTS ""IX_IconesPersonalizados_Key""
            ON ""IconesPersonalizados"" (""Key"");");

        db.Database.ExecuteSqlRaw(@"
            CREATE TABLE IF NOT EXISTS ""CatalogoDispositivos"" (
                ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_CatalogoDispositivos"" PRIMARY KEY AUTOINCREMENT,
                ""Category"" TEXT NOT NULL,
                ""DeviceType"" TEXT NOT NULL,
                ""Manufacturer"" TEXT NOT NULL,
                ""Model"" TEXT NOT NULL,
                ""Description"" TEXT NOT NULL,
                ""CreatedAt"" TEXT NOT NULL,
                ""UpdatedAt"" TEXT NOT NULL
            );");

        db.Database.ExecuteSqlRaw(@"
            CREATE UNIQUE INDEX IF NOT EXISTS ""IX_CatalogoDispositivos_Category_DeviceType_Manufacturer_Model""
            ON ""CatalogoDispositivos"" (""Category"", ""DeviceType"", ""Manufacturer"", ""Model"");");

        RemoveRepositoryCategoryColumn(db);
        MigrateProjectCatalogToFileTypes(db);

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

        db.Database.ExecuteSqlRaw(
            "DELETE FROM \"ConfiguracaoCategoriasPasta\" WHERE \"Group\" = 'Painel' OR \"CategoryKey\" IN ('Painel', 'Diversas');");

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

    private static void RemoveRepositoryCategoryColumn(AppDbContext db)
    {
        var hasCategoryColumn = db.Database.SqlQuery<int>(
                $"SELECT COUNT(*) AS Value FROM pragma_table_info('TiposArquivo') WHERE name = 'Category'")
            .Single() > 0;
        if (hasCategoryColumn)
            db.Database.ExecuteSqlRaw("ALTER TABLE \"TiposArquivo\" DROP COLUMN \"Category\";");
    }

    private static void MigrateProjectCatalogToFileTypes(AppDbContext db)
    {
        var hasProjectCatalog = db.Database.SqlQuery<int>(
                $"SELECT COUNT(*) AS Value FROM sqlite_master WHERE type = 'table' AND name = 'CatalogoProjetos'")
            .Single() > 0;
        if (!hasProjectCatalog)
            return;

        db.Database.ExecuteSqlRaw(@"
            INSERT INTO ""TiposArquivo"" (""Name"", ""Extensions"", ""Description"", ""IconKey"", ""IsActive"", ""CreatedAt"")
            SELECT p.""Name"",
                   p.""AllowedExtensions"", 
                   p.""Description"",
                   CASE p.""Category""
                       WHEN 'Clp' THEN 'Memory'
                       WHEN 'Ihm' THEN 'DesktopWindows'
                       WHEN 'Rede' THEN 'Hub'
                       ELSE 'Description'
                   END,
                   1,
                   p.""CreatedAt""
            FROM ""CatalogoProjetos"" AS p
            WHERE p.""Id"" = (
                SELECT MIN(p2.""Id"") FROM ""CatalogoProjetos"" AS p2
                WHERE lower(p2.""Name"") = lower(p.""Name"")
            )
            AND NOT EXISTS (
                SELECT 1 FROM ""TiposArquivo"" AS f
                WHERE lower(f.""Name"") = lower(p.""Name"")
            );");
        db.Database.ExecuteSqlRaw("DROP TABLE \"CatalogoProjetos\";");
    }
}
