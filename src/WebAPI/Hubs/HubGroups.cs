namespace WebAPI.Hubs;

public static class HubGroups
{
    public const string Dashboard = "dashboard";
    public const string Alerts = "alerts";
    public static string Server(int serverId) => $"server-{serverId}";
    public const string Reports = "reports";
}