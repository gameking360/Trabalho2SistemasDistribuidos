using Commerce.Api.Application;
using Commerce.Api.Controllers;
using Commerce.Api.Models;
using Commerce.Contracts.Messaging;
using Commerce.Contracts.Movements;
using Commerce.Tests.Support;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Commerce.Tests.Unit;

public sealed class StockMovementRequestServiceTests
{
    private readonly FakeMessagePublisher _publisher = new();
    private readonly StockMovementRequestService _service;

    public StockMovementRequestServiceTests()
    {
        _service = new StockMovementRequestService(_publisher, new FixedTimeProvider(TestMessages.Now),
            NullLogger<StockMovementRequestService>.Instance);
    }

    [Fact]
    public async Task CompleteSaleOrderAsync_ValidRequest_PublishesExitMovementAndReturnsMessageId()
    {
        var result = await _service.CompleteSaleOrderAsync(ValidRequest(), null, CancellationToken.None);

        Assert.Equal(MovementRequestStatus.Accepted, result.Status);

        var published = Assert.Single(_publisher.Messages);
        Assert.Equal(Exchanges.Movements, published.Exchange);
        Assert.Equal(RoutingKeys.MovementProcess, published.RoutingKey);

        var movement = _publisher.Deserialize<MovementMessage>(published);
        Assert.Equal(MovementTypes.Exit, movement.MovementType);
        Assert.Equal(result.MessageId, movement.MessageId);
        Assert.Equal(movement.MessageId.ToString(), published.MessageId);
        Assert.Equal(TestMessages.Now, movement.RequestedAt);
    }

    [Fact]
    public async Task RegisterPurchaseEntryAsync_ValidRequest_PublishesEntryMovement()
    {
        await _service.RegisterPurchaseEntryAsync(ValidRequest(), null, CancellationToken.None);

        var movement = _publisher.Deserialize<MovementMessage>(Assert.Single(_publisher.Messages));
        Assert.Equal(MovementTypes.Entry, movement.MovementType);
    }

    [Fact]
    public async Task CompleteSaleOrderAsync_IdempotencyKey_BecomesTheMessageId()
    {
        var idempotencyKey = Guid.NewGuid();

        var result = await _service.CompleteSaleOrderAsync(ValidRequest(), idempotencyKey, CancellationToken.None);

        Assert.Equal(idempotencyKey, result.MessageId);
        Assert.Equal(idempotencyKey.ToString(), Assert.Single(_publisher.Messages).MessageId);
    }

    [Fact]
    public async Task CompleteSaleOrderAsync_InvalidRequest_ReturnsErrorsAndPublishesNothing()
    {
        var request = new MovementRequest { Notify = true, Items = [TestMessages.Item(code: "", quantity: 0)] };

        var result = await _service.CompleteSaleOrderAsync(request, null, CancellationToken.None);

        Assert.Equal(MovementRequestStatus.Invalid, result.Status);
        Assert.Equal(new[] { "items[0].code", "items[0].quantity", "recipients" }, result.Errors.Select(error => error.Field));
        Assert.Empty(_publisher.Messages);
    }

    [Fact]
    public async Task CompleteSaleOrderAsync_BrokerUnavailable_ReportsFailureInsteadOfAccepting()
    {
        _publisher.BrokerUnavailable = true;

        var result = await _service.CompleteSaleOrderAsync(ValidRequest(), null, CancellationToken.None);

        Assert.Equal(MovementRequestStatus.BrokerUnavailable, result.Status);
        Assert.Null(result.Response);
    }

    [Fact]
    public async Task Controller_BrokerUnavailable_Returns503WithRetryAfter()
    {
        _publisher.BrokerUnavailable = true;
        var controller = CreateController();

        var response = await controller.Complete(ValidRequest(), null, CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(response.Result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, problem.StatusCode);
        Assert.Equal("5", controller.Response.Headers.RetryAfter.ToString());
    }

    [Fact]
    public async Task Controller_ValidRequest_Returns202WithMessageId()
    {
        var response = await CreateController().Complete(ValidRequest(), null, CancellationToken.None);

        var accepted = Assert.IsType<AcceptedResult>(response.Result);
        var body = Assert.IsType<MovementAcceptedResponse>(accepted.Value);
        Assert.NotEqual(Guid.Empty, body.MessageId);
    }

    [Fact]
    public async Task Controller_InvalidRequest_Returns400WithFieldErrors()
    {
        var response = await CreateController().Complete(new MovementRequest(), null, CancellationToken.None);

        var result = Assert.IsAssignableFrom<ObjectResult>(response.Result);
        var problem = Assert.IsType<ValidationProblemDetails>(result.Value);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
        Assert.Contains("items", problem.Errors.Keys);
    }

    private SalesOrdersController CreateController()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddControllers();

        return new SalesOrdersController(_service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() }
            }
        };
    }

    private static MovementRequest ValidRequest() => new()
    {
        Notify = true,
        Recipients = ["cliente@exemplo.com"],
        Items = [TestMessages.Item()]
    };
}
