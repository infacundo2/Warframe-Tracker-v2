using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WarframeTracker.Agent.Configuration;
using WarframeTracker.Agent.ProcessDetection;
using Xunit;

namespace WarframeTracker.Agent.Tests.ProcessDetection;

public sealed class WindowsWarframeProcessDetectorTests
{
    [Fact]
    public void System_query_finds_the_current_Windows_process_with_exe_suffix()
    {
        var executableName = Path.GetFileName(Environment.ProcessPath);
        Assert.False(string.IsNullOrWhiteSpace(executableName));

        var processes = new SystemProcessQuery().FindByName(executableName!);

        Assert.Contains(processes, process => process.ProcessId == Environment.ProcessId);
    }

    [Fact]
    public async Task Emits_started_and_stopped_only_on_state_changes()
    {
        var query = new FakeProcessQuery(
            Array.Empty<DetectedProcess>(),
            new[] { new DetectedProcess(1234) },
            new[] { new DetectedProcess(1234) },
            Array.Empty<DetectedProcess>());
        using var detector = CreateDetector(query);
        var started = new List<int>();
        var stopped = new List<int>();
        detector.WarframeStarted += (_, value) => started.Add(value.ProcessId);
        detector.WarframeStopped += (_, value) => stopped.Add(value.ProcessId);

        await detector.CheckNowAsync();
        await detector.CheckNowAsync();
        await detector.CheckNowAsync();
        await detector.CheckNowAsync();

        Assert.Equal(new[] { 1234 }, started);
        Assert.Equal(new[] { 1234 }, stopped);
        Assert.Null(detector.CurrentProcessId);
    }

    [Fact]
    public async Task Replaces_a_closed_process_with_another_running_instance()
    {
        var query = new FakeProcessQuery(
            new[] { new DetectedProcess(20) },
            new[] { new DetectedProcess(40) });
        using var detector = CreateDetector(query);
        var transitions = new List<string>();
        detector.WarframeStarted += (_, value) => transitions.Add($"start:{value.ProcessId}");
        detector.WarframeStopped += (_, value) => transitions.Add($"stop:{value.ProcessId}");

        await detector.CheckNowAsync();
        await detector.CheckNowAsync();

        Assert.Equal(new[] { "start:20", "stop:20", "start:40" }, transitions);
        Assert.Equal(40, detector.CurrentProcessId);
    }

    [Fact]
    public async Task Keeps_previous_state_when_process_query_fails_temporarily()
    {
        var query = new FakeProcessQuery(
            new[] { new DetectedProcess(77) },
            new InvalidOperationException("fallo simulado"),
            Array.Empty<DetectedProcess>());
        using var detector = CreateDetector(query);
        var stopped = 0;
        detector.WarframeStopped += (_, _) => stopped++;

        await detector.CheckNowAsync();
        await detector.CheckNowAsync();

        Assert.Equal(77, detector.CurrentProcessId);
        Assert.Equal(0, stopped);

        await detector.CheckNowAsync();

        Assert.Null(detector.CurrentProcessId);
        Assert.Equal(1, stopped);
    }

    private static WindowsWarframeProcessDetector CreateDetector(IProcessQuery query)
        => new(
            query,
            Options.Create(new TrackerAgentOptions()),
            NullLogger<WindowsWarframeProcessDetector>.Instance);

    private sealed class FakeProcessQuery : IProcessQuery
    {
        private readonly Queue<object> _results;

        public FakeProcessQuery(params object[] results)
            => _results = new Queue<object>(results);

        public IReadOnlyCollection<DetectedProcess> FindByName(string processName)
        {
            var next = _results.Dequeue();
            if (next is Exception exception)
                throw exception;
            return (IReadOnlyCollection<DetectedProcess>)next;
        }
    }
}
