using Microsoft.Extensions.Hosting;

namespace Commerce.Tests.Integration.Infrastructure;

/// <summary>
/// Worker real (mesmo registro de serviços do Program.cs) executando dentro do teste.
/// </summary>
public sealed class WorkerHost(IHost host) : IAsyncDisposable
{
    public IServiceProvider Services => host.Services;

    public async ValueTask DisposeAsync()
    {
        await host.StopAsync();

        if (host is IAsyncDisposable asyncDisposable)
            await asyncDisposable.DisposeAsync();
        else
            host.Dispose();
    }
}
