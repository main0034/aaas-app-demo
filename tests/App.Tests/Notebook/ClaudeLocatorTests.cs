using App.Assistant;

namespace App.Tests.Notebook;

public sealed class ClaudeLocatorTests
{
    private static Func<string, bool> Exists(params string[] files) => files.Contains;

    [Fact]
    public void ExplicitPathIsReturnedUnchanged()
    {
        var result = ClaudeLocator.Resolve("/custom/claude", "/usr/bin", "/Users/m", Exists());
        Assert.Equal("/custom/claude", result);
    }

    [Fact]
    public void BareNameIsFoundOnPath()
    {
        var result = ClaudeLocator.Resolve("claude", "/usr/bin:/opt/tools", "/Users/m", Exists("/opt/tools/claude"));
        Assert.Equal("/opt/tools/claude", result);
    }

    [Fact]
    public void PathWinsOverWellKnownLocations()
    {
        var result = ClaudeLocator.Resolve(
            "claude", "/opt/tools", "/Users/m", Exists("/opt/tools/claude", "/Users/m/.local/bin/claude"));
        Assert.Equal("/opt/tools/claude", result);
    }

    [Fact]
    public void IdeWithMinimalPathFindsTheNativeInstallLocation()
    {
        // What an app started from the macOS Dock sees: no user directories on PATH.
        var result = ClaudeLocator.Resolve(
            "claude", "/usr/bin:/bin:/usr/sbin:/sbin", "/Users/m", Exists("/Users/m/.local/bin/claude"));
        Assert.Equal("/Users/m/.local/bin/claude", result);
    }

    [Fact]
    public void HomebrewLocationIsSearched()
    {
        var result = ClaudeLocator.Resolve("claude", null, null, Exists("/opt/homebrew/bin/claude"));
        Assert.Equal("/opt/homebrew/bin/claude", result);
    }

    [Fact]
    public void NotFoundAnywhereReturnsTheBareName()
    {
        var result = ClaudeLocator.Resolve("claude", "/usr/bin", "/Users/m", Exists());
        Assert.Equal("claude", result);
    }

    [Fact]
    public void ChildPathStartsWithTheCliDirectory()
    {
        var psi = ClaudeAssistant.BuildProcessStartInfo("/opt/homebrew/bin/claude", "/tmp");
        Assert.StartsWith("/opt/homebrew/bin" + Path.PathSeparator, psi.Environment["PATH"] + Path.PathSeparator);
    }

    [Fact]
    public void BareNameLeavesChildPathAlone()
    {
        var inherited = Environment.GetEnvironmentVariable("PATH");
        var psi = ClaudeAssistant.BuildProcessStartInfo("claude", "/tmp");
        Assert.Equal(inherited, psi.Environment["PATH"]);
    }
}
