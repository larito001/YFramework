using NoSLoofah.BuffSystem;

/// <summary>
/// Base implementation for buff-tag rule evaluators.
/// </summary>
public abstract class BuffTagManager : IBuffTagManager
{
    public abstract void Init(BuffTagData data);
    public abstract bool IsTagRemoveOther(BuffTag tag, BuffTag other);
    public abstract bool IsTagCanAddWhenHaveOther(BuffTag tag, BuffTag other);
}
