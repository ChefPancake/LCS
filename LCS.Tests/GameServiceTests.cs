namespace LCS.Tests;

[TestClass]
public class GameServiceTests {
    //because we want to test that the timestamps are indeed in order,
    //adding this type to capture both that and the other thing we're testing
    public struct TestGameState {
        public float Timestamp;
        public GameState GameState;
    }

    public class TestGameStateUpdater : IStateUpdate<TestGameState> {
        private IStateUpdate<GameState> _inner;

        public TestGameStateUpdater(IStateUpdate<GameState> inner) =>
            _inner = inner;

        public void UpdateState(ref TestGameState player, in TestGameState diff, float delTime) =>
            _inner.UpdateState(ref player.GameState, in diff.GameState, delTime);
    }


    [TestMethod]
    public void LaterEventIsAtEndOfChain() {
        var initialState = new TestGameState() { Timestamp = 1f }; 
        var updater = new TestGameStateUpdater(new GameStateUpdater());
        var chain = new StateChain<TestGameState>(initialState, initialState.Timestamp, updater);
        var nextState = new TestGameState() { Timestamp = 2f};
        chain.Add(nextState, nextState.Timestamp, new EventId() { Id = 1 });
        Assert.AreEqual(2, chain.EnumerateStates().Count());
        AssertChainIsInOrder(chain);
    }

    [TestMethod]
    public void EarlierEventIsInsertedWithinChain() {
        var initialState = new TestGameState() {
            Timestamp = 1.0f
        };
        var newStates = new[] {
            new TestGameState() { Timestamp = 3.0f },
            new TestGameState() { Timestamp = 4.0f },
            new TestGameState() { Timestamp = 2.0f },
        };
        var updater = new TestGameStateUpdater(new GameStateUpdater());
        var chain = new StateChain<TestGameState>(initialState, initialState.Timestamp, updater);
        foreach ((var state, var index) in newStates.Select((x, i) => (x, i))) {
            chain.Add(state, state.Timestamp, new EventId() { Id = index + 1 } );
        }

        Assert.AreEqual(4, chain.EnumerateStates().Count());
        AssertChainIsInOrder(chain);
    }

    [TestMethod]
    public void AddOneThousandRandomEvents() {
        var initialState = new TestGameState() {
            Timestamp = 1.0f
        };
        var rand = new Random();
        var newStates = Enumerable.Range(0, 999).Select(x => new TestGameState() { Timestamp = rand.Next(2, int.MaxValue) });
        var updater = new TestGameStateUpdater(new GameStateUpdater());
        var chain = new StateChain<TestGameState>(initialState, initialState.Timestamp, updater);
        foreach ((var state, var index) in newStates.Select((x, i) => (x, i))) {
            chain.Add(state, state.Timestamp, new EventId() { Id = index + 1 });
        }
        Assert.AreEqual(1000, chain.EnumerateStates().Count());
        AssertChainIsInOrder(chain);
    }

    [TestMethod]
    public void CalculateGameState_RootItemOnly() {
        var initialState = new TestGameState() {
            Timestamp = 1.0f,
            GameState = new GameState() {
                Player1 = new PlayerState() {
                    XVel = 1.0f,
                },
                Player2 = new PlayerState() {
                    YVel = 2.0f
                }
            }
        };
        var updater = new TestGameStateUpdater(new GameStateUpdater());
        var chain = new StateChain<TestGameState>(initialState, initialState.Timestamp, updater);
        var state = chain.CurrentStateAt(3f);
        AssertFloatsAreCloseEnough(2f, in state.GameState.Player1.XPos, state.GameState.Player1.ToString());
        AssertFloatsAreCloseEnough(0f, in state.GameState.Player1.YPos, state.GameState.Player1.ToString());
        AssertFloatsAreCloseEnough(0f, in state.GameState.Player2.XPos, state.GameState.Player2.ToString());
        AssertFloatsAreCloseEnough(4f, in state.GameState.Player2.YPos, state.GameState.Player2.ToString());
    }

