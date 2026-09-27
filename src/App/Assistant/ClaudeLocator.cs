namespace App.Assistant;

// Finds the `claude` executable when Assistant:ClaudePath is a bare name.
//
// An IDE started from the Dock or Finder on macOS does not get the PATH a
// terminal has, so a bare "claude" that works from `dotnet run` in a shell is
// "not found" when the same app is started from Rider. Rather than make every
// launch profile carry a per-machine absolute path, look in PATH first and then
// in the places the Claude Code installers put it.
public static class ClaudeLocator
{
    public static string Resolve(
        string configured,
        string? pathVariable,
        string? homeDirectory,
        Func<string, bool> fileExists)
    {
        // An explicit path is taken as given, found or not: the caller asked for it.
        if (configured.Contains('/') || configured.Contains('\\'))
        {
            return configured;
        }

        foreach (var dir in CandidateDirectories(pathVariable, homeDirectory))
        {
            var candidate = Path.Combine(dir, configured);
            if (fileExists(candidate))
            {
                return candidate;
            }
        }

        return configured;
    }

    private static IEnumerable<string> CandidateDirectories(string? pathVariable, string? homeDirectory)
    {
        if (!string.IsNullOrEmpty(pathVariable))
        {
            foreach (var dir in pathVariable.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                yield return dir;
            }
        }

        if (!string.IsNullOrEmpty(homeDirectory))
        {
            yield return Path.Combine(homeDirectory, ".local", "bin");
            yield return Path.Combine(homeDirectory, ".claude", "local");
            yield return Path.Combine(homeDirectory, ".npm-global", "bin");
        }

        yield return "/opt/homebrew/bin";
        yield return "/usr/local/bin";
    }
}
