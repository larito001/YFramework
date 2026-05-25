# Mirror 接入说明

> 框架已完成网络层基础设施（YNetworkManager、NetworkEntityBase）。本文档给出在 Unity Editor 里把它跑起来所需的手动配置步骤。

## 框架现状（已落地）

- `Framework/Network/YNetworkManager.cs` — 继承 `Mirror.NetworkManager`，把生命周期事件转发到 `EventMgr`
- `Framework/Network/YNetworkEvent.cs` — 网络事件枚举（ServerStarted、ClientStarted、ServerSceneChanged、…）
- `Framework/Network/NetworkEntityBase.cs` — 联网实体基类，提供 `Ctx` / `GetService<T>` 访问 GameContext
- `GamePlay/Network/NetworkPlayer.cs` — 联网玩家示例（带 SyncVar 和本地输入）

## 已退役的旧基础设施

| 旧组件 | 现在用什么替代 |
|---|---|
| `YSceneManager` / `YSceneBase` | Unity 原生 `SceneManager.LoadScene` 或 Mirror `NetworkManager.ServerChangeScene` |
| `ObjectBase` / `SceneModelBase` | 直接继承 `NetworkEntityBase` 或 `MonoBehaviour`；`IClickable/IHoverable/IDraggable` 由 `SceneInteractionService` 通过 `GetComponentInParent` 查找 |
| `ObjectPool`（GameObject 池） | Mirror 自带 spawn 池化；若需本地池化自行写简单 Stack |
| `ParticleEntity`（已删，无人使用） | 直接 `Instantiate` 粒子 prefab 或写 MonoBehaviour |
| `UIMgr.InjectSceneModels` | 业务实体直接 `Ctx.Get<UIMgr>()` |

> `DataObjPool` / `PoolItem` / `BaseEntity` 保留（A* 寻路在用）。

---

## Editor 配置步骤

### 1. 创建 Unity Scene 资产

`YSceneNames` 当前定义两个场景：

```csharp
public const string Start = "GameStart";  // 已存在: Assets/Scenes/Boot/GameStart.unity
public const string Main  = "GameMain";    // ⚠ 需手动创建
```

**操作**：在 Editor 里：
1. 复制 `Assets/Scenes/Boot/GameStart.unity` 改名为 `GameMain.unity`（保留 GameLoop / UISceneDontDelete 等基础物）
2. `File → Build Settings → Scenes In Build` 把这两个场景都加进去（GameStart 在 index 0）

### 2. 创建 NetworkManager GameObject

在 **GameStart.unity** 里：

1. 新建空 GameObject，命名 `NetworkManager`
2. Add Component → `YNetworkManager`（在 Network/ 下找）
3. Add Component → `KcpTransport`
4. YNetworkManager Inspector：
   - Transport 拖入刚加的 KcpTransport
   - Network Address: `localhost`
   - Online Scene: 拖入 `GameMain.unity`
   - Offline Scene: 拖入 `GameStart.unity`
   - Player Prefab: 留空，下一步建好再配
   - Auto Create Player: 勾上
   - Dont Destroy On Load: 勾上

### 3. 创建 Player prefab

1. 在 Hierarchy 里新建空 GameObject（或挂个 Capsule）
2. Add Component：
   - `NetworkIdentity`
   - `NetworkTransformReliable`（或 `NetworkTransformUnreliable`）
   - `NetworkPlayer`（脚本在 GamePlay/Network/）
   - 一个 `CapsuleCollider` 或类似
3. 拖到 `Assets/Resources/Prefabs/NetworkPlayer.prefab`（路径任意，关键是要 prefab）
4. 删除 Hierarchy 里那个对象
5. 回到 NetworkManager Inspector，把 prefab 拖到 `Player Prefab`

### 4. 验证

1. Play GameStart.unity
2. 在 Game 视图里你应该看到 `NetworkManagerHUD`（如果给 NetworkManager 加了这个组件）的 Start Host / Client / Server 按钮
   - 没加？再 Add Component → `NetworkManagerHUD`
3. 点击 Start Host → 应该自动切到 GameMain 场景 + spawn 一个 Player
4. WASD 应该能移动 Player

### 5. 联网两端测试

1. Build 一份 Standalone（File → Build And Run）
2. Editor 里 Start Host，Build 出来的 exe Start Client（默认 connect 到 localhost）
3. 两端各看到对方的 Player 在动 → 成功

---

## 框架订阅网络事件

业务侧任何服务可以：

```csharp
ctx.Get<EventMgr>().Add(YNetworkEvent.ClientStarted, () => {
    Debug.Log("client connected");
});
ctx.Get<EventMgr>().Add<string>(YNetworkEvent.ServerSceneChanged, sceneName => {
    Debug.Log($"server changed scene to {sceneName}");
});
```

**注意**：EventMgr 的事件签名一旦确定后续订阅必须一致。`ServerSceneChanged` / `ClientSceneChanged` 是 `Action<string>`，其它都是 `Action`。

## 查询当前网络模式

```csharp
var mode = YNetworkManager.singleton.Mode;  // YNetworkMode.Offline / Server / Client / Host
```

或直接用 Mirror 静态属性：`NetworkServer.active`、`NetworkClient.active`。
