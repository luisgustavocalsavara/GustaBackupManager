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

        var isFirstInitialization = !db.Users.Any();
        if (isFirstInitialization)
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

        var defaultFileTypes = new List<FileTypeDefinition>
        {
            new() { Name = "RSLogix 5", Extensions = ".rsp", Description = "Projetos de controladores Rockwell PLC-5.", IconKey = "Memory" },
            new() { Name = "RSLogix 500", Extensions = ".rss;.ach", Description = "Projetos de controladores Rockwell MicroLogix.", IconKey = "Memory" },
            new() { Name = "RSLogix/Studio5000", Extensions = ".acd;.l5k;.l5x", Description = "Projetos e arquivos de troca Logix Designer.", IconKey = "Memory" },
            new() { Name = "RSNetworx DeviceNET", Extensions = ".dnt;.dnb", Description = "Projetos de configuração de redes DeviceNet.", IconKey = "Hub" },
            new() { Name = "FactoryTalk Studio", Extensions = ".apa;.med;.mer", Description = "Aplicações, arquivos e runtimes FactoryTalk View.", IconKey = "DesktopWindows" },
            new() { Name = "Connected Components Workbench (CCW)", Extensions = ".ccwsln;.ccwproj;.ccwarchive", Description = "Soluções e projetos Connected Components Workbench.", IconKey = "DeveloperBoard" },
            new() { Name = "PanelBuilder32", Extensions = ".pba;.pva", Description = "Aplicações e arquivos de transferência PanelBuilder 32.", IconKey = "DesktopWindows" },
            new() { Name = "Tia Portal", Extensions = ".ap13;.ap14;.ap15;.ap16;.ap17;.ap18;.ap19;.ap20;.ap21;.zap13;.zap14;.zap15;.zap16;.zap17;.zap18;.zap19;.zap20;.zap21", Description = "Projetos e arquivos compactados Siemens TIA Portal.", IconKey = "DeveloperBoard" },
            new() { Name = "Wincc Flexible 2008", Extensions = ".hmi;.fwx;.pdz", Description = "Projetos e arquivos de runtime WinCC flexible.", IconKey = "TouchApp" },
            new() { Name = "Siemens Simatic Manager", Extensions = ".s7p;.s7l;.zap", Description = "Projetos SIMATIC STEP 7 / SIMATIC Manager.", IconKey = "DeveloperBoard" },
            new() { Name = "Sick Flexi Soft - Safety controllers", Extensions = ".fsp", Description = "Projetos de controladores de segurança SICK Flexi Soft.", IconKey = "Shield" },
            new() { Name = "schneider electric SoMachine", Extensions = ".project;.projectarchive;.sta", Description = "Projetos e arquivos de aplicação Schneider Electric SoMachine.", IconKey = "DeveloperBoard" },
            new() { Name = "schneider electric MachineExpert", Extensions = ".project;.projectarchive;.sta", Description = "Projetos e arquivos de aplicação Schneider Electric Machine Expert.", IconKey = "DeveloperBoard" },
            new() { Name = "ABB Robot Studio", Extensions = ".rsstn;.rspag;.mod;.sys;.cfg", Description = "Estações RobotStudio e arquivos de sistema/programa ABB.", IconKey = "PrecisionManufacturing" },
            new() { Name = "MS-Excel", Extensions = ".xls;.xlsx;.xlsm;.xlsb;.csv", Description = "Planilhas Microsoft Excel.", IconKey = "Description" },
            new() { Name = "MS-Word", Extensions = ".doc;.docx;.docm;.rtf", Description = "Documentos Microsoft Word.", IconKey = "Description" },
            new() { Name = "MS-PowerPoint", Extensions = ".ppt;.pptx;.pptm;.pps;.ppsx", Description = "Apresentações Microsoft PowerPoint.", IconKey = "Description" },
            new() { Name = "PDF", Extensions = ".pdf", Description = "Documentos no formato PDF.", IconKey = "Description" },
            new() { Name = "Midias", Extensions = ".jpg;.jpeg;.png;.gif;.bmp;.tif;.tiff;.mp4;.mov;.avi;.mp3;.wav", Description = "Imagens, vídeos e arquivos de áudio.", IconKey = "PermMedia" }
        };

        if (isFirstInitialization && !db.FileTypes.Any())
        {
            db.FileTypes.AddRange(defaultFileTypes);
            db.SaveChanges();
        }

        db.CategoryAppearances
            .Where(category => category.CategoryKey == "Projeto")
            .ExecuteDelete();

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
