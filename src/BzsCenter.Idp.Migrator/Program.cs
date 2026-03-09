using BzsCenter.AppHost.ServiceDefaults;
using BzsCenter.Idp.Infra;
using BzsCenter.Idp.Services;
using BzsCenter.Idp.Services.Identity;
using BzsCenter.Shared.Infrastructure.Database;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddIdpService(builder.Configuration);
builder.Services.AddMigration<IdpDbContext>(static (_, sp) => sp.GetRequiredService<IdentitySeeder>().SeedAsync());

using var host = builder.Build();
await using var scope = host.Services.CreateAsyncScope();

var migrator = scope.ServiceProvider.GetRequiredService<IMigrated>();
var lifetime = scope.ServiceProvider.GetRequiredService<IHostApplicationLifetime>();

await migrator.MigrateAsync(lifetime.ApplicationStopping);
