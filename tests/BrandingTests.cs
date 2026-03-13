// Using xUnit and Moq
using Xunit;
using Moq;

public class BrandingTests
{
    [Fact]
    public void UpdateDashboardBranding_ValidColorChange_UpdatesSuccessfully()
    {
        // Arrange
        var dashboard = new Dashboard();
        var expectedColor = "#0078D4";

        // Act
        dashboard.UpdateColor(expectedColor);

        // Assert
        Assert.Equal(expectedColor, dashboard.PrimaryColor);
    }

    [Fact]
    public void UpdateDashboardBranding_ValidLogoChange_UpdatesSuccessfully()
    {
        // Arrange
        var dashboard = new Dashboard();
        var expectedLogo = "logo-option-chunky-infinity-box.svg";

        // Act
        dashboard.UpdateLogo(expectedLogo);

        // Assert
        Assert.Equal(expectedLogo, dashboard.Logo);
    }

    [Fact]
    public void UpdateDashboardBranding_LogoTransparentBackground_DisplaysCorrectly()
    {
        // Arrange
        var dashboard = new Dashboard();

        // Act
        dashboard.UpdateLogo("logo-option-chunky-infinity-box.svg");

        // Assert
        Assert.True(dashboard.IsLogoTransparent());
    }

    // Additional tests for consistency, integration, and functionality...
}