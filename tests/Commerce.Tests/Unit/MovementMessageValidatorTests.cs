using Commerce.Contracts.Movements;
using Commerce.Tests.Support;

namespace Commerce.Tests.Unit;

public sealed class MovementMessageValidatorTests
{
    [Theory]
    [InlineData("Entrada")]
    [InlineData("Saída")]
    public void Validate_ValidMovement_ReturnsNoErrors(string movementType)
    {
        var movement = TestMessages.Movement(movementType: movementType, notify: true, recipients: ["cliente@exemplo.com"]);

        Assert.Empty(MovementMessageValidator.Validate(movement));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void Validate_QuantityNotGreaterThanZero_ReturnsQuantityError(int quantity)
    {
        var movement = TestMessages.Movement(items: [TestMessages.Item(quantity: quantity)]);

        var error = Assert.Single(MovementMessageValidator.Validate(movement));
        Assert.Equal("items[0].quantity", error.Field);
        Assert.Equal("A quantidade do item deve ser maior que zero.", error.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Validate_ItemWithoutCode_ReturnsCodeError(string? code)
    {
        var movement = TestMessages.Movement(items: [TestMessages.Item(code: code!)]);

        var error = Assert.Single(MovementMessageValidator.Validate(movement));
        Assert.Equal("items[0].code", error.Field);
        Assert.Equal("O código do item é obrigatório.", error.Message);
    }

    [Fact]
    public void Validate_MovementWithoutItems_ReturnsItemsError()
    {
        var withEmptyList = TestMessages.Movement(items: []);
        var withNullList = TestMessages.Movement() with { Items = null! };

        foreach (var movement in new[] { withEmptyList, withNullList })
        {
            var error = Assert.Single(MovementMessageValidator.Validate(movement));
            Assert.Equal("items", error.Field);
            Assert.Equal("A movimentação deve possuir ao menos um item.", error.Message);
        }
    }

    [Theory]
    [InlineData("Transferencia")]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_InvalidMovementType_ReturnsMovementTypeError(string? movementType)
    {
        var movement = TestMessages.Movement(movementType: movementType!);

        var error = Assert.Single(MovementMessageValidator.Validate(movement));
        Assert.Equal("movementType", error.Field);
        Assert.Contains("Valores aceitos: Entrada ou Saída", error.Message);
    }

    [Fact]
    public void Validate_NotifyWithoutRecipients_ReturnsRecipientsError()
    {
        var movement = TestMessages.Movement(notify: true, recipients: []);

        var error = Assert.Single(MovementMessageValidator.Validate(movement));
        Assert.Equal("recipients", error.Field);
        Assert.Equal("Informe ao menos um destinatário quando notify = true.", error.Message);
    }

    [Fact]
    public void Validate_NotifyFalseWithoutRecipients_IsValid()
    {
        var movement = TestMessages.Movement(notify: false, recipients: []);

        Assert.Empty(MovementMessageValidator.Validate(movement));
    }

    [Fact]
    public void Validate_MissingRequiredFields_ReturnsOneErrorPerField()
    {
        var movement = TestMessages.Movement(items: [TestMessages.Item() with { Description = "" }]) with
        {
            MessageId = Guid.Empty,
            RequestedAt = default
        };

        var fields = MovementMessageValidator.Validate(movement).Select(error => error.Field);

        Assert.Equal(new[] { "messageId", "requestedAt", "items[0].description" }, fields);
    }
}
