namespace BzsCenter.Idp.Components.Common;

public sealed record NeoSelectOption<TValue>(TValue Value, string Label, string? Description = null)
    where TValue : notnull;
