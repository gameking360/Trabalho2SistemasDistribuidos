using Commerce.Infrastructure.Configuration;
using Commerce.NotificationWorker;

DotEnvFile.Load();

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddNotificationWorker(builder.Configuration);

builder.Build().Run();
