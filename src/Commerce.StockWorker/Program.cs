using Commerce.Infrastructure.Configuration;
using Commerce.StockWorker;

DotEnvFile.Load();

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddStockWorker(builder.Configuration);

builder.Build().Run();
