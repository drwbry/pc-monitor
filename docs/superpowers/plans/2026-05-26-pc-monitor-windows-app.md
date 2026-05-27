# Marsh PC Monitor — Windows App Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a lightweight WPF cockpit app that shows live system health for a Windows PC, flags issues via a built-in rule engine, and orchestrates the existing PowerShell capture scripts (`diagnose.ps1`, `live-probe.ps1`) so the output lands in `Documents\SysLogs\` for Claude Code analysis.

**Architecture:** Two-project .NET 8 solution. `PcMonitor.Core` is a Windows-targeted class library (no WPF deps) holding sensor wrappers, the issue rule engine, hourly-history reader, and capture orchestration. `PcMonitor.App` is the thin WPF MVVM shell. `PcMonitor.Core.Tests` is xUnit. Issue evaluator manages per-rule `FirstSeen` state across ticks (rules just answer "condition holds now?"), so sustained rules work without an infinitely-growing rolling buffer.

**Tech Stack:** WPF on .NET 8 (`net8.0-windows10.0.19041.0`), CommunityToolkit.Mvvm, LibreHardwareMonitorLib, `System.Diagnostics.PerformanceCounter`, WMI/CIM via `System.Management`, xUnit + FluentAssertions for tests.

## Build environment

All `dotnet` commands in this plan run from **Windows PowerShell** (the .NET 8 SDK on Windows, with the Windows Desktop workload). The repo lives in WSL at `/home/dreux/projects/pc-monitor`; from Windows PowerShell it is reachable as `\\wsl$\Ubuntu\home\dreux\projects\pc-monitor\`. If build performance over the WSL share is unacceptable, clone the repo to a Windows-native path (e.g. `C:\src\pc-monitor`) and run commands there — the relative paths in this plan stay the same.

When a step says `Run: dotnet ...`, run it from a Windows PowerShell prompt with the working directory at the `app/` folder unless otherwise specified.

---

## Phase 1 — Solution scaffold and shared types

### Task 1: Solution and project scaffolding

**Files:**
- Create: `app/PcMonitor.sln`
- Create: `app/Directory.Build.props`
- Create: `app/src/PcMonitor.Core/PcMonitor.Core.csproj`
- Create: `app/src/PcMonitor.App/PcMonitor.App.csproj`
- Create: `app/tests/PcMonitor.Core.Tests/PcMonitor.Core.Tests.csproj`
- Create: `app/src/PcMonitor.App/App.xaml`, `App.xaml.cs` (minimal placeholders)

- [ ] **Step 1: Create `app/Directory.Build.props`**

```xml
<Project>
  <PropertyGroup>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
</Project>
```

- [ ] **Step 2: Create `app/src/PcMonitor.Core/PcMonitor.Core.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0-windows10.0.19041.0</TargetFramework>
    <RootNamespace>PcMonitor.Core</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="LibreHardwareMonitorLib" Version="0.9.4" />
    <PackageReference Include="System.Diagnostics.PerformanceCounter" Version="8.0.0" />
    <PackageReference Include="System.Diagnostics.EventLog" Version="8.0.0" />
    <PackageReference Include="System.Management" Version="8.0.0" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Create `app/src/PcMonitor.App/PcMonitor.App.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows10.0.19041.0</TargetFramework>
    <UseWPF>true</UseWPF>
    <RootNamespace>PcMonitor.App</RootNamespace>
    <ApplicationManifest>app.manifest</ApplicationManifest>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="CommunityToolkit.Mvvm" Version="8.3.2" />
    <ProjectReference Include="..\PcMonitor.Core\PcMonitor.Core.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 4: Create `app/src/PcMonitor.App/app.manifest` (asInvoker, not requireAdministrator)**

```xml
<?xml version="1.0" encoding="utf-8"?>
<assembly manifestVersion="1.0" xmlns="urn:schemas-microsoft-com:asm.v1">
  <trustInfo xmlns="urn:schemas-microsoft-com:asm.v3">
    <security>
      <requestedPrivileges xmlns="urn:schemas-microsoft-com:asm.v3">
        <requestedExecutionLevel level="asInvoker" uiAccess="false" />
      </requestedPrivileges>
    </security>
  </trustInfo>
</assembly>
```

- [ ] **Step 5: Create minimal `app/src/PcMonitor.App/App.xaml`**

```xml
<Application x:Class="PcMonitor.App.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" />
```

- [ ] **Step 6: Create minimal `app/src/PcMonitor.App/App.xaml.cs`**

```csharp
using System.Windows;

namespace PcMonitor.App;

public partial class App : Application
{
}
```

- [ ] **Step 7: Create `app/tests/PcMonitor.Core.Tests/PcMonitor.Core.Tests.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0-windows10.0.19041.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <RootNamespace>PcMonitor.Core.Tests</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
    <PackageReference Include="FluentAssertions" Version="6.12.1" />
    <ProjectReference Include="..\..\src\PcMonitor.Core\PcMonitor.Core.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 8: Create `app/PcMonitor.sln`**

From Windows PowerShell, working dir `app/`:

```powershell
dotnet new sln -n PcMonitor
dotnet sln add src/PcMonitor.Core/PcMonitor.Core.csproj
dotnet sln add src/PcMonitor.App/PcMonitor.App.csproj
dotnet sln add tests/PcMonitor.Core.Tests/PcMonitor.Core.Tests.csproj
```

- [ ] **Step 9: Restore and build**

Run: `dotnet build`
Expected: Build succeeds with 0 errors, 0 warnings.

- [ ] **Step 10: Run the (empty) test suite**

Run: `dotnet test`
Expected: 0 tests run, 0 failures.

- [ ] **Step 11: Commit**

```bash
git add app/
git commit -m "scaffold: PcMonitor solution with Core, App, and Core.Tests projects"
git push 2>/dev/null || echo "no remote configured; skip"
```

---

### Task 2: Core model records

**Files:**
- Create: `app/src/PcMonitor.Core/Models/SensorSnapshot.cs`
- Create: `app/src/PcMonitor.Core/Models/IssueState.cs`
- Create: `app/src/PcMonitor.Core/Models/CaptureResult.cs`
- Create: `app/src/PcMonitor.Core/Models/HourlyEntry.cs`
- Test: `app/tests/PcMonitor.Core.Tests/Models/ModelSmokeTests.cs`

- [ ] **Step 1: Create `Models/SensorSnapshot.cs`**

```csharp
namespace PcMonitor.Core.Models;

public sealed record SensorSnapshot(
    DateTimeOffset Timestamp,
    double? CpuPercent,
    double? CpuPackageTempC,
    bool? IsThrottling,
    double RamUsedGb,
    double RamTotalGb,
    double FreePhysicalRamPercent,
    double? CommitUsedPercent,
    double? PagefileUsedPercent,
    double? DiskQueueLength,
    double? DriveCFreeGb,
    int? EventErrorsLast5Minutes,
    int? EventErrorsThisHour,
    double? EventErrors24hHourlyAverage,
    IReadOnlyList<ProcessSample> TopProcesses);

public sealed record ProcessSample(
    int ProcessId,
    string Name,
    double CpuPercent,
    double RamMb);
```

- [ ] **Step 2: Create `Models/IssueState.cs`**

```csharp
namespace PcMonitor.Core.Models;

public enum IssueSeverity
{
    Yellow = 1,
    Red = 2,
}

public sealed record IssueState(
    string RuleId,
    IssueSeverity Severity,
    string Title,
    string Detail,
    DateTimeOffset FirstSeen,
    IReadOnlyDictionary<string, double?> Metrics);
```

- [ ] **Step 3: Create `Models/CaptureResult.cs`**

```csharp
namespace PcMonitor.Core.Models;

public enum CaptureKind
{
    Diagnostic,
    LiveProbe,
}

public sealed record CaptureResult(
    CaptureKind Kind,
    bool Success,
    bool Cancelled,
    int? ExitCode,
    string? WindowsPath,
    string? WslPath,
    string? StdErr);

public sealed record CaptureLine(
    DateTimeOffset Timestamp,
    bool IsStdErr,
    string Text);
```

- [ ] **Step 4: Create `Models/HourlyEntry.cs`**

DTO matching the JSON written by `files/collect-stats.ps1`. Fields are optional because older snapshots may omit some.

```csharp
namespace PcMonitor.Core.Models;

public sealed record HourlyEntry(
    DateTimeOffset Timestamp,
    double? CpuPercent,
    double? RamUsedGb,
    double? RamTotalGb,
    double? DriveCFreeGb,
    int? SystemErrorsLastHour,
    int? AppErrorsLastHour);
```

- [ ] **Step 5: Write `tests/PcMonitor.Core.Tests/Models/ModelSmokeTests.cs`**

```csharp
using FluentAssertions;
using PcMonitor.Core.Models;
using Xunit;

namespace PcMonitor.Core.Tests.Models;

public class ModelSmokeTests
{
    [Fact]
    public void SensorSnapshot_RoundTripsViaWith()
    {
        var s = new SensorSnapshot(
            DateTimeOffset.UnixEpoch, 12.5, 70, false,
            10, 64, 84, 30, 5, 0.3, 400, 0, 1, 0.5,
            Array.Empty<ProcessSample>());
        (s with { CpuPercent = 99 }).CpuPercent.Should().Be(99);
    }

    [Fact]
    public void IssueSeverity_RedIsGreaterThanYellow()
    {
        ((int)IssueSeverity.Red).Should().BeGreaterThan((int)IssueSeverity.Yellow);
    }
}
```

- [ ] **Step 6: Run tests**

Run: `dotnet test`
Expected: 2 passed, 0 failed.

- [ ] **Step 7: Commit**

```bash
git add app/
git commit -m "core: add SensorSnapshot, IssueState, CaptureResult, HourlyEntry models"
git push 2>/dev/null || echo "no remote configured; skip"
```

---

### Task 3: WSL path converter

**Files:**
- Create: `app/src/PcMonitor.Core/Capture/WslPathConverter.cs`
- Test: `app/tests/PcMonitor.Core.Tests/Capture/WslPathConverterTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
using FluentAssertions;
using PcMonitor.Core.Capture;
using Xunit;

namespace PcMonitor.Core.Tests.Capture;

public class WslPathConverterTests
{
    [Theory]
    [InlineData(@"C:\Users\dreux\Documents\SysLogs\diagnostic_x.txt",
                "/mnt/c/Users/dreux/Documents/SysLogs/diagnostic_x.txt")]
    [InlineData(@"D:\stuff\file.txt", "/mnt/d/stuff/file.txt")]
    [InlineData(@"c:\Users\My Name\file.txt", "/mnt/c/Users/My Name/file.txt")]
    public void ToWsl_ConvertsDriveLetterAndSlashes(string windowsPath, string expected)
    {
        WslPathConverter.ToWsl(windowsPath).Should().Be(expected);
    }

    [Fact]
    public void ToWsl_NullOrEmpty_ReturnsNull()
    {
        WslPathConverter.ToWsl(null).Should().BeNull();
        WslPathConverter.ToWsl("").Should().BeNull();
    }

    [Fact]
    public void ToWsl_PathWithoutDriveLetter_ReturnsNull()
    {
        WslPathConverter.ToWsl(@"\\server\share\file.txt").Should().BeNull();
    }
}
```

- [ ] **Step 2: Run tests to confirm they fail**

Run: `dotnet test --filter FullyQualifiedName~WslPathConverterTests`
Expected: Fails to compile — `WslPathConverter` does not exist.

- [ ] **Step 3: Implement `Capture/WslPathConverter.cs`**

```csharp
namespace PcMonitor.Core.Capture;

public static class WslPathConverter
{
    public static string? ToWsl(string? windowsPath)
    {
        if (string.IsNullOrEmpty(windowsPath)) return null;
        if (windowsPath.Length < 2 || windowsPath[1] != ':') return null;
        var drive = char.ToLowerInvariant(windowsPath[0]);
        if (drive < 'a' || drive > 'z') return null;
        var rest = windowsPath[2..].Replace('\\', '/');
        if (!rest.StartsWith('/')) rest = "/" + rest;
        return $"/mnt/{drive}{rest}";
    }
}
```

- [ ] **Step 4: Run tests to confirm pass**

Run: `dotnet test --filter FullyQualifiedName~WslPathConverterTests`
Expected: 5 passed.

- [ ] **Step 5: Commit**

```bash
git add app/
git commit -m "core: add WslPathConverter with drive-letter + space handling"
git push 2>/dev/null || echo "no remote configured; skip"
```

---

### Task 4: Hourly JSON parser

**Files:**
- Create: `app/src/PcMonitor.Core/History/HourlyJsonParser.cs`
- Test: `app/tests/PcMonitor.Core.Tests/History/HourlyJsonParserTests.cs`
- Test data: `app/tests/PcMonitor.Core.Tests/History/sample-hourly.json`

- [ ] **Step 1: Capture a real sample JSON from the existing collector**

Inspect `files/collect-stats.ps1` to confirm the JSON shape. Create `app/tests/PcMonitor.Core.Tests/History/sample-hourly.json` containing one realistic record. If the script's exact field names differ from the DTO, prefer the script's names and rename DTO fields to match (the spec says implementation may refine names).

Example shape (adjust to match the script):

```json
{
  "Timestamp": "2026-05-26T14:00:00-04:00",
  "CpuPercent": 12.5,
  "RamUsedGb": 14.2,
  "RamTotalGb": 64.0,
  "DriveCFreeGb": 412.0,
  "SystemErrorsLastHour": 0,
  "AppErrorsLastHour": 1
}
```

Add it as an embedded resource:

```xml
<!-- in PcMonitor.Core.Tests.csproj, ItemGroup -->
<EmbeddedResource Include="History\sample-hourly.json" />
```

- [ ] **Step 2: Write failing tests**

```csharp
using FluentAssertions;
using PcMonitor.Core.History;
using PcMonitor.Core.Models;
using Xunit;

namespace PcMonitor.Core.Tests.History;

public class HourlyJsonParserTests
{
    [Fact]
    public void Parse_ValidPayload_ReturnsEntry()
    {
        var json = """
        {
          "Timestamp": "2026-05-26T14:00:00-04:00",
          "CpuPercent": 12.5,
          "RamUsedGb": 14.2,
          "RamTotalGb": 64.0,
          "DriveCFreeGb": 412.0,
          "SystemErrorsLastHour": 0,
          "AppErrorsLastHour": 1
        }
        """;
        var entry = HourlyJsonParser.Parse(json);
        entry.Should().NotBeNull();
        entry!.CpuPercent.Should().Be(12.5);
        entry.RamTotalGb.Should().Be(64.0);
    }

    [Fact]
    public void Parse_MissingFields_PopulatesNulls()
    {
        var json = """{ "Timestamp": "2026-05-26T14:00:00-04:00" }""";
        var entry = HourlyJsonParser.Parse(json);
        entry.Should().NotBeNull();
        entry!.CpuPercent.Should().BeNull();
    }

    [Fact]
    public void Parse_Malformed_ReturnsNull()
    {
        HourlyJsonParser.Parse("{ not valid").Should().BeNull();
        HourlyJsonParser.Parse("").Should().BeNull();
    }
}
```

