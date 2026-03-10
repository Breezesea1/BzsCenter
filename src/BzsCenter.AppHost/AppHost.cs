var builder = DistributedApplication.CreateBuilder(args);

var environmentName = builder.Environment.EnvironmentName;
var adminUserName = builder.Configuration["Identity:Admin:UserName"];
var adminPassword = builder.Configuration["Identity:Admin:Password"];

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume();

var idpDatabase = postgres.AddDatabase("DefaultConnection");
var redis = builder.AddRedis("redis");
var idpMigrator = builder.AddProject<Projects.BzsCenter_Idp_Migrator>("idp-migrator")
    .WithEnvironment("DOTNET_ENVIRONMENT", environmentName)
    .WithEnvironment("Identity__Admin__UserName", adminUserName)
    .WithEnvironment("Identity__Admin__Password", adminPassword)
    .WithReference(idpDatabase)
    .WaitFor(idpDatabase);

var idp = builder.AddProject<Projects.BzsCenter_Idp>("idp")
    .WithEnvironment("DOTNET_ENVIRONMENT", environmentName)
    .WithEnvironment("Identity__Admin__UserName", adminUserName)
    .WithEnvironment("Identity__Admin__Password", adminPassword)
    .WithEnvironment("CacheOptions__CacheType", "Redis")
    .WithReference(idpDatabase)
    .WithReference(redis)
    .WaitFor(idpDatabase)
    .WaitFor(redis)
    .WaitForCompletion(idpMigrator);



builder.Build().Run();
