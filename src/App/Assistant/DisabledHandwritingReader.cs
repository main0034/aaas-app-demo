namespace App.Assistant;

public sealed class DisabledHandwritingReader : IHandwritingReader
{
    public Task<HandwritingResult> ReadAsync(string pngBase64, CancellationToken ct) =>
        Task.FromResult(new HandwritingResult(null, "Handwriting recognition needs the assistant enabled."));
}