- [ ] **Step 3: Run tests to confirm failure**

Run: `dotnet test --filter FullyQualifiedName~HourlyJsonParserTests`
Expected: Fails to compile.

- [ ] **Step 4: Implement `History/HourlyJsonParser.cs`**

```csharp
using System.Text.Json;
using PcMonitor.Core.Models;

namespace PcMonitor.Core.History;

public static class HourlyJsonParser
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static HourlyEntry? Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<HourlyEntry>(json, Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
```

- [ ] **Step 5: Run tests to confirm pass**

Run: `dotnet test --filter FullyQualifiedName~HourlyJsonParserTests`
Expected: 3 passed.

- [ ] **Step 6: Commit**

```bash
git add app/
git commit -m "core: add HourlyJsonParser with schema-tolerant parsing"
git push 2>/dev/null || echo "no remote configured; skip"
```

---

### Task 5: Hourly history reader

**Files:**
- Create: `app/src/PcMonitor.Core/History/IHistoryReader.cs`
- Create: `app/src/PcMonitor.Core/History/HourlyHistoryReader.cs`
- Test: `app/tests/PcMonitor.Core.Tests/History/HourlyHistoryReaderTests.cs`

- [ ] **Step 1: Define interface `History/IHistoryReader.cs`**

```csharp
using PcMonitor.Core.Models;

namespace PcMonitor.Core.History;

public interface IHistoryReader
{
    IReadOnlyList<HourlyEntry> ReadAll();
    double? AverageHourlyErrorCount(int hoursBack = 24);
    event EventHandler? Changed;
}
```

- [ ] **Step 2: Write failing tests**

```csharp
using FluentAssertions;
using PcMonitor.Core.History;
using Xunit;

namespace PcMonitor.Core.Tests.History;

public class HourlyHistoryReaderTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "pcmon-tests-" + Guid.NewGuid());

    public HourlyHistoryReaderTests() => Directory.CreateDirectory(_dir);
    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void ReadAll_EmptyFolder_ReturnsEmpty()
    {
        var reader = new HourlyHistoryReader(_dir);
        reader.ReadAll().Should().BeEmpty();
    }

    [Fact]
    public void ReadAll_ReadsAllJsonFiles_SortedByTimestamp()
    {
        File.WriteAllText(Path.Combine(_dir, "stats_2026-05-26_14-00.json"),
            """{"Timestamp":"2026-05-26T14:00:00-04:00","CpuPercent":10}""");
        File.WriteAllText(Path.Combine(_dir, "stats_2026-05-26_13-00.json"),
            """{"Timestamp":"2026-05-26T13:00:00-04:00","CpuPercent":20}""");
        var reader = new HourlyHistoryReader(_dir);
        var entries = reader.ReadAll();
        entries.Should().HaveCount(2);
        entries[0].CpuPercent.Should().Be(20);
        entries[1].CpuPercent.Should().Be(10);
    }

    [Fact]
    public void ReadAll_SkipsMalformedFiles()
    {
        File.WriteAllText(Path.Combine(_dir, "stats_2026-05-26_14-00.json"),
            """{"Timestamp":"2026-05-26T14:00:00-04:00","CpuPercent":10}""");
        File.WriteAllText(Path.Combine(_dir, "stats_2026-05-26_15-00.json"), "{ not valid");
        var reader = new HourlyHistoryReader(_dir);
        reader.ReadAll().Should().HaveCount(1);
    }

    [Fact]
    public void AverageHourlyErrorCount_FewerThanThreshold_ReturnsNull()
    {
        File.WriteAllText(Path.Combine(_dir, "stats_a.json"),
            """{"Timestamp":"2026-05-26T14:00:00-04:00","SystemErrorsLastHour":5,"AppErrorsLastHour":3}""");
        var reader = new HourlyHistoryReader(_dir);
        reader.AverageHourlyErrorCount(hoursBack: 24).Should().BeNull();
    }

    [Fact]
    public void AverageHourlyErrorCount_AveragesErrorTotals()
    {
        for (var i = 0; i < 6; i++)
            File.WriteAllText(Path.Combine(_dir, $"stats_{i}.json"),
                $$"""{"Timestamp":"2026-05-26T1{{i}}:00:00-04:00","SystemErrorsLastHour":2,"AppErrorsLastHour":1}""");
        var reader = new HourlyHistoryReader(_dir);
        reader.AverageHourlyErrorCount(hoursBack: 24).Should().Be(3.0);
    }
}
```

- [ ] **Step 3: Run tests to confirm fail**

Run: `dotnet test --filter FullyQualifiedName~HourlyHistoryReaderTests`
Expected: Fails to compile.

- [ ] **Step 4: Implement `History/HourlyHistoryReader.cs`**

```csharp
using PcMonitor.Core.Models;

namespace PcMonitor.Core.History;

public sealed class HourlyHistoryReader : IHistoryReader, IDisposable
{
    private const int MinSamplesForAverage = 6;
    private readonly string _folder;
    private readonly FileSystemWatcher? _watcher;

    public event EventHandler? Changed;

    public HourlyHistoryReader(string folder, bool watch = false)
    {
        _folder = folder;
        if (watch && Directory.Exists(folder))
        {
            _watcher = new FileSystemWatcher(folder, "*.json")
            {
                IncludeSubdirectories = false,
                EnableRaisingEvents = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
            };
            _watcher.Created += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
            _watcher.Changed += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public IReadOnlyList<HourlyEntry> ReadAll()
    {
        if (!Directory.Exists(_folder)) return Array.Empty<HourlyEntry>();
        var entries = new List<HourlyEntry>();
        foreach (var file in Directory.EnumerateFiles(_folder, "*.json"))
        {
            try
            {
                var entry = HourlyJsonParser.Parse(File.ReadAllText(file));
                if (entry is not null) entries.Add(entry);
            }
            catch (IOException) { /* skip locked/partial files */ }
        }
        return entries.OrderByDescending(e => e.Timestamp).ToList();
    }

    public double? AverageHourlyErrorCount(int hoursBack = 24)
    {
        var cutoff = DateTimeOffset.UtcNow.AddHours(-hoursBack);
        var recent = ReadAll().Where(e => e.Timestamp >= cutoff).ToList();
        if (recent.Count < MinSamplesForAverage) return null;
        return recent.Average(e => (e.SystemErrorsLastHour ?? 0) + (e.AppErrorsLastHour ?? 0));
    }

    public void Dispose() => _watcher?.Dispose();
}
```

- [ ] **Step 5: Run tests to confirm pass**

Run: `dotnet test --filter FullyQualifiedName~HourlyHistoryReaderTests`
Expected: 5 passed.

- [ ] **Step 6: Commit**

```bash
git add app/
git commit -m "core: add HourlyHistoryReader with FileSystemWatcher and 24h error averaging"
git push 2>/dev/null || echo "no remote configured; skip"
```

---


## Phase 2 — Issue rules engine

### Task 6: `IIssueRule` interface and `RuleCheck` type

**Files:**
- Create: `app/src/PcMonitor.Core/Issues/IIssueRule.cs`
- Create: `app/src/PcMonitor.Core/Issues/RuleCheck.cs`

- [ ] **Step 1: Create `Issues/RuleCheck.cs`**

```csharp
namespace PcMonitor.Core.Issues;

public sealed record RuleCheck(
    bool ConditionMet,
    string? SubjectKey = null,
    string? Title = null,
    string? Detail = null,
    IReadOnlyDictionary<string, double?>? Metrics = null)
{
    public static RuleCheck NotMet { get; } = new(false);
}
```

- [ ] **Step 2: Create `Issues/IIssueRule.cs`**

```csharp
using PcMonitor.Core.Models;

namespace PcMonitor.Core.Issues;

public interface IIssueRule
{
    string RuleId { get; }
    IssueSeverity Severity { get; }
    TimeSpan SustainedFor { get; }
    RuleCheck Check(SensorSnapshot snapshot);
}
```

- [ ] **Step 3: Build and commit (no tests yet — exercised by Task 7+)**

Run: `dotnet build`
Expected: succeeds.

```bash
git add app/
git commit -m "core: add IIssueRule and RuleCheck"
git push 2>/dev/null || echo "no remote configured; skip"
```

---

### Task 7: `IssueEvaluator` with cross-tick state

**Files:**
- Create: `app/src/PcMonitor.Core/Issues/IssueEvaluator.cs`
- Test: `app/tests/PcMonitor.Core.Tests/Issues/IssueEvaluatorTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
using FluentAssertions;
using PcMonitor.Core.Issues;
using PcMonitor.Core.Models;
using Xunit;

namespace PcMonitor.Core.Tests.Issues;

public class IssueEvaluatorTests
{
    private static SensorSnapshot Snapshot(DateTimeOffset t) => new(
        t, 0, 0, false, 0, 0, 100, 0, 0, 0, 100, 0, 0, 0, Array.Empty<ProcessSample>());

    private sealed class TestRule : IIssueRule
    {
        public string RuleId { get; init; } = "test";
        public IssueSeverity Severity { get; init; } = IssueSeverity.Yellow;
        public TimeSpan SustainedFor { get; init; } = TimeSpan.Zero;
        public Func<SensorSnapshot, RuleCheck> CheckFn { get; init; } = _ => RuleCheck.NotMet;
        public RuleCheck Check(SensorSnapshot s) => CheckFn(s);
    }

    [Fact]
    public void NoRulesFiring_ReturnsEmpty()
    {
        var ev = new IssueEvaluator(new[] { new TestRule() });
        ev.Evaluate(Snapshot(DateTimeOffset.UnixEpoch)).Should().BeEmpty();
    }

    [Fact]
    public void ImmediateRule_FiresOnFirstMatch()
    {
        var rule = new TestRule
        {
            CheckFn = _ => new RuleCheck(true, Title: "t", Detail: "d"),
        };
        var ev = new IssueEvaluator(new[] { rule });
        var issues = ev.Evaluate(Snapshot(DateTimeOffset.UnixEpoch));
        issues.Should().ContainSingle().Which.Title.Should().Be("t");
    }

    [Fact]
    public void SustainedRule_DoesNotFireBeforeDurationElapses()
    {
        var rule = new TestRule
        {
            SustainedFor = TimeSpan.FromSeconds(30),
            CheckFn = _ => new RuleCheck(true, Title: "t"),
        };
        var ev = new IssueEvaluator(new[] { rule });
        var t0 = DateTimeOffset.UnixEpoch;
        ev.Evaluate(Snapshot(t0)).Should().BeEmpty();
        ev.Evaluate(Snapshot(t0.AddSeconds(29))).Should().BeEmpty();
    }

    [Fact]
    public void SustainedRule_FiresAtDurationBoundary()
    {
        var rule = new TestRule
        {
            SustainedFor = TimeSpan.FromSeconds(30),
            CheckFn = _ => new RuleCheck(true, Title: "t"),
        };
        var ev = new IssueEvaluator(new[] { rule });
        var t0 = DateTimeOffset.UnixEpoch;
        ev.Evaluate(Snapshot(t0));
        ev.Evaluate(Snapshot(t0.AddSeconds(30))).Should().ContainSingle();
    }

    [Fact]
    public void SustainedRule_PreservesFirstSeenAcrossTicks()
    {
        var rule = new TestRule
        {
            SustainedFor = TimeSpan.FromSeconds(30),
            CheckFn = _ => new RuleCheck(true, Title: "t"),
        };
        var ev = new IssueEvaluator(new[] { rule });
        var t0 = DateTimeOffset.UnixEpoch;
        ev.Evaluate(Snapshot(t0));
        var fired = ev.Evaluate(Snapshot(t0.AddSeconds(45))).Single();
        fired.FirstSeen.Should().Be(t0);
    }

    [Fact]
    public void ConditionBreaksThenReturns_ResetsFirstSeen()
    {
        var match = true;
        var rule = new TestRule
        {
            SustainedFor = TimeSpan.FromSeconds(30),
            CheckFn = _ => match ? new RuleCheck(true, Title: "t") : RuleCheck.NotMet,
        };
        var ev = new IssueEvaluator(new[] { rule });
        var t0 = DateTimeOffset.UnixEpoch;
        ev.Evaluate(Snapshot(t0));
        match = false;
        ev.Evaluate(Snapshot(t0.AddSeconds(10)));
        match = true;
        ev.Evaluate(Snapshot(t0.AddSeconds(20))).Should().BeEmpty();
        var fired = ev.Evaluate(Snapshot(t0.AddSeconds(50))).Single();
        fired.FirstSeen.Should().Be(t0.AddSeconds(20));
    }

    [Fact]
    public void DifferentSubjectKeys_TrackIndependently()
    {
        var subject = "A";
        var rule = new TestRule
        {
            SustainedFor = TimeSpan.FromSeconds(30),
            CheckFn = _ => new RuleCheck(true, SubjectKey: subject, Title: "t"),
        };
        var ev = new IssueEvaluator(new[] { rule });
        var t0 = DateTimeOffset.UnixEpoch;
        ev.Evaluate(Snapshot(t0));
        subject = "B";
        ev.Evaluate(Snapshot(t0.AddSeconds(45))).Should().BeEmpty();
        ev.Evaluate(Snapshot(t0.AddSeconds(80))).Should().ContainSingle().Which.FirstSeen
            .Should().Be(t0.AddSeconds(45));
    }
}
```

- [ ] **Step 2: Run to confirm failure**

Run: `dotnet test --filter FullyQualifiedName~IssueEvaluatorTests`
Expected: Fails to compile.

- [ ] **Step 3: Implement `Issues/IssueEvaluator.cs`**

