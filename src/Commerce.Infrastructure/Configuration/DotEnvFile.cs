namespace Commerce.Infrastructure.Configuration;

/// <summary>
/// Carrega o arquivo .env que fica ao lado do docker-compose.yml como variáveis de ambiente do processo.
/// Assim <c>dotnet run</c> usa as mesmas credenciais do container sem que elas sejam gravadas em appsettings.
/// Variáveis já definidas no ambiente têm precedência e nunca são sobrescritas.
/// </summary>
public static class DotEnvFile
{
    private const string ComposeFileName = "docker-compose.yml";

    public static void Load(string fileName = ".env")
    {
        var path = Find(fileName, Directory.GetCurrentDirectory()) ?? Find(fileName, AppContext.BaseDirectory);
        if (path is null)
            return;

        foreach (var line in File.ReadAllLines(path))
        {
            var entry = line.Trim();
            var separator = entry.IndexOf('=');
            if (entry.Length == 0 || entry.StartsWith('#') || separator <= 0)
                continue;

            var key = entry[..separator].Trim();
            var value = entry[(separator + 1)..].Trim().Trim('"', '\'');

            if (Environment.GetEnvironmentVariable(key) is null)
                Environment.SetEnvironmentVariable(key, value);
        }
    }

    private static string? Find(string fileName, string startDirectory)
    {
        for (var directory = new DirectoryInfo(startDirectory); directory is not null; directory = directory.Parent)
        {
            if (!File.Exists(Path.Combine(directory.FullName, ComposeFileName)))
                continue;

            var candidate = Path.Combine(directory.FullName, fileName);
            return File.Exists(candidate) ? candidate : null;
        }

        return null;
    }
}
