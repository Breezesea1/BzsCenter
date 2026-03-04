namespace BzsCenter.Shared.Infrastructure.Database;

public interface IMigrated
{
    Task MigrateAsync(CancellationToken cancellationToken);
}