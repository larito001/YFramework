using NoSLoofah.BuffSystem;

public sealed class GameRuntimeConfig
{
    public bool IsTest { get; }
    public BuffCollection BuffCollection { get; }
    public BuffTagData BuffData { get; }

    public GameRuntimeConfig(bool isTest, BuffCollection buffCollection, BuffTagData buffData)
    {
        IsTest = isTest;
        BuffCollection = buffCollection;
        BuffData = buffData;
    }
}