```csharp
using PcMonitor.Core.Models;

namespace PcMonitor.Core.Issues;

public sealed class IssueEvaluator
{
    private readonly IReadOnlyList<IIssueRule> _rules;
    private readonly Dictionary<(string RuleId, string? Subject), DateTimeOffset> _activeSince = new();

    public IssueEvaluator(IEnumerable<IIssueRule> rules)
    {
        _rules = rules.ToList();
    }

    public IReadOnlyList<IssueState> Evaluate(SensorSnapshot snapshot)
    {
        var stillActive = new HashSet<(string, string?)>();
        var emitted = new List<IssueState>();

        foreach (var rule in _rules)
        {
            var check = rule.Check(snapshot);
            if (!check.ConditionMet) continue;

            var key = (rule.RuleId, check.SubjectKey);
            if (!_activeSince.TryGetValue(key, out var firstSeen))
            {
                firstSeen = snapshot.Timestamp;
                _activeSince[key] = firstSeen;
            }
            stillActive.Add(key);

            if (snapshot.Timestamp - firstSeen >= rule.SustainedFor)
            {
                emitted.Add(new IssueState(
                    rule.RuleId,
                    rule.Severity,
                    check.Title ?? "",
                    check.Detail ?? "",
                    firstSeen,
                    check.Metrics ?? new Dictionary<string, double?>()));
            }
        }

        foreach (var stale in _activeSince.Keys.Where(k => !stillActive.Contains(k)).ToList())
        {
            _activeSince.Remove(stale);
        }

        return emitted
            .OrderByDescending(i => i.Severity)
            .ThenBy(i => i.FirstSeen)
            .ToList();
    }
}
```

- [ ] **Step 4: Run tests**

Run: `dotnet test --filter FullyQualifiedName~IssueEvaluatorTests`
Expected: 7 passed.

- [ ] **Step 5: Commit**

```bash
git add app/
git commit -m "core: add IssueEvaluator with cross-tick FirstSeen tracking and subject keys"
git push 2>/dev/null || echo "no remote configured; skip"
```

---

### Task 8: Red issue rules

All six Red rules in one task. Tests follow a shared shape: instantiate the rule, build a `SensorSnapshot`, call `Check`, assert.

**Files:**
- Create: `app/src/PcMonitor.Core/Issues/Rules/ThermalThrottleRule.cs`
- Create: `app/src/PcMonitor.Core/Issues/Rules/CpuPackageTempHighRule.cs`
- Create: `app/src/PcMonitor.Core/Issues/Rules/CommitNearExhaustionRule.cs`
- Create: `app/src/PcMonitor.Core/Issues/Rules/DriveCCriticalRule.cs`
- Create: `app/src/PcMonitor.Core/Issues/Rules/RunawayProcessRule.cs`
- Create: `app/src/PcMonitor.Core/Issues/Rules/EventLogSpikeRule.cs`
- Create: `app/tests/PcMonitor.Core.Tests/Issues/Rules/SnapshotBuilder.cs`
- Create: `app/tests/PcMonitor.Core.Tests/Issues/Rules/RedRuleTests.cs`

- [ ] **Step 1: Create test helper `Issues/Rules/SnapshotBuilder.cs`**

```csharp
using PcMonitor.Core.Models;

namespace PcMonitor.Core.Tests.Issues.Rules;

public static class SnapshotBuilder
{
    public static SensorSnapshot Default(
        DateTimeOffset? t = null,
        double? cpuPct = 5,
        double? tempC = 60,
        bool? throttling = false,
        double freePhysRamPct = 50,
        double? commitPct = 40,
        double? pagefilePct = 10,
        double? diskQ = 0.2,
        double? driveCFree = 400,
        int? errLast5 = 0,
        int? errThisHour = 0,
        double? errAvg24h = 1,
        IReadOnlyList<ProcessSample>? procs = null)
        => new(
            t ?? DateTimeOffset.UnixEpoch,
            cpuPct, tempC, throttling,
            10, 64, freePhysRamPct,
            commitPct, pagefilePct, diskQ, driveCFree,
            errLast5, errThisHour, errAvg24h,
            procs ?? Array.Empty<ProcessSample>());
}
```

- [ ] **Step 2: Implement the six rules**

`Issues/Rules/ThermalThrottleRule.cs`:

```csharp
using PcMonitor.Core.Models;

namespace PcMonitor.Core.Issues.Rules;

public sealed class ThermalThrottleRule : IIssueRule
{
    public string RuleId => "thermal-throttle-active";
    public IssueSeverity Severity => IssueSeverity.Red;
    public TimeSpan SustainedFor => TimeSpan.Zero;

    public RuleCheck Check(SensorSnapshot s)
    {
        if (s.IsThrottling != true) return RuleCheck.NotMet;
        var detail = s.CpuPackageTempC.HasValue
            ? $"CPU package at {s.CpuPackageTempC:F0}°C; PROCHOT detected."
            : "PROCHOT detected.";
        return new RuleCheck(true,
            Title: "Thermal throttle active",
            Detail: detail,
            Metrics: new Dictionary<string, double?> { ["temp_c"] = s.CpuPackageTempC });
    }
}
```

`Issues/Rules/CpuPackageTempHighRule.cs`:

```csharp
using PcMonitor.Core.Models;

namespace PcMonitor.Core.Issues.Rules;

public sealed class CpuPackageTempHighRule : IIssueRule
{
    public string RuleId => "cpu-temp-critical";
    public IssueSeverity Severity => IssueSeverity.Red;
    public TimeSpan SustainedFor => TimeSpan.FromSeconds(30);

    public RuleCheck Check(SensorSnapshot s)
    {
        if (s.CpuPackageTempC is not double t || t < 95) return RuleCheck.NotMet;
        return new RuleCheck(true,
            Title: "CPU package temperature critical",
            Detail: $"{t:F0}°C",
            Metrics: new Dictionary<string, double?> { ["temp_c"] = t });
    }
}
```

`Issues/Rules/CommitNearExhaustionRule.cs`:

```csharp
using PcMonitor.Core.Models;

namespace PcMonitor.Core.Issues.Rules;

public sealed class CommitNearExhaustionRule : IIssueRule
{
    public string RuleId => "commit-near-exhaustion";
    public IssueSeverity Severity => IssueSeverity.Red;
    public TimeSpan SustainedFor => TimeSpan.Zero;

    public RuleCheck Check(SensorSnapshot s)
    {
        if (s.CommitUsedPercent is not double c || c < 95) return RuleCheck.NotMet;
        return new RuleCheck(true,
            Title: "RAM commit near exhaustion",
            Detail: $"Committed {c:F0}% of limit — system is paging.",
            Metrics: new Dictionary<string, double?> { ["commit_pct"] = c });
    }
}
```

`Issues/Rules/DriveCCriticalRule.cs`:

```csharp
using PcMonitor.Core.Models;

namespace PcMonitor.Core.Issues.Rules;

public sealed class DriveCCriticalRule : IIssueRule
{
    public string RuleId => "drive-c-critical";
    public IssueSeverity Severity => IssueSeverity.Red;
    public TimeSpan SustainedFor => TimeSpan.Zero;

    public RuleCheck Check(SensorSnapshot s)
    {
        if (s.DriveCFreeGb is not double gb || gb >= 5) return RuleCheck.NotMet;
        return new RuleCheck(true,
            Title: "Drive C: critically full",
            Detail: $"{gb:F1} GB free.",
            Metrics: new Dictionary<string, double?> { ["free_gb"] = gb });
    }
}
```

`Issues/Rules/RunawayProcessRule.cs`:

```csharp
using PcMonitor.Core.Models;

namespace PcMonitor.Core.Issues.Rules;

public sealed class RunawayProcessRule : IIssueRule
{
    public string RuleId => "runaway-process";
    public IssueSeverity Severity => IssueSeverity.Red;
    public TimeSpan SustainedFor => TimeSpan.FromMinutes(5);

    public RuleCheck Check(SensorSnapshot s)
    {
        var top = s.TopProcesses.OrderByDescending(p => p.CpuPercent).FirstOrDefault();
        if (top is null || top.CpuPercent <= 50) return RuleCheck.NotMet;
        return new RuleCheck(true,
            SubjectKey: $"{top.Name}:{top.ProcessId}",
            Title: $"{top.Name} high CPU",
            Detail: $"{top.CpuPercent:F0}% CPU",
            Metrics: new Dictionary<string, double?> { ["cpu_pct"] = top.CpuPercent });
    }
}
```

`Issues/Rules/EventLogSpikeRule.cs`:

```csharp
using PcMonitor.Core.Models;

namespace PcMonitor.Core.Issues.Rules;

public sealed class EventLogSpikeRule : IIssueRule
{
    public string RuleId => "event-log-spike";
    public IssueSeverity Severity => IssueSeverity.Red;
    public TimeSpan SustainedFor => TimeSpan.Zero;

    public RuleCheck Check(SensorSnapshot s)
    {
        if (s.EventErrorsLast5Minutes is not int c || c < 10) return RuleCheck.NotMet;
        return new RuleCheck(true,
            Title: "Event log error spike",
            Detail: $"{c} errors in the last 5 minutes.",
            Metrics: new Dictionary<string, double?> { ["errors_5m"] = c });
    }
}
```

- [ ] **Step 3: Write `RedRuleTests.cs`**

```csharp
using FluentAssertions;
using PcMonitor.Core.Issues.Rules;
using PcMonitor.Core.Models;
using Xunit;

namespace PcMonitor.Core.Tests.Issues.Rules;

public class RedRuleTests
{
    [Fact]
    public void ThermalThrottle_FiresWhenIsThrottlingTrue()
    {
        new ThermalThrottleRule().Check(SnapshotBuilder.Default(throttling: true, tempC: 97))
            .ConditionMet.Should().BeTrue();
    }

    [Fact]
    public void ThermalThrottle_DoesNotFireWhenFalseOrNull()
    {
        new ThermalThrottleRule().Check(SnapshotBuilder.Default(throttling: false)).ConditionMet.Should().BeFalse();
        new ThermalThrottleRule().Check(SnapshotBuilder.Default(throttling: null)).ConditionMet.Should().BeFalse();
    }

    [Theory]
    [InlineData(94.9, false)]
    [InlineData(95.0, true)]
    [InlineData(99.0, true)]
    public void CpuTempCritical_BoundaryAt95(double temp, bool met)
    {
        new CpuPackageTempHighRule().Check(SnapshotBuilder.Default(tempC: temp))
            .ConditionMet.Should().Be(met);
    }

    [Theory]
    [InlineData(94.9, false)]
    [InlineData(95.0, true)]
    public void CommitNearExhaustion_BoundaryAt95(double commit, bool met)
    {
        new CommitNearExhaustionRule().Check(SnapshotBuilder.Default(commitPct: commit))
            .ConditionMet.Should().Be(met);
    }

    [Theory]
    [InlineData(5.0, false)]
    [InlineData(4.9, true)]
    [InlineData(0.0, true)]
    public void DriveCCritical_BoundaryBelow5Gb(double free, bool met)
    {
        new DriveCCriticalRule().Check(SnapshotBuilder.Default(driveCFree: free))
            .ConditionMet.Should().Be(met);
    }

    [Fact]
    public void RunawayProcess_FiresWhenAnyProcessAbove50Percent()
    {
        var procs = new[] { new ProcessSample(123, "chrome.exe", 60, 1000) };
        var result = new RunawayProcessRule().Check(SnapshotBuilder.Default(procs: procs));
        result.ConditionMet.Should().BeTrue();
        result.SubjectKey.Should().Be("chrome.exe:123");
    }

    [Fact]
    public void RunawayProcess_DoesNotFireAt50OrBelow()
    {
        var procs = new[] { new ProcessSample(1, "p", 50, 1) };
        new RunawayProcessRule().Check(SnapshotBuilder.Default(procs: procs))
            .ConditionMet.Should().BeFalse();
    }

    [Theory]
    [InlineData(9, false)]
    [InlineData(10, true)]
    public void EventLogSpike_BoundaryAt10(int count, bool met)
    {
        new EventLogSpikeRule().Check(SnapshotBuilder.Default(errLast5: count))
            .ConditionMet.Should().Be(met);
    }
}
```

- [ ] **Step 4: Run tests**

Run: `dotnet test --filter FullyQualifiedName~RedRuleTests`
Expected: All tests pass.

- [ ] **Step 5: Commit**

```bash
git add app/
git commit -m "core: add 6 Red issue rules with boundary tests"
git push 2>/dev/null || echo "no remote configured; skip"
```

---

### Task 9: Yellow issue rules

**Files:**
- Create: `app/src/PcMonitor.Core/Issues/Rules/CpuPackageTempElevatedRule.cs`
- Create: `app/src/PcMonitor.Core/Issues/Rules/RamPressureRule.cs`
- Create: `app/src/PcMonitor.Core/Issues/Rules/SustainedCpuHogRule.cs`
- Create: `app/src/PcMonitor.Core/Issues/Rules/MemoryHogRule.cs`
- Create: `app/src/PcMonitor.Core/Issues/Rules/DriveCLowRule.cs`
- Create: `app/src/PcMonitor.Core/Issues/Rules/PagefilePressureRule.cs`
- Create: `app/src/PcMonitor.Core/Issues/Rules/EventLogUptickRule.cs`
- Create: `app/src/PcMonitor.Core/Issues/Rules/DiskQueueElevatedRule.cs`
- Create: `app/tests/PcMonitor.Core.Tests/Issues/Rules/YellowRuleTests.cs`

- [ ] **Step 1: Implement all eight rules**

`CpuPackageTempElevatedRule.cs`:

```csharp
using PcMonitor.Core.Models;

namespace PcMonitor.Core.Issues.Rules;

public sealed class CpuPackageTempElevatedRule : IIssueRule
{
    public string RuleId => "cpu-temp-elevated";
    public IssueSeverity Severity => IssueSeverity.Yellow;
    public TimeSpan SustainedFor => TimeSpan.FromMinutes(1);

    public RuleCheck Check(SensorSnapshot s)
    {
        if (s.CpuPackageTempC is not double t || t < 85) return RuleCheck.NotMet;
        return new RuleCheck(true,
            Title: "CPU package temperature elevated",
            Detail: $"{t:F0}°C",
            Metrics: new Dictionary<string, double?> { ["temp_c"] = t });
    }
}
```

`RamPressureRule.cs`:

```csharp
using PcMonitor.Core.Models;

namespace PcMonitor.Core.Issues.Rules;

public sealed class RamPressureRule : IIssueRule
{
    public string RuleId => "ram-pressure";
    public IssueSeverity Severity => IssueSeverity.Yellow;
    public TimeSpan SustainedFor => TimeSpan.Zero;

    public RuleCheck Check(SensorSnapshot s)
    {
        if (s.FreePhysicalRamPercent >= 15) return RuleCheck.NotMet;
        return new RuleCheck(true,
            Title: "RAM pressure",
            Detail: $"Free physical RAM at {s.FreePhysicalRamPercent:F0}%.",
            Metrics: new Dictionary<string, double?> { ["free_pct"] = s.FreePhysicalRamPercent });
    }
}
```

`SustainedCpuHogRule.cs`:

