namespace App.Assistant;

public sealed class DisabledAssistant : IAssistant
{
    public Task<AssistantResult> AskAsync(string question, CancellationToken ct) =>
        Task.FromResult(new AssistantResult(null, "AI assistant is not enabled."));
}
