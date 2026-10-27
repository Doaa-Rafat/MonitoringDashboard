using Domain.Constants;
using Hangfire.Dashboard;

namespace WebAPI.Filters;

public class HangfireDashboardAuthorizationFilter(IWebHostEnvironment env) : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        if (env.IsDevelopment()) return true;     // convenient locally; see README note
        var user = context.GetHttpContext().User;
        return user.Identity?.IsAuthenticated == true && user.IsInRole(RoleNames.Admin);
    }
}