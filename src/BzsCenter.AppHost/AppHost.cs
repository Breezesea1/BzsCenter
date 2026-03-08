var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume();

var idpDatabase = postgres.AddDatabase("DefaultConnection");
var redis = builder.AddRedis("redis");

builder.AddProject<Projects.BzsCenter_Idp>("idp")
    .WithEnvironment("CacheOptions__CacheType", "Redis")
    .WithReference(idpDatabase)
    .WithReference(redis)
    .WaitFor(idpDatabase)
    .WaitFor(redis);

builder.Build().Run();
