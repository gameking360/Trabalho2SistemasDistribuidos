using System.Text.Encodings.Web;
using System.Text.Unicode;
using Commerce.Api.Application;
using Commerce.Contracts.Movements;
using Commerce.Infrastructure.Configuration;
using Commerce.Infrastructure.DependencyInjection;

DotEnvFile.Load();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRabbitMqMessaging(builder.Configuration);
builder.Services.AddScoped<IStockMovementRequestService, StockMovementRequestService>();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
        // Mantém acentos legíveis nas respostas (ex.: "Saída") sem liberar caracteres sensíveis a HTML.
        options.JsonSerializerOptions.Encoder = JavaScriptEncoder.Create(UnicodeRanges.BasicLatin, UnicodeRanges.Latin1Supplement));

builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.IncludeXmlComments(typeof(Program).Assembly);
    options.IncludeXmlComments(typeof(MovementItem).Assembly);
});

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
}

app.MapControllers();

app.Run();
