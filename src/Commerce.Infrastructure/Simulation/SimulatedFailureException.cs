namespace Commerce.Infrastructure.Simulation;

public sealed class SimulatedFailureException(string message) : Exception(message);
