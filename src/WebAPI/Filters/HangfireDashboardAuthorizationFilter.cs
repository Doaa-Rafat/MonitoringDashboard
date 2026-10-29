using Domain.Constants;
using Hangfire.Dashboard;

namespace WebAPI.Filters;

/// <param name="allowAnonymous">Demo switch (Hangfire:AllowAnonymousDashboard). Never enable it on a public deployment.</param>
public class HangfireDashboardAuthorizationFilter(bool allowAnonymous) : IDashboardAuthorizationFilter
{
    public static bool IsAnonymousAccessEnabled(bool isDevelopment, bool explicitlyEnabled) =>
        isDevelopment && explicitlyEnabled;

    public bool Authorize(DashboardContext context)
    {
        if (allowAnonymous) return true;
        var user = context.GetHttpContext().User;
        return user.Identity?.IsAuthenticated == true && user.IsInRole(RoleNames.Admin);
    }
}