```csharp
using PcMonitor.Core.Models;

namespace PcMonitor.Core.Issues.Rules;

public sealed class SustainedCpuHogRule : IIssueRule
{
    public string RuleId => "sustained-cpu-hog";
    public IssueSeverity Severity => IssueSeverity.Yellow;
    public TimeSpan SustainedFor => TimeSpan.FromMinutes(10);

    public RuleCheck Check(SensorSnapshot s)
    {
        var top = s.TopProcesses.OrderByDescending(p => p.CpuPercent).FirstOrDefault();
        if (top is null || top.CpuPercent <= 30) return RuleCheck.NotMet;
        return new RuleCheck(true,
            SubjectKey: $"{top.Name}:{top.ProcessId}",
            Title: $"{top.Name} sustained CPU",
            Detail: $"{top.CpuPercent:F0}% CPU",
            Metrics: new Dictionary<string, double?> { ["cpu_pct"] = top.CpuPercent });
    }
}
```

`MemoryHogRule.cs`:

```csharp
using PcMonitor.Core.Models;

namespace PcMonitor.Core.Issues.Rules;

public sealed class MemoryHogRule : IIssueRule
{
    public string RuleId => "memory-hog";
    public IssueSeverity Severity => IssueSeverity.Yellow;
    public TimeSpan SustainedFor => TimeSpan.Zero;

    public RuleCheck Check(SensorSnapshot s)
    {
        var top = s.TopProcesses.OrderByDescending(p => p.RamMb).FirstOrDefault();
        if (top is null || top.RamMb <= 4096) return RuleCheck.NotMet;
        return new RuleCheck(true,
            SubjectKey: $"{top.Name}:{top.ProcessId}",
            Title: $"{top.Name} memory hog",
            Detail: $"{top.RamMb / 1024.0:F1} GB RAM",
            Metrics: new Dictionary<string, double?> { ["ram_mb"] = top.RamMb });
    }
}
```

`DriveCLowRule.cs`:

```csharp
using PcMonitor.Core.Models;

namespace PcMonitor.Core.Issues.Rules;

public sealed class DriveCLowRule : IIssueRule
{
    public string RuleId => "drive-c-low";
    public IssueSeverity Severity => IssueSeverity.Yellow;
    public TimeSpan SustainedFor => TimeSpan.Zero;

    public RuleCheck Check(SensorSnapshot s)
    {
        if (s.DriveCFreeGb is not double gb || gb >= 20) return RuleCheck.NotMet;
        return new RuleCheck(true,
            Title: "Drive C: getting full",
            Detail: $"{gb:F0} GB free.",
            Metrics: new Dictionary<string, double?> { ["free_gb"] = gb });
    }
}
```

`PagefilePressureRule.cs`:

```csharp
using PcMonitor.Core.Models;

namespace PcMonitor.Core.Issues.Rules;

public sealed class PagefilePressureRule : IIssueRule
{
    public string RuleId => "pagefile-pressure";
    public IssueSeverity Severity => IssueSeverity.Yellow;
    public TimeSpan SustainedFor => TimeSpan.Zero;

    public RuleCheck Check(SensorSnapshot s)
    {
        if (s.PagefileUsedPercent is not double p || p <= 50) return RuleCheck.NotMet;
        return new RuleCheck(true,
            Title: "Pagefile pressure",
            Detail: $"Pagefile at {p:F0}% of allocated.",
            Metrics: new Dictionary<string, double?> { ["pagefile_pct"] = p });
    }
}
```

`EventLogUptickRule.cs`:

```csharp
using PcMonitor.Core.Models;

namespace PcMonitor.Core.Issues.Rules;

public sealed class EventLogUptickRule : IIssueRule
{
    public string RuleId => "event-log-uptick";
    public IssueSeverity Severity => IssueSeverity.Yellow;
    public TimeSpan SustainedFor => TimeSpan.Zero;

    public RuleCheck Check(SensorSnapshot s)
    {
        if (s.EventErrors24hHourlyAverage is not double avg || avg <= 0) return RuleCheck.NotMet;
        if (s.EventErrorsThisHour is not int now) return RuleCheck.NotMet;
        if (now < 2 * avg) return RuleCheck.NotMet;
        return new RuleCheck(true,
            Title: "Event log uptick",
            Detail: $"{now} errors this hour vs {avg:F1} avg.",
            Metrics: new Dictionary<string, double?>
            {
                ["errors_this_hour"] = now,
                ["avg_24h"] = avg,
            });
    }
}
```

`DiskQueueElevatedRule.cs`:

```csharp
using PcMonitor.Core.Models;

namespace PcMonitor.Core.Issues.Rules;

public sealed class DiskQueueElevatedRule : IIssueRule
{
    public string RuleId => "disk-queue-elevated";
    public IssueSeverity Severity => IssueSeverity.Yellow;
    public TimeSpan SustainedFor => TimeSpan.FromSeconds(60);

    public RuleCheck Check(SensorSnapshot s)
    {
        if (s.DiskQueueLength is not double q || q <= 4) return RuleCheck.NotMet;
        return new RuleCheck(true,
            Title: "Disk queue elevated",
            Detail: $"Queue length {q:F1}.",
            Metrics: new Dictionary<string, double?> { ["queue"] = q });
    }
}
```

- [ ] **Step 2: Write `YellowRuleTests.cs`**

```csharp
using FluentAssertions;
using PcMonitor.Core.Issues.Rules;
using PcMonitor.Core.Models;
using Xunit;

namespace PcMonitor.Core.Tests.Issues.Rules;

public class YellowRuleTests
{
    [Theory]
    [InlineData(84.9, false)]
    [InlineData(85.0, true)]
    public void TempElevated_BoundaryAt85(double t, bool met) =>
        new CpuPackageTempElevatedRule().Check(SnapshotBuilder.Default(tempC: t)).ConditionMet.Should().Be(met);

    [Theory]
    [InlineData(15.0, false)]
    [InlineData(14.9, true)]
    public void RamPressure_BoundaryAt15Percent(double free, bool met) =>
        new RamPressureRule().Check(SnapshotBuilder.Default(freePhysRamPct: free)).ConditionMet.Should().Be(met);

    [Fact]
    public void SustainedCpuHog_FiresAbove30()
    {
        var procs = new[] { new ProcessSample(1, "p", 31, 0) };
        new SustainedCpuHogRule().Check(SnapshotBuilder.Default(procs: procs)).ConditionMet.Should().BeTrue();
    }

    [Fact]
    public void MemoryHog_FiresAbove4Gb()
    {
        var procs = new[] { new ProcessSample(1, "p", 0, 4097) };
        new MemoryHogRule().Check(SnapshotBuilder.Default(procs: procs)).ConditionMet.Should().BeTrue();
    }

    [Theory]
    [InlineData(20.0, false)]
    [InlineData(19.9, true)]
    public void DriveCLow_BoundaryBelow20Gb(double gb, bool met) =>
        new DriveCLowRule().Check(SnapshotBuilder.Default(driveCFree: gb)).ConditionMet.Should().Be(met);

    [Theory]
    [InlineData(50.0, false)]
    [InlineData(50.1, true)]
    public void PagefilePressure_BoundaryAt50(double p, bool met) =>
        new PagefilePressureRule().Check(SnapshotBuilder.Default(pagefilePct: p)).ConditionMet.Should().Be(met);

    [Theory]
    [InlineData(11, 6.0, false)]  // 11 < 2*6=12 → not met
    [InlineData(12, 6.0, true)]   // 12 == 2*6=12 → met
    [InlineData(8, 5.0, false)]   // 8 < 2*5=10 → not met
    [InlineData(10, 5.0, true)]   // 10 == 2*5=10 → met
    public void EventLogUptick_DoubleBaseline(int now, double avg, bool met) =>
        new EventLogUptickRule().Check(SnapshotBuilder.Default(errThisHour: now, errAvg24h: avg))
            .ConditionMet.Should().Be(met);

    [Fact]
    public void EventLogUptick_NullAverageDoesNotFire() =>
        new EventLogUptickRule().Check(SnapshotBuilder.Default(errThisHour: 100, errAvg24h: null))
            .ConditionMet.Should().BeFalse();

    [Theory]
    [InlineData(4.0, false)]
    [InlineData(4.1, true)]
    public void DiskQueueElevated_BoundaryAbove4(double q, bool met) =>
        new DiskQueueElevatedRule().Check(SnapshotBuilder.Default(diskQ: q)).ConditionMet.Should().Be(met);
}
```

- [ ] **Step 3: Run tests**

Run: `dotnet test --filter FullyQualifiedName~YellowRuleTests`
Expected: All pass.

- [ ] **Step 4: Commit**

```bash
git add app/
git commit -m "core: add 8 Yellow issue rules with boundary tests"
git push 2>/dev/null || echo "no remote configured; skip"
```

---

## Phase 3 — Sensors

This phase wires the actual data sources. Pure-logic pieces (`ProcessSampler` math) are unit-tested; the LibreHardwareMonitor and perf counter wiring is verified manually on the target PC.

### Task 10: `ProcessSampler` — per-process CPU normalization

Per-process CPU is computed as a delta of `Process.TotalProcessorTime` over the interval, divided by `interval * logicalCoreCount` to normalize to a 0–100% machine-wide percentage. This matches `live-probe.ps1`. The sampler is testable with an injectable clock + injectable process enumeration.

**Files:**
- Create: `app/src/PcMonitor.Core/Sensors/ProcessSampler.cs`
- Create: `app/src/PcMonitor.Core/Sensors/IProcessEnumerator.cs`
- Test: `app/tests/PcMonitor.Core.Tests/Sensors/ProcessSamplerTests.cs`

- [ ] **Step 1: Create `Sensors/IProcessEnumerator.cs`**

```csharp
namespace PcMonitor.Core.Sensors;

public readonly record struct RawProcess(int Pid, string Name, TimeSpan TotalProcessorTime, long WorkingSetBytes);

public interface IProcessEnumerator
{
    IReadOnlyList<RawProcess> Enumerate();
}
```

- [ ] **Step 2: Write failing tests**

```csharp
using FluentAssertions;
using PcMonitor.Core.Models;
using PcMonitor.Core.Sensors;
using Xunit;

namespace PcMonitor.Core.Tests.Sensors;

public class ProcessSamplerTests
{
    private sealed class StubEnumerator : IProcessEnumerator
    {
        public IReadOnlyList<RawProcess> Next { get; set; } = Array.Empty<RawProcess>();
        public IReadOnlyList<RawProcess> Enumerate() => Next;
    }

    [Fact]
    public void FirstSample_ReturnsEmpty()
    {
        var stub = new StubEnumerator
        {
            Next = new[] { new RawProcess(1, "p", TimeSpan.FromSeconds(10), 1024 * 1024) },
        };
        var clock = DateTimeOffset.UnixEpoch;
        var sampler = new ProcessSampler(stub, logicalCores: 24);
        sampler.Sample(clock).Should().BeEmpty();
    }

    [Fact]
    public void SecondSample_ComputesNormalizedCpuPercent()
    {
        var stub = new StubEnumerator
        {
            Next = new[] { new RawProcess(1, "p", TimeSpan.FromSeconds(10), 0) },
        };
        var sampler = new ProcessSampler(stub, logicalCores: 4);
        var t0 = DateTimeOffset.UnixEpoch;
        sampler.Sample(t0);

        stub.Next = new[] { new RawProcess(1, "p", TimeSpan.FromSeconds(11), 0) };
        var result = sampler.Sample(t0.AddSeconds(1));
        var only = result.Should().ContainSingle().Subject;
        only.CpuPercent.Should().BeApproximately(25, 0.01);
    }

    [Fact]
    public void ProcessExitedBetweenSamples_DroppedFromResults()
    {
        var stub = new StubEnumerator
        {
            Next = new[]
            {
                new RawProcess(1, "p1", TimeSpan.FromSeconds(10), 0),
                new RawProcess(2, "p2", TimeSpan.FromSeconds(10), 0),
            },
        };
        var sampler = new ProcessSampler(stub, logicalCores: 4);
        var t0 = DateTimeOffset.UnixEpoch;
        sampler.Sample(t0);
        stub.Next = new[] { new RawProcess(1, "p1", TimeSpan.FromSeconds(11), 0) };
        var res = sampler.Sample(t0.AddSeconds(1));
        res.Select(p => p.ProcessId).Should().BeEquivalentTo(new[] { 1 });
    }

    [Fact]
    public void RamMbIsBytesDividedByMb()
    {
        var stub = new StubEnumerator
        {
            Next = new[] { new RawProcess(1, "p", TimeSpan.FromSeconds(10), 200L * 1024 * 1024) },
        };
        var sampler = new ProcessSampler(stub, logicalCores: 4);
        sampler.Sample(DateTimeOffset.UnixEpoch);
        stub.Next = new[] { new RawProcess(1, "p", TimeSpan.FromSeconds(10), 200L * 1024 * 1024) };
        var res = sampler.Sample(DateTimeOffset.UnixEpoch.AddSeconds(1)).Single();
        res.RamMb.Should().BeApproximately(200, 0.1);
    }
}
```

- [ ] **Step 3: Run tests to confirm failure**

Run: `dotnet test --filter FullyQualifiedName~ProcessSamplerTests`
Expected: Compile failure.

- [ ] **Step 4: Implement `Sensors/ProcessSampler.cs`**

```csharp
using PcMonitor.Core.Models;

namespace PcMonitor.Core.Sensors;

public sealed class ProcessSampler
{
    private readonly IProcessEnumerator _enumerator;
    private readonly int _logicalCores;
    private DateTimeOffset _lastSampleAt;
    private Dictionary<int, RawProcess> _previous = new();

    public ProcessSampler(IProcessEnumerator enumerator, int logicalCores)
    {
        _enumerator = enumerator;
        _logicalCores = Math.Max(1, logicalCores);
    }

    public IReadOnlyList<ProcessSample> Sample(DateTimeOffset now)
    {
        var current = _enumerator.Enumerate().ToDictionary(p => p.Pid);

        if (_previous.Count == 0)
        {
            _previous = current;
            _lastSampleAt = now;
            return Array.Empty<ProcessSample>();
        }

        var deltaSeconds = (now - _lastSampleAt).TotalSeconds;
        if (deltaSeconds <= 0)
        {
            _previous = current;
            _lastSampleAt = now;
            return Array.Empty<ProcessSample>();
        }

        var results = new List<ProcessSample>(current.Count);
        foreach (var (pid, proc) in current)
        {
            if (!_previous.TryGetValue(pid, out var prev)) continue;
            var cpuSeconds = (proc.TotalProcessorTime - prev.TotalProcessorTime).TotalSeconds;
            var pct = (cpuSeconds / deltaSeconds) / _logicalCores * 100.0;
            pct = Math.Clamp(pct, 0, 100);
            var ramMb = proc.WorkingSetBytes / (1024.0 * 1024.0);
            results.Add(new ProcessSample(pid, proc.Name, pct, ramMb));
        }

        _previous = current;
        _lastSampleAt = now;
        return results;
    }
}
```

