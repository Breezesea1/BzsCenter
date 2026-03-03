namespace BzsCenter.Shared.Infrastructure;

public interface IMigrated
{
    Task MigrateAsync(CancellationToken cancellationToken);
}