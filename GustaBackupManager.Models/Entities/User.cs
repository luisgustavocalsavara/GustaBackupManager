namespace GustaBackupManager.Models.Entities;

public enum UserRole
{
    Manutencao = 0, // Visualiza pastas e baixa arquivos de backup
    Operacao = 1,   // Cria/edita pastas, envia arquivos e gerencia tipos de arquivo
    Admin = 2       // Acesso total + configuracao SMB e gerenciamento de usuarios
}

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Operacao;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