- [ ] **Step 5: Run tests to confirm pass**

Run: `dotnet test --filter FullyQualifiedName~ProcessSamplerTests`
Expected: 4 passed.

- [ ] **Step 6: Implement live process enumerator `Sensors/SystemProcessEnumerator.cs`**

```csharp
using System.Diagnostics;

namespace PcMonitor.Core.Sensors;

public sealed class SystemProcessEnumerator : IProcessEnumerator
{
    public IReadOnlyList<RawProcess> Enumerate()
    {
        var procs = Process.GetProcesses();
        var list = new List<RawProcess>(procs.Length);
        foreach (var p in procs)
        {
            try
            {
                list.Add(new RawProcess(p.Id, p.ProcessName, p.TotalProcessorTime, p.WorkingSet64));
            }
            catch
            {
                // process exited or access denied; skip
            }
            finally
            {
                p.Dispose();
            }
        }
        return list;
    }
}
```

- [ ] **Step 7: Commit**

```bash
git add app/
git commit -m "core: add ProcessSampler with normalized per-process CPU + system enumerator"
git push 2>/dev/null || echo "no remote configured; skip"
```

---

### Task 11: `EventLogPoller` — cached event-log error counts

`EventLogPoller` exposes `LastFiveMinuteErrors` and `LastHourErrors`. It refreshes on its own cadence (every 60 s by default) to avoid hammering the event log on every 1 Hz tick. Use `System.Diagnostics.Eventing.Reader.EventLogQuery` so we can filter by level + timeframe via XPath.

**Files:**
- Create: `app/src/PcMonitor.Core/Sensors/IEventLogPoller.cs`
- Create: `app/src/PcMonitor.Core/Sensors/EventLogPoller.cs`
- Test: `app/tests/PcMonitor.Core.Tests/Sensors/EventLogPollerCacheTests.cs`

- [ ] **Step 1: Create `Sensors/IEventLogPoller.cs`**

```csharp
namespace PcMonitor.Core.Sensors;

public interface IEventLogPoller
{
    int? Last5MinutesErrors { get; }
    int? LastHourErrors { get; }
    void RefreshIfDue(DateTimeOffset now);
}
```

- [ ] **Step 2: Write cache-behavior tests**

```csharp
using FluentAssertions;
using PcMonitor.Core.Sensors;
using Xunit;

namespace PcMonitor.Core.Tests.Sensors;

public class EventLogPollerCacheTests
{
    [Fact]
    public void RefreshIfDue_OnlyCallsBackendOncePerCacheWindow()
    {
        var calls = 0;
        var poller = new EventLogPoller(
            queryFn: _ => { calls++; return (2, 7); },
            refreshInterval: TimeSpan.FromSeconds(60));
        var t = DateTimeOffset.UnixEpoch;
        poller.RefreshIfDue(t);
        poller.RefreshIfDue(t.AddSeconds(30));
        poller.RefreshIfDue(t.AddSeconds(59));
        calls.Should().Be(1);
        poller.RefreshIfDue(t.AddSeconds(60));
        calls.Should().Be(2);
    }

    [Fact]
    public void Counts_ExposedFromLastQuery()
    {
        var poller = new EventLogPoller(
            queryFn: _ => (3, 11),
            refreshInterval: TimeSpan.FromSeconds(60));
        poller.RefreshIfDue(DateTimeOffset.UnixEpoch);
        poller.Last5MinutesErrors.Should().Be(3);
        poller.LastHourErrors.Should().Be(11);
    }
}
```

- [ ] **Step 3: Implement `Sensors/EventLogPoller.cs`**

```csharp
namespace PcMonitor.Core.Sensors;

public sealed class EventLogPoller : IEventLogPoller
{
    private readonly Func<DateTimeOffset, (int last5m, int lastHour)> _queryFn;
    private readonly TimeSpan _refreshInterval;
    private DateTimeOffset _lastQueryAt = DateTimeOffset.MinValue;

    public int? Last5MinutesErrors { get; private set; }
    public int? LastHourErrors { get; private set; }

    public EventLogPoller(
        Func<DateTimeOffset, (int last5m, int lastHour)> queryFn,
        TimeSpan refreshInterval)
    {
        _queryFn = queryFn;
        _refreshInterval = refreshInterval;
    }

    public void RefreshIfDue(DateTimeOffset now)
    {
        if (now - _lastQueryAt < _refreshInterval) return;
        try
        {
            var (last5m, lastHour) = _queryFn(now);
            Last5MinutesErrors = last5m;
            LastHourErrors = lastHour;
        }
        catch
        {
            Last5MinutesErrors = null;
            LastHourErrors = null;
        }
        _lastQueryAt = now;
    }

    public static (int last5m, int lastHour) QueryWindowsEventLog(DateTimeOffset now)
    {
        var fiveMinAgo = now.AddMinutes(-5).UtcDateTime.ToString("o");
        var oneHourAgo = now.AddHours(-1).UtcDateTime.ToString("o");

        int Count(string log, string startIso)
        {
            var xpath = $"*[System[(Level=1 or Level=2) and TimeCreated[@SystemTime>='{startIso}']]]";
            var query = new System.Diagnostics.Eventing.Reader.EventLogQuery(log, System.Diagnostics.Eventing.Reader.PathType.LogName, xpath);
            using var reader = new System.Diagnostics.Eventing.Reader.EventLogReader(query);
            var c = 0;
            while (reader.ReadEvent() is { } e)
            {
                e.Dispose();
                c++;
            }
            return c;
        }

        var sys5 = Count("System", fiveMinAgo) + Count("Application", fiveMinAgo);
        var sys60 = Count("System", oneHourAgo) + Count("Application", oneHourAgo);
        return (sys5, sys60);
    }
}
```

- [ ] **Step 4: Run tests**

Run: `dotnet test --filter FullyQualifiedName~EventLogPollerCacheTests`
Expected: 2 passed.

- [ ] **Step 5: Commit**

```bash
git add app/
git commit -m "core: add EventLogPoller with cache + Windows event log query helper"
git push 2>/dev/null || echo "no remote configured; skip"
```

---

### Task 12: `SensorService` — LHM + perf counters + WMI wiring

This is the integration point. Unit tests are limited (mostly the consumers test against the `ISensorService` interface with fakes). Verify manually on Windows.

**Files:**
- Create: `app/src/PcMonitor.Core/Sensors/ISensorService.cs`
- Create: `app/src/PcMonitor.Core/Sensors/SensorService.cs`

- [ ] **Step 1: Create `Sensors/ISensorService.cs`**

```csharp
using PcMonitor.Core.Models;

namespace PcMonitor.Core.Sensors;

public interface ISensorService : IDisposable
{
    SensorSnapshot Read(DateTimeOffset now);
    bool TempSensorsAvailable { get; }
}
```

- [ ] **Step 2: Implement `Sensors/SensorService.cs`**

```csharp
using System.Diagnostics;
using System.Management;
using LibreHardwareMonitor.Hardware;
using PcMonitor.Core.History;
using PcMonitor.Core.Models;

namespace PcMonitor.Core.Sensors;

public sealed class SensorService : ISensorService
{
    private readonly Computer? _computer;
    private readonly ProcessSampler _processes;
    private readonly EventLogPoller _events;
    private readonly IHistoryReader _history;
    private readonly PerformanceCounter? _cpuTotal;
    private readonly PerformanceCounter? _diskQueue;

    public bool TempSensorsAvailable { get; }

    public SensorService(IHistoryReader history)
    {
        _history = history;
        try
        {
            _computer = new Computer
            {
                IsCpuEnabled = true,
                IsMemoryEnabled = true,
                IsStorageEnabled = true,
            };
            _computer.Open();
            TempSensorsAvailable = true;
        }
        catch
        {
            _computer = null;
            TempSensorsAvailable = false;
        }

        try { _cpuTotal = new PerformanceCounter("Processor", "% Processor Time", "_Total"); _cpuTotal.NextValue(); } catch { _cpuTotal = null; }
        try { _diskQueue = new PerformanceCounter("PhysicalDisk", "Current Disk Queue Length", "_Total"); _diskQueue.NextValue(); } catch { _diskQueue = null; }

        _processes = new ProcessSampler(new SystemProcessEnumerator(), Environment.ProcessorCount);
        _events = new EventLogPoller(EventLogPoller.QueryWindowsEventLog, TimeSpan.FromSeconds(60));
    }

    public SensorSnapshot Read(DateTimeOffset now)
    {
        double? cpu = null;
        try { cpu = _cpuTotal?.NextValue(); } catch { }

        double? tempC = null;
        bool? throttling = null;
        try
        {
            if (_computer is not null)
            {
                foreach (var hw in _computer.Hardware)
                {
                    if (hw.HardwareType != HardwareType.Cpu) continue;
                    hw.Update();
                    foreach (var sensor in hw.Sensors)
                    {
                        if (sensor.SensorType == SensorType.Temperature &&
                            sensor.Name.Contains("Package", StringComparison.OrdinalIgnoreCase))
                            tempC = sensor.Value;
                        if (sensor.Name.Contains("Throttle", StringComparison.OrdinalIgnoreCase) ||
                            sensor.Name.Contains("PROCHOT", StringComparison.OrdinalIgnoreCase))
                            throttling = (sensor.Value ?? 0) > 0;
                    }
                }
            }
        }
        catch { }
        if (tempC is double t && throttling is null) throttling = t >= 99;

        var (ramUsed, ramTotal, freePhysPct, commitPct, pagefilePct, driveCFree) = ReadMemoryAndDisk();
        double? diskQ = null;
        try { diskQ = _diskQueue?.NextValue(); } catch { }

        _events.RefreshIfDue(now);
        var procs = _processes.Sample(now).OrderByDescending(p => p.CpuPercent).Take(10).ToList();
        var avg24h = _history.AverageHourlyErrorCount();

        return new SensorSnapshot(
            now, cpu, tempC, throttling,
            ramUsed, ramTotal, freePhysPct,
            commitPct, pagefilePct, diskQ, driveCFree,
            _events.Last5MinutesErrors, _events.LastHourErrors, avg24h,
            procs);
    }

    private static (double ramUsed, double ramTotal, double freePhysPct, double? commitPct, double? pagefilePct, double? driveCFree) ReadMemoryAndDisk()
    {
        double ramUsed = 0, ramTotal = 0, freePhysPct = 0;
        double? commitPct = null, pagefilePct = null, driveCFree = null;
        try
        {
            using var os = new ManagementObjectSearcher("SELECT TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem");
            foreach (ManagementObject m in os.Get())
            {
                var totalKb = Convert.ToDouble(m["TotalVisibleMemorySize"]);
                var freeKb = Convert.ToDouble(m["FreePhysicalMemory"]);
                ramTotal = totalKb / 1024.0 / 1024.0;
                ramUsed = (totalKb - freeKb) / 1024.0 / 1024.0;
                freePhysPct = totalKb == 0 ? 0 : (freeKb / totalKb * 100.0);
            }
        }
        catch { }

        try
        {
            using var pf = new ManagementObjectSearcher("SELECT AllocatedBaseSize, CurrentUsage FROM Win32_PageFileUsage");
            foreach (ManagementObject m in pf.Get())
            {
                var alloc = Convert.ToDouble(m["AllocatedBaseSize"]);
                var used = Convert.ToDouble(m["CurrentUsage"]);
                if (alloc > 0) pagefilePct = used / alloc * 100.0;
            }
        }
        catch { }

        try
        {
            using var commit = new ManagementObjectSearcher("SELECT CommittedBytes, CommitLimit FROM Win32_PerfRawData_PerfOS_Memory");
            foreach (ManagementObject m in commit.Get())
            {
                var committed = Convert.ToDouble(m["CommittedBytes"]);
                var limit = Convert.ToDouble(m["CommitLimit"]);
                if (limit > 0) commitPct = committed / limit * 100.0;
            }
        }
        catch { }

        try
        {
            var drive = new DriveInfo("C");
            if (drive.IsReady) driveCFree = drive.AvailableFreeSpace / (1024.0 * 1024 * 1024);
        }
        catch { }

        return (ramUsed, ramTotal, freePhysPct, commitPct, pagefilePct, driveCFree);
    }

    public void Dispose()
    {
        _cpuTotal?.Dispose();
        _diskQueue?.Dispose();
        _computer?.Close();
    }
}
```

- [ ] **Step 3: Build**

Run: `dotnet build`
Expected: 0 errors.

- [ ] **Step 4: Commit**

```bash
git add app/
git commit -m "core: add SensorService wiring LHM + perf counters + WMI + history"
git push 2>/dev/null || echo "no remote configured; skip"
```

---

## Phase 4 — Capture orchestration

### Task 13: `IProcessRunner` + `PowerShellProcessRunner`

**Files:**
- Create: `app/src/PcMonitor.Core/Capture/IProcessRunner.cs`
- Create: `app/src/PcMonitor.Core/Capture/PowerShellProcessRunner.cs`

- [ ] **Step 1: Create `Capture/IProcessRunner.cs`**

```csharp
namespace PcMonitor.Core.Capture;

public interface IProcessRunner
{
    Task<int> RunAsync(
        string fileName,
        string arguments,
        Action<string, bool> onLine,
        CancellationToken ct);
}
```

`onLine(text, isStdErr)` is invoked once per output line.

- [ ] **Step 2: Implement `Capture/PowerShellProcessRunner.cs`**

```csharp
using System.Diagnostics;

namespace PcMonitor.Core.Capture;

public sealed class PowerShellProcessRunner : IProcessRunner
{
    public async Task<int> RunAsync(
        string fileName,
        string arguments,
        Action<string, bool> onLine,
        CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
        proc.OutputDataReceived += (_, e) => { if (e.Data is not null) onLine(e.Data, false); };
        proc.ErrorDataReceived += (_, e) => { if (e.Data is not null) onLine(e.Data, true); };
        proc.Start();
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();

        using var reg = ct.Register(() =>
        {
            try { if (!proc.HasExited) proc.Kill(entireProcessTree: true); } catch { }
        });

        await proc.WaitForExitAsync(CancellationToken.None);
        return proc.ExitCode;
    }
}
```

- [ ] **Step 3: Build and commit**

Run: `dotnet build`
Expected: 0 errors.

```bash
git add app/
git commit -m "core: add IProcessRunner and PowerShellProcessRunner"
git push 2>/dev/null || echo "no remote configured; skip"
```

---

### Task 14: `CaptureService`

**Files:**
- Create: `app/src/PcMonitor.Core/Capture/ICaptureService.cs`
- Create: `app/src/PcMonitor.Core/Capture/CaptureService.cs`
- Test: `app/tests/PcMonitor.Core.Tests/Capture/CaptureServiceTests.cs`

