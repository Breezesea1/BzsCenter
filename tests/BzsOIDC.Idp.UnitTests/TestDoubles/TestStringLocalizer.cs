using System.Globalization;
using Microsoft.Extensions.Localization;

namespace BzsOIDC.Idp.UnitTests.TestDoubles;

internal sealed class TestStringLocalizer<T> : IStringLocalizer<T>
{
    public LocalizedString this[string name] => new(name, name, resourceNotFound: false);

    public LocalizedString this[string name, params object[] arguments] => new(
        name,
        string.Format(name, arguments),
        resourceNotFound: false);

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
    {
        return [];
    }

    public IStringLocalizer WithCulture(CultureInfo culture)
    {
        return this;
    }
}
