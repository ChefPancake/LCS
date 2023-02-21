namespace LCS;

public interface IStateUpdate<T> where T: struct{
    void UpdateState(ref T state, in T diff, float delTime);
}