- [ ] **Step 1: Create `Capture/ICaptureService.cs`**

```csharp
using PcMonitor.Core.Models;

namespace PcMonitor.Core.Capture;

public interface ICaptureService
{
    Task<CaptureResult> RunAsync(
        CaptureKind kind,
        Action<CaptureLine> onLine,
        CancellationToken ct);
}
```

- [ ] **Step 2: Write failing tests with a fake `IProcessRunner`**

```csharp
using FluentAssertions;
using PcMonitor.Core.Capture;
using PcMonitor.Core.Models;
using Xunit;

namespace PcMonitor.Core.Tests.Capture;

public class CaptureServiceTests : IDisposable
{
    private readonly string _scriptsDir = Path.Combine(Path.GetTempPath(), "pcmon-scripts-" + Guid.NewGuid());
    private readonly string _logsDir = Path.Combine(Path.GetTempPath(), "pcmon-logs-" + Guid.NewGuid());

    public CaptureServiceTests()
    {
        Directory.CreateDirectory(_scriptsDir);
        Directory.CreateDirectory(_logsDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_scriptsDir, recursive: true); } catch { }
        try { Directory.Delete(_logsDir, recursive: true); } catch { }
    }

    private sealed class FakeRunner : IProcessRunner
    {
        public int ExitCode { get; init; }
        public Action<Action<string, bool>>? Emit { get; init; }
        public Task<int> RunAsync(string fileName, string arguments, Action<string, bool> onLine, CancellationToken ct)
        {
            Emit?.Invoke(onLine);
            return Task.FromResult(ExitCode);
        }
    }

    [Fact]
    public async Task RunAsync_MissingScript_ReturnsFailure()
    {
        var svc = new CaptureService(new FakeRunner(), _scriptsDir, _logsDir);
        var result = await svc.RunAsync(CaptureKind.Diagnostic, _ => { }, CancellationToken.None);
        result.Success.Should().BeFalse();
        result.StdErr.Should().Contain("not found");
    }

    [Fact]
    public async Task RunAsync_SuccessWithMatchingNewFile_ReturnsPath()
    {
        File.WriteAllText(Path.Combine(_scriptsDir, "diagnose.ps1"), "# stub");
        var newFile = Path.Combine(_logsDir, "diagnostic_2026-05-26_14-32.txt");
        var runner = new FakeRunner
        {
            ExitCode = 0,
            Emit = onLine =>
            {
                File.WriteAllText(newFile, "stub output");
                onLine("done", false);
            },
        };
        var svc = new CaptureService(runner, _scriptsDir, _logsDir);
        var result = await svc.RunAsync(CaptureKind.Diagnostic, _ => { }, CancellationToken.None);
        result.Success.Should().BeTrue();
        result.WindowsPath.Should().Be(newFile);
        result.WslPath.Should().Be(WslPathConverter.ToWsl(newFile));
    }

    [Fact]
    public async Task RunAsync_NonZeroExit_ReturnsFailureWithStderr()
    {
        File.WriteAllText(Path.Combine(_scriptsDir, "diagnose.ps1"), "# stub");
        var runner = new FakeRunner
        {
            ExitCode = 1,
            Emit = onLine => onLine("boom", true),
        };
        var svc = new CaptureService(runner, _scriptsDir, _logsDir);
        var result = await svc.RunAsync(CaptureKind.Diagnostic, _ => { }, CancellationToken.None);
        result.Success.Should().BeFalse();
        result.ExitCode.Should().Be(1);
        result.StdErr.Should().Contain("boom");
    }
}
```

- [ ] **Step 3: Implement `Capture/CaptureService.cs`**

```csharp
using System.Text;
using PcMonitor.Core.Models;

namespace PcMonitor.Core.Capture;

public sealed class CaptureService : ICaptureService
{
    private readonly IProcessRunner _runner;
    private readonly string _scriptsDir;
    private readonly string _logsDir;

    public CaptureService(IProcessRunner runner, string scriptsDir, string logsDir)
    {
        _runner = runner;
        _scriptsDir = scriptsDir;
        _logsDir = logsDir;
    }

    public async Task<CaptureResult> RunAsync(CaptureKind kind, Action<CaptureLine> onLine, CancellationToken ct)
    {
        var (scriptFile, filePrefix) = kind switch
        {
            CaptureKind.Diagnostic => ("diagnose.ps1", "diagnostic_"),
            CaptureKind.LiveProbe => ("live-probe.ps1", "live_"),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        var scriptPath = Path.Combine(_scriptsDir, scriptFile);
        if (!File.Exists(scriptPath))
        {
            return new CaptureResult(kind, false, false, null, null, null,
                $"Script not found: {scriptPath}. Re-run install.ps1 or copy from repo files/.");
        }

        var stderr = new StringBuilder();
        var startedAt = DateTime.UtcNow.AddSeconds(-1);
        int exitCode;
        try
        {
            exitCode = await _runner.RunAsync(
                "powershell.exe",
                $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                (text, isErr) =>
                {
                    if (isErr) stderr.AppendLine(text);
                    onLine(new CaptureLine(DateTimeOffset.UtcNow, isErr, text));
                },
                ct);
        }
        catch (OperationCanceledException)
        {
            return new CaptureResult(kind, false, true, null, null, null, null);
        }

        string? newest = null;
        try
        {
            newest = Directory.EnumerateFiles(_logsDir, filePrefix + "*.txt")
                .Select(p => new FileInfo(p))
                .Where(fi => fi.CreationTimeUtc >= startedAt)
                .OrderByDescending(fi => fi.CreationTimeUtc)
                .Select(fi => fi.FullName)
                .FirstOrDefault();
        }
        catch { }

        var ok = exitCode == 0 && newest is not null;
        return new CaptureResult(
            kind,
            Success: ok,
            Cancelled: false,
            ExitCode: exitCode,
            WindowsPath: newest,
            WslPath: WslPathConverter.ToWsl(newest),
            StdErr: stderr.Length == 0 ? null : stderr.ToString().TrimEnd());
    }
}
```

- [ ] **Step 4: Run tests**

Run: `dotnet test --filter FullyQualifiedName~CaptureServiceTests`
Expected: 3 passed.

- [ ] **Step 5: Commit**

```bash
git add app/
git commit -m "core: add CaptureService with script lookup, file scanning, and WSL path conversion"
git push 2>/dev/null || echo "no remote configured; skip"
```

---

## Phase 5 — WPF UI

### Task 15: App startup, single-instance, dependency wiring

**Files:**
- Modify: `app/src/PcMonitor.App/App.xaml.cs`
- Create: `app/src/PcMonitor.App/Composition/Services.cs`
- Create: `app/src/PcMonitor.App/Composition/Paths.cs`
- Modify: `app/src/PcMonitor.App/App.xaml` (set `StartupUri="Views/CockpitWindow.xaml"`)

- [ ] **Step 1: Create `Composition/Paths.cs`**

```csharp
namespace PcMonitor.App.Composition;

public static class Paths
{
    public static string SysLogsRoot =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SysLogs");
    public static string HourlyFolder => Path.Combine(SysLogsRoot, "hourly");
    public static string ScriptsFolder => Path.Combine(SysLogsRoot, "scripts");
    public static string AppDataFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PcMonitor");
    public static string LogFile => Path.Combine(AppDataFolder, "log.txt");
    public static string SettingsFile => Path.Combine(AppDataFolder, "settings.json");
}
```

