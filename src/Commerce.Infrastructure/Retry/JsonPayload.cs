using System.Text;
using System.Text.Json;

namespace Commerce.Infrastructure.Retry;

/// <summary>
/// Converte payloads entre bytes AMQP e <see cref="JsonElement"/>, para que os envelopes de retry/DLQ guardem a
/// mensagem como objeto JSON (legível no RabbitMQ Management). Conteúdo que não é JSON válido é preservado
/// como texto, já que mensagens malformadas também precisam chegar à DLQ para análise.
/// </summary>
public static class JsonPayload
{
    public static JsonElement ToElement(byte[] body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return JsonSerializer.SerializeToElement(Encoding.UTF8.GetString(body));
        }
    }

    // Os contratos são objetos JSON; um payload do tipo string representa um conteúdo original que não era JSON.
    public static byte[] ToBody(JsonElement payload) =>
        payload.ValueKind == JsonValueKind.String
            ? Encoding.UTF8.GetBytes(payload.GetString() ?? string.Empty)
            : Encoding.UTF8.GetBytes(payload.GetRawText());

    public static bool AreEquivalent(byte[] left, byte[] right) => Normalize(left) == Normalize(right);

    private static string Normalize(byte[] body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            return JsonSerializer.Serialize(document.RootElement);
        }
        catch (JsonException)
        {
            return Encoding.UTF8.GetString(body);
        }
    }
}
