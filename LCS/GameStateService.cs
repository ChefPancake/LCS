namespace LCS;

public class GameStateService {
    private const int CHANNEL_SERVER = 0;
    private const int CHANNEL_PLAYER_1 = 1;
    private const int CHANNEL_PLAYER_2 = 2;
    private static int[] CHANNELS = new[] {
        CHANNEL_SERVER, 
        CHANNEL_PLAYER_1, 
        CHANNEL_PLAYER_2
    };

    private readonly StateChain<GameState> _stateChain;
    
    private readonly Dictionary<int, long> _lastSeenEventIds;

    private readonly int _ownerChannelId;

    public GameStateService(StateChain<GameState> chain, int ownerChannelId) {
        _stateChain = chain;
        _ownerChannelId = ownerChannelId;
        _lastSeenEventIds = CHANNELS.ToDictionary(x => x, x => -1L);
    }

    public void HandlePlayerUpdateVelEvent(in PlayerUpdateVelEvent updateEvent) {
        if (_lastSeenEventIds.TryGetValue(updateEvent.Id.ChannelId, out long lastId)) {
            if (updateEvent.Id.Id <= lastId) {
                return;
            }
            _lastSeenEventIds[updateEvent.Id.ChannelId] = updateEvent.Id.Id;
        } else {
            throw new Exception($"Channel with id {updateEvent.Id.ChannelId} not found");
        }
        var newState = updateEvent.Id.ChannelId switch {
            CHANNEL_PLAYER_1 => new GameState() { 
                Player1 = new PlayerState() {
                    XVel = updateEvent.XDiff,
                    YVel = updateEvent.YDiff
                }
            },
            CHANNEL_PLAYER_2 => new GameState() { 
                Player2 = new PlayerState() {
                    XVel = updateEvent.XDiff,
                    YVel = updateEvent.YDiff
                }
            },
            CHANNEL_SERVER => throw new InvalidOperationException("Received invalid event from server"),
            _ => throw new InvalidOperationException("Unknown channel with id")
        };
        _stateChain.Add(newState, updateEvent.Timestamp, updateEvent.Id);
    }

    public void HandleServerSyncEvent(in ServerSyncEvent syncEvent) {
        _stateChain.Reset(
            syncEvent.CurrentState, 
            syncEvent.TimeStamp, 
            syncEvent.Id,
            new EventId() { Id = syncEvent.LastSeenEventIds[_ownerChannelId] + 1, ChannelId = _ownerChannelId });
        foreach (var channel in CHANNELS) {
            _lastSeenEventIds[channel] = Math.Max(
                _lastSeenEventIds[channel], 
                syncEvent.LastSeenEventIds[channel]);
        }
    }

    public GameState GetStateAt(float timestamp) => 
        _stateChain.CurrentStateAt(timestamp);
}
