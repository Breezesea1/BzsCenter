namespace BzsCenter.Shared.Infrastructure.AspNetCore;

public class DataProtectionOptions
{
    public required string ApplicationName { get; init; }
    public required string StorageDirectory { get; init; }
    public double KeyLifetimeDays { get; init; } = 90;
}