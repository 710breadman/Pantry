using Pantry.Detection;
using Pantry.Domain;

namespace Pantry.Tests;

public sealed class WingetListParserTests
{
    [Fact]
    public void Installed_package_without_available_column_is_current()
    {
        var output = """
Name   Id          Version  Source
----------------------------------
7-Zip  7zip.7zip  24.09    winget
""";

        var result = WingetListParser.Parse("7zip", "7zip.7zip", output);

        Assert.Equal(DetectedAppState.InstalledCurrent, result.State);
        Assert.Equal(DetectionConfidence.High, result.Confidence);
        Assert.Equal("24.09", result.InstalledVersion);
        Assert.Null(result.AvailableVersion);
    }

    [Fact]
    public void Installed_package_with_available_column_is_update_available()
    {
        var output = """
Name   Id          Version  Available  Source
---------------------------------------------
7-Zip  7zip.7zip  24.09    25.00      winget
""";

        var result = WingetListParser.Parse("7zip", "7zip.7zip", output);

        Assert.Equal(DetectedAppState.UpdateAvailable, result.State);
        Assert.Equal("24.09", result.InstalledVersion);
        Assert.Equal("25.00", result.AvailableVersion);
    }

    [Fact]
    public void Missing_package_returns_not_installed()
    {
        var output = """
Name   Id             Version  Source
-------------------------------------
Other  Vendor.Other   1.0      winget
""";

        var result = WingetListParser.Parse("7zip", "7zip.7zip", output);

        Assert.Equal(DetectedAppState.NotInstalled, result.State);
        Assert.Equal(DetectionConfidence.Medium, result.Confidence);
    }
}

