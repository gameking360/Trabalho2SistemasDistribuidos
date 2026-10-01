namespace Commerce.Contracts.Movements;

public static class MovementTypes
{
    public const string Entry = "Entrada";
    public const string Exit = "Saída";

    public static bool IsEntry(string? value) => string.Equals(value, Entry, StringComparison.OrdinalIgnoreCase);

    public static bool IsExit(string? value) => string.Equals(value, Exit, StringComparison.OrdinalIgnoreCase);

    public static bool IsValid(string? value) => IsEntry(value) || IsExit(value);
}
