/// <summary>
/// 业务侧 Unity Scene 资产名常量，配合 SceneManager.LoadScene /
/// NetworkManager.ServerChangeScene 使用。新增场景时在此加一个常量。
/// 与 Assets/Scenes/ 下的 .unity 资产名一一对应。
/// </summary>
public static class YSceneNames
{
    public const string Start = "GameStart";
    public const string Main = "GameMain";
}