    [TestMethod]
    public void CalculateGameState_WithDiffs() {
        var initialState = new TestGameState() {
            Timestamp = 1.0f,
            GameState = new GameState() {
                Player1 = new PlayerState() {
                    XVel = 1.0f,
                },
                Player2 = new PlayerState() {
                    YVel = 2.0f
                }
            }
        };
        var newStates = new[] {
            //values arbitrary besides being easy enough to add
            new TestGameState() { Timestamp = 3.0f, GameState = new() { Player1 = new() { XVel = 2f } } },
            new TestGameState() { Timestamp = 4.0f, GameState = new() { Player1 = new() { XVel = -3f } } },
            new TestGameState() { Timestamp = 2.0f, GameState = new() { Player2 = new() { YVel = 8f } } },
        };
        var updater = new TestGameStateUpdater(new GameStateUpdater());
        var chain = new StateChain<TestGameState>(initialState, initialState.Timestamp, updater);
        foreach ((var newState, var index) in newStates.Select((x, i) => (x, i))) {
            chain.Add(newState, newState.Timestamp, new EventId() { Id = index + 1 });
        }
        var state = chain.CurrentStateAt(5f);
        AssertFloatsAreCloseEnough(5f, in state.GameState.Player1.XPos, state.GameState.Player1.ToString());
        AssertFloatsAreCloseEnough(0f, in state.GameState.Player1.YPos, state.GameState.Player1.ToString());
        AssertFloatsAreCloseEnough(0f, in state.GameState.Player2.XPos, state.GameState.Player2.ToString());
        AssertFloatsAreCloseEnough(32f, in state.GameState.Player2.YPos, state.GameState.Player2.ToString());
    }

    [TestMethod]
    public void ResetFullChain_OnlyOneItemLeft() {
        var initialState = new TestGameState() {
            Timestamp = 1.0f,
        };
        var newStates = new[] {
            //values arbitrary besides being easy enough to add
            new TestGameState() { Timestamp = 3.0f },
            new TestGameState() { Timestamp = 4.0f },
            new TestGameState() { Timestamp = 2.0f },
        };
        var updater = new TestGameStateUpdater(new GameStateUpdater());
        var chain = new StateChain<TestGameState>(initialState, initialState.Timestamp, updater);
        foreach ((var newState, var index) in newStates.Select((x, i) => (x, i))) {
            chain.Add(newState, newState.Timestamp, new EventId() { Id = index + 1 });
        }
        var state = chain.CurrentStateAt(5f);
        chain.Reset(state, 5f, new EventId() { Id = 5 }, new EventId() { Id = 5 });
        Assert.AreEqual(1, chain.EnumerateStates().Count());
        AssertChainIsInOrder(chain);
    }

    [TestMethod]
    public void GameService_OwnedEventSentBeforeButReceivedAfterServerSendsSync() {
        var initialState = new GameState();
        var channelId = 1;
        var updater = new GameStateUpdater();
        var chain = new StateChain<GameState>(initialState, timestamp: 0f, updater);
        var service = new GameStateService(chain, channelId);
        //sent first, so that state can update for a baseline
        var firstPlayerUpdate = new PlayerUpdateVelEvent() {
            Timestamp = 1f,
            Id = new EventId() {
                ChannelId = channelId,
                Id = 1
            },
            XDiff = 1f,
            YDiff = 0f
        };
        //sent second. Will be sent before the server creates the sync to send,
        //but won't be received until after the server sends the sync. We expect
        //the owner of this service to recognize that and adjust.
        var secondPlayerUpdate = new PlayerUpdateVelEvent() {
            Timestamp = 3f,
            Id = new EventId() {
                ChannelId = channelId,
                Id = 2
            },
            XDiff = 1f,
            YDiff = 0f
        };
        //sent by the server back to the owner of this gameService
        var syncEvent = new ServerSyncEvent() {
            TimeStamp = 5f,
            Id = new EventId() {
                ChannelId = 0,
                Id = 1
            },
            LastSeenEventIds = new[] { 1L, 1L, 0L },
            CurrentState = new GameState() {
                Player1 = new PlayerState() {
                    XVel = 1,
                    XPos = 4
                }
            }
        };
        service.HandlePlayerUpdateVelEvent(firstPlayerUpdate);
        service.HandlePlayerUpdateVelEvent(secondPlayerUpdate);
        service.HandleServerSyncEvent(syncEvent);

        var updatedState = service.GetStateAt(7f);
        AssertFloatsAreCloseEnough(in updatedState.Player1.XVel, 2f, updatedState.Player1.ToString());
        AssertFloatsAreCloseEnough(in updatedState.Player1.XPos, 10f, updatedState.Player1.ToString());
        AssertFloatsAreCloseEnough(in updatedState.Player1.YVel, 0f, updatedState.Player1.ToString());
        AssertFloatsAreCloseEnough(in updatedState.Player1.YPos, 0f, updatedState.Player1.ToString());
    }

