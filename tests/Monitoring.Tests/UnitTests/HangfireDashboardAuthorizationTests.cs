using WebAPI.Filters;

namespace Monitoring.Tests.UnitTests;

public class HangfireDashboardAuthorizationTests
{
    [Theory]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public void IsAnonymousAccessEnabled_RequiresDevelopmentAndExplicitOptIn(
        bool isDevelopment,
        bool explicitlyEnabled,
        bool expected)
    {
        var result = HangfireDashboardAuthorizationFilter.IsAnonymousAccessEnabled(
            isDevelopment,
            explicitlyEnabled);

        Assert.Equal(expected, result);
    }
}
