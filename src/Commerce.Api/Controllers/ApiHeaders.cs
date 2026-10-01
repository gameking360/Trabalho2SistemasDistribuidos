namespace Commerce.Api.Controllers;

public static class ApiHeaders
{
    /// <summary>Chave opcional (UUID) usada como messageId para tornar o reenvio de uma requisição seguro.</summary>
    public const string IdempotencyKey = "Idempotency-Key";
}