    [TestMethod]
    public void ServerExpectsLocalClientToHaveReceivedAnEventThatItDidnt_LateEventIsSkippedWhenReceived() {
        var initialState = new GameState();
        var firstPlayerChannelId = 1;
        var secondPlayerChannelId = 2;
        var updater = new GameStateUpdater();
        var chain = new StateChain<GameState>(initialState, timestamp: 0f, updater);
        var service = new GameStateService(chain, firstPlayerChannelId);

        //sent first, so that state can update for a baseline
        var firstPlayerUpdate = new PlayerUpdateVelEvent() {
            Timestamp = 1f,
            Id = new EventId() {
                ChannelId = firstPlayerChannelId,
                Id = 1
            },
            XDiff = 1f,
            YDiff = 0f
        };

        //sent second, but not received by this client until after the sync
        var secondPlayerUpdate = new PlayerUpdateVelEvent() {
            Timestamp = 3f,
            Id = new EventId() {
                ChannelId = secondPlayerChannelId,
                Id = 1
            },
            XDiff = 1f,
            YDiff = 0f
        };

        //sent by the server back to the owner of this gameService
        var syncEvent = new ServerSyncEvent() {
            TimeStamp = 5f,
            Id = new EventId() {
                ChannelId = 0,
                Id = 1
            },
            LastSeenEventIds = new[] { 1L, 1L, 1L },
            CurrentState = new GameState() {
                Player1 = new PlayerState() {
                    XVel = 1,
                    XPos = 4
                },
                Player2 = new PlayerState() {
                    XVel = 1,
                    XPos = 2
                }
            }
        };

        service.HandlePlayerUpdateVelEvent(firstPlayerUpdate);
        service.HandleServerSyncEvent(syncEvent);
        var stateBeforeLateEvent = service.GetStateAt(7f);
        service.HandlePlayerUpdateVelEvent(secondPlayerUpdate);
        var stateAfterLateEvent = service.GetStateAt(7f);
        Assert.AreEqual(stateBeforeLateEvent, stateAfterLateEvent);
        AssertFloatsAreCloseEnough(in stateAfterLateEvent.Player1.XVel, 1f, stateAfterLateEvent.Player1.ToString());
        AssertFloatsAreCloseEnough(in stateAfterLateEvent.Player1.XPos, 6f, stateAfterLateEvent.Player1.ToString());
        AssertFloatsAreCloseEnough(in stateAfterLateEvent.Player2.XVel, 1f, stateAfterLateEvent.Player1.ToString());
        AssertFloatsAreCloseEnough(in stateAfterLateEvent.Player2.XPos, 4f, stateAfterLateEvent.Player1.ToString());
    }

