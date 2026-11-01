namespace Nexus.App.Models;

public sealed record TaskItem(long Id, string Title, bool IsCompleted, DateTime CreatedUtc, DateTime? CompletedUtc);
