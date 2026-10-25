using System.Diagnostics;
using System.Text.Json;
using System.Xml.Linq;

namespace LmuStrategy.Architecture.Tests;

public sealed class ProjectBoundaryTests
{
    private static readonly string Root = FindRepositoryRoot();

    // Allowed edges, not required implementation details. New boundaries need deliberate review.
    public static TheoryData<string, string, string[]> Projects => new()
    {
        { "Domain", "net10.0", [] },
        { "Strategy", "net10.0", ["Domain"] },
        { "Simulation", "net10.0", ["Domain", "Strategy"] },
        { "Persistence", "net10.0", ["Domain"] },
        { "LmuAdapter", "net10.0", ["Domain"] },
        { "LmuAdapter.Windows", "net10.0-windows", ["Domain", "LmuAdapter"] },
        { "Overlay", "net10.0-windows", ["Domain", "Strategy"] },
        { "Windows", "net10.0-windows", ["Domain", "Strategy", "Simulation", "Persistence", "LmuAdapter", "LmuAdapter.Windows", "Overlay"] },
    };

    [Theory]
    [MemberData(nameof(Projects))]
    public async Task Evaluated_project_preserves_platform_and_dependency_boundaries(
        string name, string framework, string[] allowedReferences)
    {
        string path = Path.Combine(Root, "src", $"LmuStrategy.{name}", $"LmuStrategy.{name}.csproj");
        Assert.True(File.Exists(path), $"Missing foundation project: {path}");

        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = Root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (string argument in new[] { "msbuild", path, "-nologo",
            "-getProperty:TargetFramework,TargetFrameworks,UseWPF,UseWindowsForms",
            "-getItem:ProjectReference,FrameworkReference", "-p:EnableWindowsTargeting=true" })
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start)!;
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> errors = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw;
        }

        Assert.True(process.ExitCode == 0, await errors + await output);
        using var evaluation = JsonDocument.Parse(await output);
        JsonElement properties = evaluation.RootElement.GetProperty("Properties");
        Assert.Equal(framework, properties.GetProperty("TargetFramework").GetString());
        Assert.Equal("", properties.GetProperty("TargetFrameworks").GetString());

        JsonElement items = evaluation.RootElement.GetProperty("Items");
        var allowedPaths = allowedReferences.Select(reference => Path.GetFullPath(
            Path.Combine(Root, "src", $"LmuStrategy.{reference}", $"LmuStrategy.{reference}.csproj")));
        foreach (JsonElement reference in items.GetProperty("ProjectReference").EnumerateArray())
        {
            Assert.Contains(reference.GetProperty("FullPath").GetString(), allowedPaths);
        }

        if (framework == "net10.0")
        {
            Assert.NotEqual("true", properties.GetProperty("UseWPF").GetString());
            Assert.NotEqual("true", properties.GetProperty("UseWindowsForms").GetString());
            Assert.DoesNotContain(items.GetProperty("FrameworkReference").EnumerateArray(),
                reference => reference.GetProperty("Identity").GetString()!.Contains("WindowsDesktop"));
        }
        else if (name is "Windows" or "Overlay")
        {
            Assert.Equal("true", properties.GetProperty("UseWPF").GetString());
        }
    }

    [Fact]
    public void Linux_solution_contains_all_core_projects_and_tests_without_windows_projects()
    {
        string[] projects = SolutionProjects("LmuStrategy.Core.slnx");
        string[] expected = ["Domain", "Strategy", "Simulation", "Persistence", "LmuAdapter"];
        Assert.Equal(expected.Select(name => $"LmuStrategy.{name}").Order(),
            projects.Where(name => !name.EndsWith(".Tests", StringComparison.Ordinal)).Order());
        Assert.Contains("LmuStrategy.Architecture.Tests", projects);
    }

    [Fact]
    public void Full_solution_contains_every_source_and_test_project()
    {
        var actual = SolutionProjects("LmuStrategy.slnx").Order();
        var expected = Directory.EnumerateFiles(Path.Combine(Root, "src"), "*.csproj", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(Path.Combine(Root, "tests"), "*.csproj", SearchOption.AllDirectories))
            .Select(path => Path.GetFileNameWithoutExtension(path)).Order();
        Assert.Equal(expected, actual);
    }

    private static string[] SolutionProjects(string file) => XDocument.Load(Path.Combine(Root, file))
        .Descendants("Project").Select(project =>
            Path.GetFileNameWithoutExtension((string)project.Attribute("Path")!)).ToArray();

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "docs", "ENGINEERING.md")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}