    [TestMethod]
    public void ClientReceivesEventThatOccurredBeforeLastSync() {
        var initialState = new GameState();
        var firstPlayerChannelId = 1;
        var secondPlayerChannelId = 2;
        var updater = new GameStateUpdater();
        var chain = new StateChain<GameState>(initialState, timestamp: 0f, updater);
        var service = new GameStateService(chain, firstPlayerChannelId);

        //sent first, so that state can update for a baseline
        var firstPlayerUpdate = new PlayerUpdateVelEvent() {
            Timestamp = 1f,
            Id = new EventId() {
                ChannelId = firstPlayerChannelId,
                Id = 1
            },
            XDiff = 1f,
            YDiff = 0f
        };

        //setn second
        var secondPlayerUpdate = new PlayerUpdateVelEvent() {
            Timestamp = 3f,
            Id = new EventId() {
                ChannelId = secondPlayerChannelId,
                Id = 1
            },
            XDiff = 1f,
            YDiff = 0f
        };

        //sent third, but not received by this client until after the sync
        var thirdPlayerUpdate = new PlayerUpdateVelEvent() {
            Timestamp = 4f,
            Id = new EventId() {
                ChannelId = secondPlayerChannelId,
                Id = 2
            },
            XDiff = 2f,
            YDiff = 0f
        };

        //sent by the server back to the owner of this gameService
        var syncEvent = new ServerSyncEvent() {
            TimeStamp = 5f,
            Id = new EventId() {
                ChannelId = 0,
                Id = 1
            },
            LastSeenEventIds = new[] { 1L, 1L, 1L },
            CurrentState = new GameState() {
                Player1 = new PlayerState() {
                    XVel = 1,
                    XPos = 4
                },
                Player2 = new PlayerState() {
                    XVel = 1,
                    XPos = 2
                }
            }
        };

        service.HandlePlayerUpdateVelEvent(firstPlayerUpdate);
        service.HandlePlayerUpdateVelEvent(secondPlayerUpdate);
        service.HandleServerSyncEvent(syncEvent);
        var stateBeforeLateEvent = service.GetStateAt(7f);
        AssertFloatsAreCloseEnough(in stateBeforeLateEvent.Player1.XVel, 1f, stateBeforeLateEvent.Player1.ToString());
        AssertFloatsAreCloseEnough(in stateBeforeLateEvent.Player1.XPos, 6f, stateBeforeLateEvent.Player1.ToString());
        AssertFloatsAreCloseEnough(in stateBeforeLateEvent.Player2.XVel, 1f, stateBeforeLateEvent.Player1.ToString());
        AssertFloatsAreCloseEnough(in stateBeforeLateEvent.Player2.XPos, 4f, stateBeforeLateEvent.Player1.ToString());
        service.HandlePlayerUpdateVelEvent(thirdPlayerUpdate);
        var stateAfterLateEvent = service.GetStateAt(7f);
        AssertFloatsAreCloseEnough(in stateAfterLateEvent.Player1.XVel, 1f, stateAfterLateEvent.Player1.ToString());
        AssertFloatsAreCloseEnough(in stateAfterLateEvent.Player1.XPos, 6f, stateAfterLateEvent.Player1.ToString());
        AssertFloatsAreCloseEnough(in stateAfterLateEvent.Player2.XVel, 3f, stateAfterLateEvent.Player1.ToString());
        AssertFloatsAreCloseEnough(in stateAfterLateEvent.Player2.XPos, 10f, stateAfterLateEvent.Player1.ToString());
    }

    private static void AssertFloatsAreCloseEnough(in float expected, in float actual, string? message = null) {
        Assert.IsTrue(Math.Abs(actual - expected) < float.Epsilon, message);
    }

    private static void AssertChainIsInOrder(StateChain<TestGameState> chain) {
        try { 
            float lastTimestamp = -1f;
            foreach (var state in chain.EnumerateStates()) {
                Assert.IsTrue(state.Timestamp >= lastTimestamp, $"events are out of order. Failed at timestamp: {state.Timestamp}");
                lastTimestamp = state.Timestamp;
            }
        } catch (AssertFailedException) {
            Console.WriteLine(string.Join(Environment.NewLine, chain.EnumerateStates().Select(x => $"Timestamp: {x.Timestamp}")));
            throw;
        }
    }
}