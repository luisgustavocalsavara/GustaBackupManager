namespace GustaBackupManager.App.Models;

public sealed record OperationResult(bool Success, string Message);

public sealed record OperationResult<T>(bool Success, string Message, T? Data = default);
