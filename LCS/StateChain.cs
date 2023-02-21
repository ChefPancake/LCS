namespace LCS;

public class StateChain<T> where T: struct {
    private struct ChainLink {
        public EventId Id;
        public float Timestamp;
        public int? NextItemIndex;
        public T Item;

        public readonly ChainLink WithNextIndex(int? newIndex) {
            return this with { NextItemIndex = newIndex };
        }
    }
    
    //The root is always an absolute value, and the chain will always
    //have at least 1 value or it will complain. All subsequent values
    //will be diffs based on the root

    private const int STATE_CHAIN_MAX_LENGTH = 1024;

    private readonly ChainLink[] _chain;
    private int _chainRootIndex;
    private int _nextLinkIndex;

    public StateChain(T initialState, float timestamp) {
        _chain = new ChainLink[STATE_CHAIN_MAX_LENGTH];
        _chain[0] = new() { 
            Id = new EventId(), 
            Timestamp = timestamp, 
            Item = initialState
        };
        _chainRootIndex = 0;
        _nextLinkIndex = 1;
    }

    public void Add(T newState, float timestamp, EventId eventId) {
        var link = new ChainLink() {
            Id = eventId,
            Timestamp = timestamp,
            Item = newState
        };
        _nextLinkIndex = GetSurroundingLinksForTimestamp(link.Timestamp) switch {
            (null, null) => throw new InvalidDataException("Event chain was invalid"),
            (int prev, null) => AddToEnd(prev, link),
            (null, int next) => throw new NotImplementedException("TODO: not sure how to handle this yet"),
            (int prev, int next) => Insert(prev, next, link)
        };
    }

    public void Reset(T newRoot, float timestamp, EventId eventId, EventId includeEventsFrom, IStateUpdate<T> updater) {
        foreach (var link in EnumerateLinks()
                .Where(x => x.Id.ChannelId == includeEventsFrom.ChannelId 
                    && x.Id.Id >= includeEventsFrom.Id)) {
            updater.UpdateState(ref newRoot, in link.Item, timestamp - link.Timestamp);
        }
        _chain[_chainRootIndex] = new ChainLink() {
            Id = eventId,
            Timestamp = timestamp,
            Item = newRoot
        };
        _nextLinkIndex = 1;
    }

    private IEnumerable<ChainLink> EnumerateLinks() {
        int? index = _chainRootIndex;
        while (index is int i) {
            yield return _chain[i];
            index = _chain[i].NextItemIndex;
        }
    }

    public IEnumerable<T> EnumerateStates() =>
        EnumerateLinks().Select(x => x.Item);

    public T CurrentStateAt(float timestamp, IStateUpdate<T> updater) {
        var root = _chain[_chainRootIndex];
        if (timestamp < root.Timestamp) {
            throw new InvalidOperationException("Cannot retrieve state from the past");
        }
        var currentState = root.Item;
        var lastTimestamp = root.Timestamp;
        foreach (var state in EnumerateLinks().Skip(1).TakeWhile(x => x.Timestamp <= timestamp)) {
            var delTime = state.Timestamp - lastTimestamp;
            updater.UpdateState(ref currentState, in state.Item, delTime);
            lastTimestamp = state.Timestamp;
        }
        updater.UpdateState(ref currentState, default, timestamp - lastTimestamp);
        return currentState;
    }

    private int AddToEnd(int prevEndIndex, ChainLink newState) {
        _chain[_nextLinkIndex] = newState;
        _chain[prevEndIndex] = _chain[prevEndIndex].WithNextIndex(_nextLinkIndex);
        return _nextLinkIndex + 1;
    }

    private int Insert(int prevIndex, int nextIndex, ChainLink newState) {
        _chain[_nextLinkIndex] = newState.WithNextIndex(nextIndex);
        _chain[prevIndex] = _chain[prevIndex].WithNextIndex(_nextLinkIndex);
        return _nextLinkIndex + 1;
    }

    private (int?, int?) GetSurroundingLinksForTimestamp(float timestamp) {
        int? index = _chainRootIndex;
        int? prevLinkIndex = null;
        while (index is int i) {
            var link = _chain[i];
            if (link.Timestamp > timestamp) {
                return (prevLinkIndex, index);
            }
            prevLinkIndex = index;
            index = link.NextItemIndex;
        }
        return (prevLinkIndex, index);
    }
}
