using System.Text.Encodings.Web;
using System.Text.Json;

namespace Commerce.Contracts.Messaging;

/// <summary>
/// Formato JSON único para todas as mensagens trafegadas no RabbitMQ (camelCase, leitura sem diferenciar maiúsculas).
/// </summary>
public static class MessageJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        // Acentos legíveis no Management e nos logs; o conteúdo nunca é renderizado como HTML.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static byte[] Serialize<T>(T value) => JsonSerializer.SerializeToUtf8Bytes(value, Options);

    public static T Deserialize<T>(ReadOnlySpan<byte> body) =>
        JsonSerializer.Deserialize<T>(body, Options)
        ?? throw new JsonException("O corpo da mensagem está vazio (null).");
}
