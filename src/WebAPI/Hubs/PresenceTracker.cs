namespace WebAPI.Hubs;

public interface IPresenceTracker
{
    /// <returns>true if this is the user's first open connection.</returns>
    bool UserConnected(int userId, string userName, string connectionId);
    /// <returns>true if that was the user's last open connection.</returns>
    bool UserDisconnected(int userId, string connectionId);
    IReadOnlyList<OnlineUserDto> GetOnlineUsers();
}

public class PresenceTracker : IPresenceTracker
{
    private readonly Dictionary<int, (string UserName, HashSet<string> Connections)> _users = new();
    private readonly object _lock = new();

    public bool UserConnected(int userId, string userName, string connectionId)
    {
        lock (_lock)
        {
            if (!_users.TryGetValue(userId, out var entry))
            {
                _users[userId] = (userName, new HashSet<string> { connectionId });
                return true;
            }
            entry.Connections.Add(connectionId);
            return false;
        }
    }

    public bool UserDisconnected(int userId, string connectionId)
    {
        lock (_lock)
        {
            if (!_users.TryGetValue(userId, out var entry)) return false;
            entry.Connections.Remove(connectionId);
            if (entry.Connections.Count > 0) return false;
            _users.Remove(userId);
            return true;
        }
    }

    public IReadOnlyList<OnlineUserDto> GetOnlineUsers()
    {
        lock (_lock)
        {
            return _users
                .Select(kv => new OnlineUserDto(kv.Key, kv.Value.UserName, kv.Value.Connections.Count))
                .OrderBy(u => u.UserName)
                .ToList();
        }
    }
}