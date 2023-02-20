namespace LCS;

public struct PlayerState {
    public float XPos;
    public float YPos;
    public float XVel;
    public float YVel;

    public override readonly string ToString() =>
        $"pos: [{XPos}, {YPos}]; vel: [{XVel}, {YVel}]";
}
