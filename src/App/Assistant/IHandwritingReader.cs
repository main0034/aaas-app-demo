namespace App.Assistant;

public interface IHandwritingReader
{
    Task<HandwritingResult> ReadAsync(string pngBase64, CancellationToken ct);
}

public record HandwritingResult(string? Text, string? Error);
