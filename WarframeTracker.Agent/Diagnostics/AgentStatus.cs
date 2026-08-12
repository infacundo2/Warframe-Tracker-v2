namespace WarframeTracker.Agent.Diagnostics;

public sealed class AgentStatus
{
    private readonly object _gate = new();
    private AgentStatusSnapshot _current = new(
        AgentState.Starting,
        null,
        DateTimeOffset.UtcNow);

    public event EventHandler<AgentStatusSnapshot>? Changed;

    public AgentStatusSnapshot Current
    {
        get
        {
            lock (_gate)
                return _current;
        }
    }

    public void Set(AgentState state, int? processId = null)
    {
        AgentStatusSnapshot next;
        lock (_gate)
        {
            if (_current.State == state && _current.WarframeProcessId == processId)
                return;

            next = new AgentStatusSnapshot(state, processId, DateTimeOffset.UtcNow);
            _current = next;
        }

        Changed?.Invoke(this, next);
    }
}