- [ ] **Step 2: Create `Composition/Services.cs` (poor-man's DI)**

```csharp
using PcMonitor.Core.Capture;
using PcMonitor.Core.History;
using PcMonitor.Core.Issues;
using PcMonitor.Core.Issues.Rules;
using PcMonitor.Core.Sensors;

namespace PcMonitor.App.Composition;

public sealed class Services : IDisposable
{
    public ISensorService Sensors { get; }
    public IssueEvaluator Issues { get; }
    public ICaptureService Capture { get; }
    public HourlyHistoryReader History { get; }

    public Services()
    {
        Directory.CreateDirectory(Paths.AppDataFolder);
        History = new HourlyHistoryReader(Paths.HourlyFolder, watch: true);
        Sensors = new SensorService(History);
        Issues = new IssueEvaluator(new IIssueRule[]
        {
            new ThermalThrottleRule(),
            new CpuPackageTempHighRule(),
            new CommitNearExhaustionRule(),
            new DriveCCriticalRule(),
            new RunawayProcessRule(),
            new EventLogSpikeRule(),
            new CpuPackageTempElevatedRule(),
            new RamPressureRule(),
            new SustainedCpuHogRule(),
            new MemoryHogRule(),
            new DriveCLowRule(),
            new PagefilePressureRule(),
            new EventLogUptickRule(),
            new DiskQueueElevatedRule(),
        });
        Capture = new CaptureService(new PowerShellProcessRunner(), Paths.ScriptsFolder, Paths.SysLogsRoot);
    }

    public void Dispose()
    {
        Sensors.Dispose();
        History.Dispose();
    }
}
```

- [ ] **Step 3: Replace `App.xaml.cs`**

```csharp
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Windows;
using PcMonitor.App.Composition;

namespace PcMonitor.App;

public partial class App : Application
{
    private const string MutexName = @"Global\MarshPcMonitor.SingleInstance";
    private const string PipeName = "MarshPcMonitor.Activate";
    private Mutex? _mutex;
    public Services? Services { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        _mutex = new Mutex(true, MutexName, out var isFirst);
        if (!isFirst)
        {
            try
            {
                using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
                client.Connect(500);
                using var w = new StreamWriter(client);
                w.WriteLine("activate");
            }
            catch { }
            Shutdown();
            return;
        }

        Services = new Services();
        _ = Task.Run(ActivationListener);
        base.OnStartup(e);
    }

    private async Task ActivationListener()
    {
        while (true)
        {
            try
            {
                using var server = new NamedPipeServerStream(PipeName, PipeDirection.In);
                await server.WaitForConnectionAsync();
                using var r = new StreamReader(server);
                var msg = await r.ReadLineAsync();
                if (msg == "activate")
                {
                    Dispatcher.Invoke(() =>
                    {
                        if (MainWindow is { } w)
                        {
                            if (w.WindowState == WindowState.Minimized) w.WindowState = WindowState.Normal;
                            w.Activate();
                            w.Topmost = true;
                            w.Topmost = false;
                        }
                    });
                }
            }
            catch { }
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Services?.Dispose();
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
```

- [ ] **Step 4: Update `App.xaml` to point at the cockpit window**

```xml
<Application x:Class="PcMonitor.App.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             StartupUri="Views/CockpitWindow.xaml" />
```

- [ ] **Step 5: Build (will fail until Task 17 adds `CockpitWindow.xaml`)**

Run: `dotnet build`
Expected: Build fails because `CockpitWindow.xaml` does not exist yet. That is acceptable — the next tasks add it.

- [ ] **Step 6: Stage but do not commit yet**

Do not commit until Task 17 builds.

---

### Task 16: User settings persistence

**Files:**
- Create: `app/src/PcMonitor.App/Settings/UserSettings.cs`
- Create: `app/src/PcMonitor.App/Settings/SettingsStore.cs`

No automated test for `SettingsStore` in v1: it's a thin JSON wrapper, only consumed by `App`, and adding a `PcMonitor.App.Tests` project is more friction than the unit pays back. Covered by the manual smoke test in Task 20.

- [ ] **Step 1: Create `Settings/UserSettings.cs`**

```csharp
namespace PcMonitor.App.Settings;

public sealed class UserSettings
{
    public bool ExplainerCollapsed { get; set; } = false;
    public DateTimeOffset? LastLaunch { get; set; }
}
```

- [ ] **Step 2: Create `Settings/SettingsStore.cs`**

```csharp
using System.Text.Json;
using PcMonitor.App.Composition;

namespace PcMonitor.App.Settings;

public sealed class SettingsStore
{
    private readonly string _path;
    public UserSettings Current { get; private set; } = new();

    public SettingsStore() : this(Paths.SettingsFile) { }

    public SettingsStore(string path)
    {
        _path = path;
        Load();
    }

    public void Load()
    {
        try
        {
            if (File.Exists(_path))
                Current = JsonSerializer.Deserialize<UserSettings>(File.ReadAllText(_path)) ?? new();
        }
        catch
        {
            Current = new UserSettings();
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(Current,
                new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}
```

- [ ] **Step 3: Wire into `Services`**

In `Composition/Services.cs`, add a property (the `= new()` initializer means no constructor changes are required):

```csharp
public Settings.SettingsStore Settings { get; } = new();
```

- [ ] **Step 4: Build with placeholder window**

Skip commit; will commit after the next task makes the build green.

---

### Task 17: Cockpit window skeleton + view model

**Files:**
- Create: `app/src/PcMonitor.App/ViewModels/CockpitViewModel.cs`
- Create: `app/src/PcMonitor.App/ViewModels/LiveTilesViewModel.cs`
- Create: `app/src/PcMonitor.App/ViewModels/IssueCardViewModel.cs`
- Create: `app/src/PcMonitor.App/ViewModels/SparklineViewModel.cs`
- Create: `app/src/PcMonitor.App/Views/CockpitWindow.xaml`
- Create: `app/src/PcMonitor.App/Views/CockpitWindow.xaml.cs`

- [ ] **Step 1: Create `ViewModels/LiveTilesViewModel.cs`**

```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PcMonitor.Core.Models;

namespace PcMonitor.App.ViewModels;

public partial class LiveTilesViewModel : ObservableObject
{
    [ObservableProperty] private string _cpuPercent = "--";
    [ObservableProperty] private string _ramText = "--";
    [ObservableProperty] private string _tempText = "--";
    [ObservableProperty] private string _driveCText = "--";
    [ObservableProperty] private bool _tempUnavailable;

    public ObservableCollection<ProcessRow> TopProcesses { get; } = new();

    public void Apply(SensorSnapshot s)
    {
        CpuPercent = s.CpuPercent is double cpu ? $"{cpu:F0}%" : "--";
        RamText = $"{s.RamUsedGb:F1} / {s.RamTotalGb:F0} GB";
        TempText = s.CpuPackageTempC is double t ? $"{t:F0}°C" : "--";
        TempUnavailable = s.CpuPackageTempC is null;
        DriveCText = s.DriveCFreeGb is double gb ? $"{gb:F0} GB free" : "--";
        TopProcesses.Clear();
        foreach (var p in s.TopProcesses.Take(5))
            TopProcesses.Add(new ProcessRow(p.Name, $"{p.CpuPercent:F1}", $"{p.RamMb / 1024.0:F1} GB"));
    }
}

public sealed record ProcessRow(string Name, string CpuPercent, string Ram);
```

- [ ] **Step 2: Create `ViewModels/IssueCardViewModel.cs`**

```csharp
using PcMonitor.Core.Models;

namespace PcMonitor.App.ViewModels;

public sealed class IssueCardViewModel
{
    public string RuleId { get; }
    public IssueSeverity Severity { get; }
    public string Title { get; }
    public string Detail { get; }
    public DateTimeOffset FirstSeen { get; }
    public string DurationText { get; }
    public bool IsRed => Severity == IssueSeverity.Red;
    public bool IsYellow => Severity == IssueSeverity.Yellow;

    public IssueCardViewModel(IssueState s, DateTimeOffset now)
    {
        RuleId = s.RuleId;
        Severity = s.Severity;
        Title = s.Title;
        Detail = s.Detail;
        FirstSeen = s.FirstSeen;
        DurationText = FormatDuration(now - s.FirstSeen);
    }

    private static string FormatDuration(TimeSpan d)
    {
        if (d.TotalSeconds < 60) return $"{(int)d.TotalSeconds}s";
        if (d.TotalMinutes < 60) return $"{(int)d.TotalMinutes} min";
        return $"{(int)d.TotalHours}h {d.Minutes}m";
    }
}
```

- [ ] **Step 3: Create `ViewModels/SparklineViewModel.cs`**

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using PcMonitor.Core.History;

namespace PcMonitor.App.ViewModels;

public partial class SparklineViewModel : ObservableObject
{
    private readonly IHistoryReader _history;
    [ObservableProperty] private string _cpu = "";
    [ObservableProperty] private string _ram = "";
    [ObservableProperty] private string _errors = "";
    [ObservableProperty] private bool _available;

    public SparklineViewModel(IHistoryReader history)
    {
        _history = history;
        _history.Changed += (_, _) => Refresh();
        Refresh();
    }

    public void Refresh()
    {
        var data = _history.ReadAll();
        Available = data.Count > 0;
        if (!Available) return;
        var ordered = data.OrderBy(e => e.Timestamp).TakeLast(24).ToList();
        Cpu = Spark(ordered.Select(e => e.CpuPercent ?? 0));
        Ram = Spark(ordered.Select(e => e.RamUsedGb ?? 0));
        Errors = Spark(ordered.Select(e => (double)((e.SystemErrorsLastHour ?? 0) + (e.AppErrorsLastHour ?? 0))));
    }

    private static string Spark(IEnumerable<double> values)
    {
        const string blocks = "▁▂▃▄▅▆▇█";
        var list = values.ToList();
        if (list.Count == 0) return "";
        var min = list.Min(); var max = list.Max();
        var range = Math.Max(0.001, max - min);
        var sb = new System.Text.StringBuilder(list.Count);
        foreach (var v in list)
        {
            var idx = (int)Math.Round((v - min) / range * (blocks.Length - 1));
            sb.Append(blocks[Math.Clamp(idx, 0, blocks.Length - 1)]);
        }
        return sb.ToString();
    }
}
```

- [ ] **Step 4: Create `ViewModels/CockpitViewModel.cs`**

```csharp
using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PcMonitor.App.Composition;
using PcMonitor.Core.Models;

namespace PcMonitor.App.ViewModels;

public partial class CockpitViewModel : ObservableObject, IDisposable
{
    private readonly Services _svc;
    private readonly DispatcherTimer _timer;

    public LiveTilesViewModel Live { get; }
    public SparklineViewModel Sparkline { get; }
    public ObservableCollection<IssueCardViewModel> Issues { get; } = new();

    [ObservableProperty] private string _healthLabel = "All clear";
    [ObservableProperty] private System.Windows.Media.Brush _healthBrush =
        new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x3F, 0xB9, 0x50));
    [ObservableProperty] private bool _explainerCollapsed;
    [ObservableProperty] private bool _captureRunning;
    [ObservableProperty] private string? _tempBanner;

    public IRelayCommand<string> CaptureCommand { get; }
    public IRelayCommand ToggleExplainerCommand { get; }

    public event EventHandler<CaptureKind>? CaptureRequested;

    public CockpitViewModel(Services svc)
    {
        _svc = svc;
        Live = new LiveTilesViewModel();
        Sparkline = new SparklineViewModel(svc.History);
        ExplainerCollapsed = svc.Settings.Current.ExplainerCollapsed;
        if (!svc.Sensors.TempSensorsAvailable)
            TempBanner = "Temperature sensors unavailable (LibreHardwareMonitor could not load). Temp tile and thermal rules are disabled.";

        CaptureCommand = new RelayCommand<string>(kind =>
        {
            if (kind == "Diagnostic") CaptureRequested?.Invoke(this, CaptureKind.Diagnostic);
            else if (kind == "LiveProbe") CaptureRequested?.Invoke(this, CaptureKind.LiveProbe);
        }, _ => !CaptureRunning);

        ToggleExplainerCommand = new RelayCommand(() =>
        {
            ExplainerCollapsed = !ExplainerCollapsed;
            svc.Settings.Current.ExplainerCollapsed = ExplainerCollapsed;
            svc.Settings.Save();
        });

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => Tick();
        _timer.Start();
    }

    private void Tick()
    {
        try
        {
            var now = DateTimeOffset.UtcNow;
            var snap = _svc.Sensors.Read(now);
            Live.Apply(snap);
            var active = _svc.Issues.Evaluate(snap);

            Issues.Clear();
            foreach (var i in active) Issues.Add(new IssueCardViewModel(i, now));

            if (active.Any(i => i.Severity == IssueSeverity.Red))
            {
                HealthLabel = "Problems";
                HealthBrush = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0xF8, 0x51, 0x49));
            }
            else if (active.Any(i => i.Severity == IssueSeverity.Yellow))
            {
                HealthLabel = "Issues";
                HealthBrush = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0xD2, 0x99, 0x22));
            }
            else
            {
                HealthLabel = "All clear";
                HealthBrush = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x3F, 0xB9, 0x50));
            }
        }
        catch (Exception ex)
        {
            try
            {
                Directory.CreateDirectory(Paths.AppDataFolder);
                File.AppendAllText(Paths.LogFile, $"{DateTime.UtcNow:o} tick error: {ex}\n");
            }
            catch { }
        }
    }

    public void Dispose() => _timer.Stop();
}
```

- [ ] **Step 5: Create `Views/CockpitWindow.xaml`**

```xml
<Window x:Class="PcMonitor.App.Views.CockpitWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Marsh PC Monitor" Width="960" Height="640"
        Background="#0D1117" Foreground="#C9D1D9" FontFamily="Segoe UI">
  <Window.Resources>
    <Style TargetType="TextBlock">
      <Setter Property="Foreground" Value="#C9D1D9"/>
    </Style>
  </Window.Resources>
  <DockPanel LastChildFill="True" Margin="16">
    <!-- Header -->
    <Border DockPanel.Dock="Top" Padding="0,0,0,12">
      <StackPanel Orientation="Horizontal">
        <TextBlock Text="Marsh PC Monitor" FontSize="20" FontWeight="SemiBold"/>
        <Ellipse Width="14" Height="14" Margin="20,5,8,0" Fill="{Binding HealthBrush}"/>
        <TextBlock Text="{Binding HealthLabel}" FontSize="16" VerticalAlignment="Center"/>
      </StackPanel>
    </Border>

    <!-- Temp banner -->
    <Border DockPanel.Dock="Top" Background="#21262D" CornerRadius="6" Padding="10"
            Margin="0,0,0,8" Visibility="{Binding TempBanner, Converter={StaticResource NullToCollapsed}}">
      <TextBlock Text="{Binding TempBanner}" TextWrapping="Wrap"/>
    </Border>

    <!-- Explainer -->
    <Expander DockPanel.Dock="Top" Header="How to use this"
              IsExpanded="{Binding ExplainerCollapsed, Converter={StaticResource InvertBool}}"
              Margin="0,0,0,12" Foreground="#C9D1D9">
      <TextBlock TextWrapping="Wrap" Margin="0,8,0,4">
        This is your "is anything wrong?" cockpit. Live tiles below show what's
        happening right now. Issue cards appear when something crosses a threshold.
        When you want a deeper look:
        1. Click Capture Diagnostic (full snapshot, ~10–20s) or Capture Live Probe (5s trace).
        2. When it finishes, hit "Copy Claude prompt" — it puts a ready-to-paste prompt
           with the file path on your clipboard.
        3. Paste into Claude Code in WSL and let it analyze.
        Files land in Documents\SysLogs\ alongside the hourly snapshots the scheduled
        task is already collecting.
      </TextBlock>
    </Expander>

    <!-- Capture buttons -->
    <Border DockPanel.Dock="Bottom" Padding="0,8,0,0">
      <StackPanel Orientation="Horizontal" HorizontalAlignment="Center">
        <Button Content="Capture Diagnostic" Margin="6" Padding="20,8" MinWidth="200"
                Command="{Binding CaptureCommand}" CommandParameter="Diagnostic"/>
        <Button Content="Capture Live Probe (5s)" Margin="6" Padding="20,8" MinWidth="200"
                Command="{Binding CaptureCommand}" CommandParameter="LiveProbe"/>
      </StackPanel>
    </Border>

    <!-- Sparkline footer -->
    <Border DockPanel.Dock="Bottom" Padding="0,4,0,8"
            Visibility="{Binding Sparkline.Available, Converter={StaticResource BoolToVisible}}">
      <TextBlock FontFamily="Cascadia Mono, Consolas">
        <Run Text="24h: CPU "/><Run Text="{Binding Sparkline.Cpu}"/>
        <Run Text="   RAM "/><Run Text="{Binding Sparkline.Ram}"/>
        <Run Text="   Errors "/><Run Text="{Binding Sparkline.Errors}"/>
      </TextBlock>
    </Border>

    <!-- Issues + Live -->
    <Grid>
      <Grid.RowDefinitions>
        <RowDefinition Height="Auto"/>
        <RowDefinition Height="*"/>
      </Grid.RowDefinitions>

      <ItemsControl Grid.Row="0" ItemsSource="{Binding Issues}" Margin="0,0,0,12">
        <ItemsControl.ItemTemplate>
          <DataTemplate>
            <Border Margin="0,4" Padding="12" CornerRadius="6"
                    Background="#161B22" BorderThickness="1" BorderBrush="#30363D">
              <StackPanel>
                <StackPanel Orientation="Horizontal">
                  <Ellipse Width="10" Height="10" Margin="0,0,8,0"
                           Fill="{Binding Severity, Converter={StaticResource SeverityToBrush}}"/>
                  <TextBlock Text="{Binding Title}" FontWeight="SemiBold"/>
                  <TextBlock Text="{Binding DurationText}" Margin="12,0,0,0" Opacity="0.7"/>
                </StackPanel>
                <TextBlock Text="{Binding Detail}" Margin="18,4,0,0" Opacity="0.85"/>
              </StackPanel>
            </Border>
          </DataTemplate>
        </ItemsControl.ItemTemplate>
      </ItemsControl>

      <Border Grid.Row="1" Padding="12" Background="#161B22" CornerRadius="6"
              BorderThickness="1" BorderBrush="#30363D">
        <StackPanel>
          <TextBlock Text="LIVE" FontWeight="SemiBold" Margin="0,0,0,8"/>
          <StackPanel Orientation="Horizontal">
            <TextBlock Text="CPU "/><TextBlock Text="{Binding Live.CpuPercent}" Margin="0,0,16,0"/>
            <TextBlock Text="RAM "/><TextBlock Text="{Binding Live.RamText}" Margin="0,0,16,0"/>
            <TextBlock Text="Pkg Temp "/><TextBlock Text="{Binding Live.TempText}" Margin="0,0,16,0"/>
            <TextBlock Text="C: "/><TextBlock Text="{Binding Live.DriveCText}"/>
          </StackPanel>
          <TextBlock Text="Top processes" Margin="0,12,0,4" Opacity="0.7"/>
          <DataGrid ItemsSource="{Binding Live.TopProcesses}" AutoGenerateColumns="False"
                    HeadersVisibility="Column" GridLinesVisibility="None"
                    Background="Transparent" Foreground="#C9D1D9"
                    RowBackground="Transparent" AlternatingRowBackground="#1B222B"
                    BorderThickness="0">
            <DataGrid.Columns>
              <DataGridTextColumn Header="Name" Binding="{Binding Name}" Width="*"/>
              <DataGridTextColumn Header="CPU %" Binding="{Binding CpuPercent}" Width="100"/>
              <DataGridTextColumn Header="RAM" Binding="{Binding Ram}" Width="120"/>
            </DataGrid.Columns>
          </DataGrid>
        </StackPanel>
      </Border>
    </Grid>
  </DockPanel>
</Window>
```

- [ ] **Step 6: Create `Views/CockpitWindow.xaml.cs`**

```csharp
using System.Windows;
using PcMonitor.App.ViewModels;
using PcMonitor.App.Views.Dialogs;
using PcMonitor.Core.Models;

namespace PcMonitor.App.Views;

public partial class CockpitWindow : Window
{
    public CockpitWindow()
    {
        InitializeComponent();
        var svc = ((App)Application.Current).Services!;
        var vm = new CockpitViewModel(svc);
        DataContext = vm;
        vm.CaptureRequested += (_, kind) => OpenCaptureDialog(kind, vm, svc);
        Closed += (_, _) => vm.Dispose();
    }

    private void OpenCaptureDialog(CaptureKind kind, CockpitViewModel vm, Composition.Services svc)
    {
        vm.CaptureRunning = true;
        var dialog = new CaptureDialog(svc.Capture, kind) { Owner = this };
        dialog.Closed += (_, _) => vm.CaptureRunning = false;
        dialog.Show();
    }
}
```

- [ ] **Step 7: Create resource dictionary `Views/SharedConverters.xaml`**

```xml
<ResourceDictionary
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:conv="clr-namespace:PcMonitor.App.Converters">
  <conv:NullToCollapsedConverter x:Key="NullToCollapsed"/>
  <conv:InvertBoolConverter x:Key="InvertBool"/>
  <conv:BoolToVisibleConverter x:Key="BoolToVisible"/>
  <conv:SeverityToBrushConverter x:Key="SeverityToBrush"/>
</ResourceDictionary>
```

Reference this from `App.xaml`:

```xml
<Application x:Class="PcMonitor.App.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             StartupUri="Views/CockpitWindow.xaml">
  <Application.Resources>
    <ResourceDictionary>
      <ResourceDictionary.MergedDictionaries>
        <ResourceDictionary Source="Views/SharedConverters.xaml"/>
      </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
  </Application.Resources>
</Application>
```

- [ ] **Step 8: Create converters in `Converters/`**

```csharp
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using PcMonitor.Core.Models;

namespace PcMonitor.App.Converters;

public sealed class NullToCollapsedConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object value, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}

