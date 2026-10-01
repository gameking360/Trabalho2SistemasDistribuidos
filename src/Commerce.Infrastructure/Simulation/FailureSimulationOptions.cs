namespace Commerce.Infrastructure.Simulation;

public sealed class FailureSimulationOptions
{
    public const string SectionName = "FailureSimulation";

    /// <summary>Desabilitado por padrão; o appsettings.Development.json dos workers habilita para demonstração.</summary>
    public bool Enabled { get; set; }

    /// <summary>Quantas tentativas iniciais falham quando o marcador de falha temporária é usado.</summary>
    public int TemporaryFailures { get; set; } = 1;
}
