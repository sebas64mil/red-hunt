using System.Collections.Generic;

public class PlayerRegistry
{
    private Dictionary<int, PlayerSession> players = new();
    private Queue<int> availableIds = new();

    private int nextId = 1;

    public PlayerSession AddPlayer(string playerType)
    {
        int id = GetNextId();

        var player = new PlayerSession(id, playerType);
        players[id] = player;

        return player;
    }

    public PlayerSession AddPlayerWithId(int id, string playerType)
    {
        if (players.ContainsKey(id))
            return players[id];

        var player = new PlayerSession(id, playerType);
        players[id] = player;

        var newQueue = new Queue<int>();
        while (availableIds.Count > 0)
        {
            var v = availableIds.Dequeue();
            if (v != id) newQueue.Enqueue(v);
        }
        availableIds = newQueue;

        if (id >= nextId)
            nextId = id + 1;

        return player;
    }

    public void RemovePlayer(int id)
    {
        if (!players.ContainsKey(id)) return;

        players.Remove(id);
        availableIds.Enqueue(id);
    }

    public PlayerSession GetPlayer(int id)
    {
        players.TryGetValue(id, out var player);
        return player;
    }

    public IEnumerable<PlayerSession> GetAllPlayers()
    {
        return players.Values;
    }

    private int GetNextId()
    {
        if (availableIds.Count > 0)
        {
            return availableIds.Dequeue();
        }

        return nextId++;
    }
}