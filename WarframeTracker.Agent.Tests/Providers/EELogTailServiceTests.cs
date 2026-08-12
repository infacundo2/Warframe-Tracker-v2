using WarframeTracker.Agent.Providers.EELog;
using Xunit;

namespace WarframeTracker.Agent.Tests.Providers;

public sealed class EELogTailServiceTests
{
    [Theory]
    [InlineData("Logged in successfully", EELogEventKind.SessionStarted)]
    [InlineData("GameRules mission started", EELogEventKind.MissionStarted)]
    [InlineData("GameRules mission shutdown", EELogEventKind.MissionEnded)]
    [InlineData("Loadout changed", EELogEventKind.InventoryHint)]
    [InlineData("private unrelated line", EELogEventKind.Unknown)]
    public void Classifies_without_exposing_line_content(string line, EELogEventKind expected)
        => Assert.Equal(expected, EELogTailService.Classify(line));
}
