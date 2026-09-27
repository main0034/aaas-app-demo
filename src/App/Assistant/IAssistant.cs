namespace App.Assistant;

public interface IAssistant
{
    Task<AssistantResult> AskAsync(string question, CancellationToken ct);
}

public record AssistantResult(string? Answer, string? Error);
