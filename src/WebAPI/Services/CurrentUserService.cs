using System.Security.Claims;
using Application.Common.Interfaces;

namespace WebAPI.Services;

public class CurrentUserService(IHttpContextAccessor accessor) : ICurrentUserService
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public int? UserId =>
        int.TryParse(Principal?.FindFirstValue("sub"), out var id) ? id : null;

    public string? UserName => Principal?.FindFirstValue("unique_name");
    public string? Email => Principal?.FindFirstValue("email");
}