public sealed class InvertBoolConverter : IValueConverter
{
    public object Convert(object? v, Type t, object? p, CultureInfo c) => !(bool)(v ?? false);
    public object ConvertBack(object v, Type t, object? p, CultureInfo c) => !(bool)v;
}

public sealed class BoolToVisibleConverter : IValueConverter
{
    public object Convert(object? v, Type t, object? p, CultureInfo c)
        => (bool)(v ?? false) ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object v, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}

public sealed class SeverityToBrushConverter : IValueConverter
{
    public object Convert(object? v, Type t, object? p, CultureInfo c) => v switch
    {
        IssueSeverity.Red => (Brush)new SolidColorBrush(Color.FromRgb(0xF8, 0x51, 0x49)),
        IssueSeverity.Yellow => new SolidColorBrush(Color.FromRgb(0xD2, 0x99, 0x22)),
        _ => Brushes.Gray,
    };
    public object ConvertBack(object v, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}
```

- [ ] **Step 9: Build (Cockpit window still needs `CaptureDialog`, see Task 18)**

The build will fail until Task 18 lands. That's expected — these two tasks are paired.

---

### Task 18: Capture dialog + result modal

**Files:**
- Create: `app/src/PcMonitor.App/Views/Dialogs/CaptureDialog.xaml`
- Create: `app/src/PcMonitor.App/Views/Dialogs/CaptureDialog.xaml.cs`
- Create: `app/src/PcMonitor.App/ViewModels/CaptureDialogViewModel.cs`

- [ ] **Step 1: Create `ViewModels/CaptureDialogViewModel.cs`**

```csharp
using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PcMonitor.Core.Capture;
using PcMonitor.Core.Models;

namespace PcMonitor.App.ViewModels;

public partial class CaptureDialogViewModel : ObservableObject
{
    private readonly ICaptureService _capture;
    private readonly CaptureKind _kind;
    private readonly CancellationTokenSource _cts = new();

    [ObservableProperty] private string _status = "Running…";
    [ObservableProperty] private bool _isRunning = true;
    [ObservableProperty] private bool _isSuccess;
    [ObservableProperty] private bool _isFailure;
    [ObservableProperty] private string? _windowsPath;
    [ObservableProperty] private string? _wslPath;
    [ObservableProperty] private string? _suggestedPrompt;
    [ObservableProperty] private string? _stdErr;
    public ObservableCollection<string> Lines { get; } = new();

    public IRelayCommand CancelCommand { get; }
    public IRelayCommand CopyPromptCommand { get; }
    public IRelayCommand OpenInExplorerCommand { get; }

    public CaptureDialogViewModel(ICaptureService capture, CaptureKind kind)
    {
        _capture = capture;
        _kind = kind;
        CancelCommand = new RelayCommand(() => _cts.Cancel());
        CopyPromptCommand = new RelayCommand(
            () => { if (SuggestedPrompt is not null) Clipboard.SetText(SuggestedPrompt); },
            () => SuggestedPrompt is not null);
        OpenInExplorerCommand = new RelayCommand(
            () =>
            {
                if (WindowsPath is null) return;
                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{WindowsPath}\"");
            },
            () => WindowsPath is not null);
    }

    public async Task RunAsync()
    {
        try
        {
            var result = await _capture.RunAsync(_kind,
                line => Application.Current.Dispatcher.Invoke(() => Lines.Add(line.Text)),
                _cts.Token);
            IsRunning = false;
            if (result.Cancelled) { Status = "Cancelled."; IsFailure = true; return; }
            if (!result.Success)
            {
                Status = "Capture failed.";
                IsFailure = true;
                StdErr = result.StdErr;
                return;
            }
            IsSuccess = true;
            Status = "Capture complete.";
            WindowsPath = result.WindowsPath;
            WslPath = result.WslPath;
            SuggestedPrompt = _kind == CaptureKind.Diagnostic
                ? $"Read {result.WslPath} and give me the top 5 issues to address."
                : $"Read {result.WslPath} and tell me what's hammering the CPU right now.";
            CopyPromptCommand.NotifyCanExecuteChanged();
            OpenInExplorerCommand.NotifyCanExecuteChanged();
        }
        catch (Exception ex)
        {
            IsRunning = false;
            IsFailure = true;
            Status = $"Error: {ex.Message}";
        }
    }
}
```

- [ ] **Step 2: Create `Views/Dialogs/CaptureDialog.xaml`**

```xml
<Window x:Class="PcMonitor.App.Views.Dialogs.CaptureDialog"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Capture" Width="640" Height="480"
        Background="#0D1117" Foreground="#C9D1D9" FontFamily="Segoe UI"
        WindowStartupLocation="CenterOwner">
  <Grid Margin="16">
    <Grid.RowDefinitions>
      <RowDefinition Height="Auto"/>
      <RowDefinition Height="*"/>
      <RowDefinition Height="Auto"/>
      <RowDefinition Height="Auto"/>
    </Grid.RowDefinitions>

    <TextBlock Text="{Binding Status}" FontSize="16" FontWeight="SemiBold"/>

    <ListBox Grid.Row="1" ItemsSource="{Binding Lines}" Margin="0,8" Background="#161B22"
             Foreground="#C9D1D9" BorderThickness="1" BorderBrush="#30363D"
             FontFamily="Cascadia Mono, Consolas"/>

    <StackPanel Grid.Row="2" Visibility="{Binding IsSuccess, Converter={StaticResource BoolToVisible}}">
      <TextBlock Text="Suggested Claude Code prompt:" Margin="0,8,0,4" Opacity="0.7"/>
      <Border Background="#161B22" CornerRadius="6" Padding="10" BorderBrush="#30363D" BorderThickness="1">
        <TextBox Text="{Binding SuggestedPrompt, Mode=OneWay}" IsReadOnly="True"
                 Background="Transparent" Foreground="#C9D1D9" BorderThickness="0"
                 TextWrapping="Wrap" AcceptsReturn="True"/>
      </Border>
    </StackPanel>

    <StackPanel Grid.Row="3" Orientation="Horizontal" Margin="0,12,0,0" HorizontalAlignment="Right">
      <Button Content="Cancel" Padding="14,6" Margin="0,0,8,0"
              Command="{Binding CancelCommand}"
              Visibility="{Binding IsRunning, Converter={StaticResource BoolToVisible}}"/>
      <Button Content="Copy Claude prompt" Padding="14,6" Margin="0,0,8,0"
              Command="{Binding CopyPromptCommand}"
              Visibility="{Binding IsSuccess, Converter={StaticResource BoolToVisible}}"/>
      <Button Content="Open in Explorer" Padding="14,6" Margin="0,0,8,0"
              Command="{Binding OpenInExplorerCommand}"
              Visibility="{Binding IsSuccess, Converter={StaticResource BoolToVisible}}"/>
      <Button Content="Close" Padding="14,6" Click="OnClose"/>
    </StackPanel>
  </Grid>
</Window>
```

- [ ] **Step 3: Create `Views/Dialogs/CaptureDialog.xaml.cs`**

```csharp
using System.Windows;
using PcMonitor.App.ViewModels;
using PcMonitor.Core.Capture;
using PcMonitor.Core.Models;

namespace PcMonitor.App.Views.Dialogs;

public partial class CaptureDialog : Window
{
    private readonly CaptureDialogViewModel _vm;

    public CaptureDialog(ICaptureService capture, CaptureKind kind)
    {
        InitializeComponent();
        _vm = new CaptureDialogViewModel(capture, kind);
        DataContext = _vm;
        Loaded += async (_, _) => await _vm.RunAsync();
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
```

- [ ] **Step 4: Build the full app**

Run: `dotnet build`
Expected: Build succeeds for `PcMonitor.Core`, `PcMonitor.App`, and `PcMonitor.Core.Tests`.

- [ ] **Step 5: Run the tests one more time**

Run: `dotnet test`
Expected: All previously added tests still pass.

- [ ] **Step 6: Commit the WPF UI as one unit (Tasks 15–18)**

```bash
git add app/
git commit -m "app: WPF cockpit window, view models, capture dialog, single-instance plumbing"
git push 2>/dev/null || echo "no remote configured; skip"
```

---

## Phase 6 — Install and acceptance smoke test

### Task 19: `install.ps1` and `publish.ps1`

**Files:**
- Create: `app/install/install.ps1`
- Create: `app/install/publish.ps1`
- Create: `app/install/README.md`

- [ ] **Step 1: Create `app/install/publish.ps1`**

```powershell
# Build a self-contained single-file PcMonitor.exe.
param(
  [string]$Configuration = "Release",
  [switch]$FrameworkDependent
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $root "src/PcMonitor.App/PcMonitor.App.csproj"
$selfContained = if ($FrameworkDependent) { "false" } else { "true" }

dotnet publish $proj -c $Configuration -r win-x64 `
  --self-contained $selfContained `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true

$outDir = Join-Path $root "src/PcMonitor.App/bin/$Configuration/net8.0-windows10.0.19041.0/win-x64/publish"
Write-Host "Published to: $outDir"
```

- [ ] **Step 2: Create `app/install/install.ps1`**

```powershell
# Install Marsh PC Monitor for the current user.
param(
  [switch]$Publish
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$appName = "PcMonitor"
$installDir = Join-Path $env:LOCALAPPDATA $appName
$scriptsDest = Join-Path $env:USERPROFILE "Documents\SysLogs\scripts"

if ($Publish) {
  & (Join-Path $PSScriptRoot "publish.ps1")
}

$publishDir = Join-Path $root "src/PcMonitor.App/bin/Release/net8.0-windows10.0.19041.0/win-x64/publish"
$exe = Join-Path $publishDir "PcMonitor.exe"
if (-not (Test-Path $exe)) {
  throw "PcMonitor.exe not found at $exe. Run install.ps1 -Publish first."
}

New-Item -ItemType Directory -Force -Path $installDir | Out-Null
Copy-Item -Force $exe $installDir

# Copy the companion scripts so the capture buttons work.
$filesDir = Join-Path $root "../files"
New-Item -ItemType Directory -Force -Path $scriptsDest | Out-Null
foreach ($name in @("diagnose.ps1", "live-probe.ps1", "collect-stats.ps1")) {
  $src = Join-Path $filesDir $name
  if (Test-Path $src) { Copy-Item -Force $src $scriptsDest }
}

# Start Menu shortcut.
$startMenu = [Environment]::GetFolderPath("StartMenu")
$shortcut = Join-Path $startMenu "Programs\Marsh PC Monitor.lnk"
$shell = New-Object -ComObject WScript.Shell
$sc = $shell.CreateShortcut($shortcut)
$sc.TargetPath = Join-Path $installDir "PcMonitor.exe"
$sc.WorkingDirectory = $installDir
$sc.IconLocation = Join-Path $installDir "PcMonitor.exe"
$sc.Save()

Write-Host "Installed:  $installDir\PcMonitor.exe"
Write-Host "Shortcut:   $shortcut"
Write-Host "Scripts:    $scriptsDest"
Write-Host ""
Write-Host "Launch from Start Menu, or run: $installDir\PcMonitor.exe"
```

- [ ] **Step 3: Create `app/install/README.md`**

```markdown
# Install — Marsh PC Monitor

From a Windows PowerShell prompt at the repo root:

```powershell
.\app\install\install.ps1 -Publish
```

This will:

1. Publish `PcMonitor.exe` (self-contained, single file, win-x64).
2. Copy it to `%LocalAppData%\PcMonitor\`.
3. Copy `diagnose.ps1`, `live-probe.ps1`, `collect-stats.ps1` to `Documents\SysLogs\scripts\`.
4. Create a Start Menu shortcut.

To rebuild and reinstall after code changes, re-run the same command.

If you prefer a smaller framework-dependent build, add `-FrameworkDependent` to `publish.ps1`. You'll need the .NET 8 Desktop Runtime installed.
```

- [ ] **Step 4: Commit**

```bash
git add app/install/
git commit -m "install: add publish.ps1 + install.ps1 + README"
git push 2>/dev/null || echo "no remote configured; skip"
```

---

### Task 20: Acceptance smoke test on the target PC

This is the manual verification gate. Run on Marsh PC.

- [ ] **Step 1: Build and publish**

From Windows PowerShell at the repo root:

```powershell
.\app\install\install.ps1 -Publish
```

Expected: prints install paths, no errors.

- [ ] **Step 2: Launch from Start Menu**

Click "Marsh PC Monitor". Expected: window opens in under 2 seconds.

- [ ] **Step 3: Verify each acceptance criterion from the spec**

Walk the spec's "Acceptance checklist for handoff" and check each item:

- [ ] App builds and publishes from the documented command.
- [ ] Cold launch opens the cockpit in under 2 seconds on the target PC.
- [ ] With `Documents\SysLogs\hourly\` absent, the app still opens and live tiles work. (Test by temporarily renaming the `hourly` folder before launch.)
- [ ] Capture buttons are disabled while a capture is running and re-enabled afterward.
- [ ] Diagnostic capture produces a `diagnostic_*.txt` path and a Claude prompt containing the correct `/mnt/c/...` path.
- [ ] Live probe capture produces a `live_*.txt` path and a Claude prompt containing the correct `/mnt/c/...` path.
- [ ] Killing or failing a capture does not crash the app.
- [ ] Unavailable LHM/temp sensors disable only temp/throttle tiles/rules. (Hard to force on the target machine; if temps are visible, mark "verified by inverse — degraded path code path was tested via unit tests".)
- [ ] Single-instance launch activates the first window. (Run the exe twice; second should foreground the first.)
- [ ] Rule tests cover all red/yellow thresholds. (Already covered by `dotnet test`.)

- [ ] **Step 4: Verify the "Copy Claude prompt" round-trip**

After a successful Diagnostic capture, click "Copy Claude prompt", paste into Claude Code in WSL, and confirm Claude reads the file successfully and returns analysis.

- [ ] **Step 5: Record findings**

If everything passes, create a final `docs/superpowers/notes/2026-05-26-pc-monitor-v1-smoke-test.md` noting:

- Target machine details (CPU, OS build).
- Whether temp sensors initialized cleanly via LHM.
- Approximate cold-start time.
- Any quirks observed.
- Any thresholds that fired immediately and felt wrong (input for v1.1 settings work).

- [ ] **Step 6: Commit smoke-test notes**

```bash
git add docs/
git commit -m "notes: v1 acceptance smoke test results on Marsh PC"
git push 2>/dev/null || echo "no remote configured; skip"
```

---

## Done

If every box above is checked, v1 is shippable for the user's own machine. The follow-up backlog (deferred from spec):

- Tray mode (plumbing already in `App.xaml.cs`).
- Settings pane for threshold tuning.
- GPU temps for the dGPU.
- Auto-update.
- Re-tune thresholds based on real-world false positives observed during use.
