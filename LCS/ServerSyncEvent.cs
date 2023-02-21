namespace LCS;

public struct ServerSyncEvent {
    public EventId Id;
    public float TimeStamp;
    public GameState CurrentState;
    //index = channel, value = id
    public long[] LastSeenEventIds;
}