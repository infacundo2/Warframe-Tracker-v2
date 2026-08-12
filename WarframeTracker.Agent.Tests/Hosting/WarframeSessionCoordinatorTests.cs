using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WarframeTracker.Agent.Configuration;
using WarframeTracker.Agent.Diagnostics;
using WarframeTracker.Agent.Hosting;
using WarframeTracker.Agent.ProcessDetection;
using Xunit;

namespace WarframeTracker.Agent.Tests.Hosting;

public sealed class WarframeSessionCoordinatorTests
{
    [Fact]
    public async Task Cancels_session_and_returns_to_waiting_when_Warframe_stops()
    {
        var detector = new FakeDetector();
        var status = new AgentStatus();
        using var coordinator = new WarframeSessionCoordinator(
            detector,
            status,
            Options.Create(new TrackerAgentOptions()),
            NullLogger<WarframeSessionCoordinator>.Instance);

        await coordinator.StartAsync(CancellationToken.None);
        Assert.Equal(AgentState.WaitingForWarframe, status.Current.State);

        detector.Start(555);
        var sessionToken = coordinator.SessionCancellationToken;
        Assert.Equal(AgentState.WarframeRunning, status.Current.State);
        Assert.Equal(555, status.Current.WarframeProcessId);
        Assert.True(sessionToken.CanBeCanceled);

        detector.Stop(555);
        Assert.True(sessionToken.IsCancellationRequested);
        Assert.Equal(AgentState.WaitingForWarframe, status.Current.State);
        Assert.Null(status.Current.WarframeProcessId);

        await coordinator.StopAsync(CancellationToken.None);
        Assert.Equal(AgentState.Stopped, status.Current.State);
    }

    [Fact]
    public async Task Disabled_agent_does_not_create_a_Warframe_session()
    {
        var detector = new FakeDetector();
        var status = new AgentStatus();
        using var coordinator = new WarframeSessionCoordinator(
            detector,
            status,
            Options.Create(new TrackerAgentOptions { Enabled = false }),
            NullLogger<WarframeSessionCoordinator>.Instance);

        await coordinator.StartAsync(CancellationToken.None);
        detector.Start(999);

        Assert.Equal(AgentState.Disabled, status.Current.State);
        Assert.False(coordinator.SessionCancellationToken.CanBeCanceled);
    }

    private sealed class FakeDetector : IWarframeProcessDetector
    {
        public event EventHandler<WarframeProcessEvent>? WarframeStarted;
        public event EventHandler<WarframeProcessEvent>? WarframeStopped;

        public int? CurrentProcessId { get; private set; }

        public void Start(int processId)
        {
            CurrentProcessId = processId;
            WarframeStarted?.Invoke(this, new WarframeProcessEvent(
                processId,
                DateTimeOffset.UtcNow));
        }

        public void Stop(int processId)
        {
            CurrentProcessId = null;
            WarframeStopped?.Invoke(this, new WarframeProcessEvent(
                processId,
                DateTimeOffset.UtcNow));
        }
    }
}
