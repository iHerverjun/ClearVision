using Acme.Product.Core.Entities;
using FluentAssertions;

namespace Acme.Product.Tests.Configuration;

public class AppConfigThemeNormalizationTests
{
    [Theory]
    [InlineData("dark", GeneralConfig.ThemeDark)]
    [InlineData(" DARK ", GeneralConfig.ThemeDark)]
    [InlineData("light", GeneralConfig.ThemeLight)]
    [InlineData("LiGhT", GeneralConfig.ThemeLight)]
    [InlineData("", GeneralConfig.ThemeDark)]
    [InlineData("sepia", GeneralConfig.ThemeDark)]
    public void NormalizeTheme_ShouldClampToSupportedValues(string rawTheme, string expectedTheme)
    {
        var config = new AppConfig
        {
            General = new GeneralConfig
            {
                Theme = rawTheme
            }
        };

        config.Normalize();

        config.General.Theme.Should().Be(expectedTheme);
    }
}
