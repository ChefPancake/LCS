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
    private readonly IStateUpdate<GameState> _updater;
    


    private readonly Dictionary<int, long> _lastSeenEventIds;
    private readonly HashSet<EventId> _missingEventIds;

    private readonly int _ownerChannelId;
    private long _lastOwnedEventId;

    public GameStateService(StateChain<GameState> chain, IStateUpdate<GameState> updater, int ownerChannelId) {
        _stateChain = chain;
        _updater = updater;
        _ownerChannelId = ownerChannelId;
        _missingEventIds = new();
        _lastSeenEventIds = CHANNELS.ToDictionary(x=> x, x => -1L);
    }

    public void HandlePlayerUpdateVelEvent(in PlayerUpdateVelEvent updateEvent) {
        if (updateEvent.Id.ChannelId == _ownerChannelId) {
            _lastOwnedEventId = updateEvent.Id.Id;
        }
        if (_lastSeenEventIds.TryGetValue(updateEvent.Id.ChannelId, out long lastId)) {
            for (long id = lastId + 1; id < updateEvent.Id.Id; id++) {
                _missingEventIds.Add(new EventId() {
                    ChannelId = updateEvent.Id.ChannelId,
                    Id = id,
                });
            }
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
            new EventId() { Id = syncEvent.LastSeenEventIds[_ownerChannelId] + 1, ChannelId = _ownerChannelId },
            _updater);
    }

    public GameState GetStateAt(float timestamp) => 
        _stateChain.CurrentStateAt(timestamp, _updater);
}
