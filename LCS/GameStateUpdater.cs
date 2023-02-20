namespace LCS;

public class GameStateUpdater : IStateUpdate<GameState> {
    public void UpdateState(ref GameState state, in GameState diff, float delTime) {
        UpdatePlayerState(ref state.Player1, in diff.Player1, delTime);
        UpdatePlayerState(ref state.Player2, in diff.Player2, delTime);
    }

    private void UpdatePlayerState(ref PlayerState player, in PlayerState diff, float delTime) {
        player.XPos += diff.XPos + player.XVel * delTime;
        player.YPos += diff.YPos + player.YVel * delTime;
        player.XVel += diff.XVel;
        player.YVel += diff.YVel;
    }
}