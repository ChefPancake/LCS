# LCS - Lag Compensation System

## Overview

This is a rollback netcode implementation that I built over the weekend for an application to Innersloth (Hi Innersloth!). It uses both event-sourced game state and server-sent synchronizations points and is written in C#.

## How it works

### The Basics

The meat of this is the `StateChain` ([src](./LCS/StateChain.cs)). It maintains a collection of generic structs where the first struct in that collection contains absolute game state. On application startup this will be the initial state of the game, afterwards it will be the latest synchronized game state. All structs after the first are differentials that contain changes in the game state sourced from player events, either local or received as a network packet from the server. Game state at a particular point in time can be determined based on those diffs, given an update function defined in an implementation of `IStateUpdate<T>` ([src](./LCS/IStateUpdate.cs)). 

The server and all players would each have their own `StateChain` to maintain their own state and what they think the state of other players is. When a game client would send a packet for an event to the server, they'd first add that data to their own chain. The server would then receive that event, validate it based on game rules and its known state, add the event data to its chain, and then pass it along to the other clients. Clients' implementations of `IStateUpdate<T>` would be dumbed down but the server would contain the full logic needed to enforce game rules.

Each event that is passed is decorated with a timestamp that indicates when exactly in time the event occurred. When events are added to each other remote `StateChain`, the events are arranged in order based on this timestamp. This rearranging of events in time is what allows rollback to happen.

### Edge Cases

If we had infinite processing power and RAM, this `StateChain` would be perfectly capable of maintaining synchronized state. Unfortunately, the real world exists, so we need to keep the `StateChain` at a reasonable size. Periodically, the server aggregates its `StateChain` into a new singular, absolute state, placing it at the root of the chain. When it does this, it will send that absolute state to each client for them to accept that state as the root of their chain. This truncation and collapsing of events produces edge cases that require manual handling.

Tests are [here](./LCS.Tests/GameStateTests.cs), some of which replicate these issues.

#### Case 1
1. time=5 - Client, with 3s latency, sends an event to the server
2. time=6 - Server performs a sync
3. time=8 - Server receives the event from time=5
4. time=9 - Client receives the sync, which erases the effects from the event sent at time=5

This was remedied by attaching an incrementing id with each event that occurs. When the sync happens, the server also shares what was the last event id that it saw. When a client sees that the server has not yet seen an event that it sent (due to latency), it maintains that event and adds its effects to the incoming sync state, producing a new absolute state at its `StateChain` root.

#### Case 2
1. time=5 - Client A, with 3s latency, sends an event to the server
2. time=8 - Server receives event from time=5. Sends to Client B
3. time=9 - Server performs sync
4. time=10 - Client B, with unstable latency, receives sync which includes effects from event at time=5. Causes ClientB to correct.
5. time=11 - Client B receives event from time=5, duplicating its effect

This was remedied by using the same incrementing event id. If the server said its last seen event Id is later than what the client thinks it should be, it accepts the sync state and will ignore all events that come in with ids lower than the last known event id from that sync.

#### Case 3
1. time=5 - Client A, with 3s latency, sends an event to the server
2. time=6 - Server performs sync
3. time=7 - Server receives event from time=5. Sends to Client B
4. time=10 - Client B, with 4s latency, receives sync from Server
5. time=11 - Client B, receives event from time=5, which is before sync from time=6 which is currently the root of the chain.

This was also remedied using eventIds. If a client receives a sync whose event ids line up, but then receives an event that occured before the sync started, it produces a new root state that includes the effect of the late event.
