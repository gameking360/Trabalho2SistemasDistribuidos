using Commerce.Infrastructure.Simulation;
using Microsoft.Extensions.Options;

namespace Commerce.Tests.Unit;

public sealed class FailureSimulatorTests
{
    [Fact]
    public void ThrowIfRequested_Disabled_NeverFails()
    {
        var simulator = Create(enabled: false);

        simulator.ThrowIfRequested([FailureSimulator.PermanentFailureMarker], attempt: 1);
    }

    [Fact]
    public void ThrowIfRequested_TemporaryMarker_FailsOnlyOnTheFirstAttempt()
    {
        var simulator = Create(enabled: true);

        Assert.Throws<SimulatedFailureException>(() => simulator.ThrowIfRequested([FailureSimulator.TemporaryFailureMarker], attempt: 1));
        simulator.ThrowIfRequested([FailureSimulator.TemporaryFailureMarker], attempt: 2);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(6)]
    public void ThrowIfRequested_PermanentMarker_FailsOnEveryAttempt(int attempt)
    {
        var simulator = Create(enabled: true);

        Assert.Throws<SimulatedFailureException>(() => simulator.ThrowIfRequested(["SKU-1", "falha-permanente"], attempt));
    }

    private static FailureSimulator Create(bool enabled) =>
        new(Options.Create(new FailureSimulationOptions { Enabled = enabled }));
}
