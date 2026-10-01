using Commerce.Infrastructure.Configuration;
using Commerce.RetryWorker;

DotEnvFile.Load();

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddRetryWorker(builder.Configuration);

builder.Build().Run();
