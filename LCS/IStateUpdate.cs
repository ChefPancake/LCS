namespace LCS;

public interface IStateUpdate<T> where T: struct{
    void UpdateState(ref T player, in T diff, float delTime);
}
