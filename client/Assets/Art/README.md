# Art

美术资源根目录，**按专业域横向分类**。详见 `Docs/项目规范.md §1 / §3`。

| 子目录 | 内容 | 归属 |
|---|---|---|
| `Animations/` | AnimController / `.anim` 文件 | 美术（动画） |
| `Audio/BGM/` | 背景音乐 | 美术（音频） |
| `Audio/SFX/` | 音效 | 美术（音频） |
| `Characters/` | 角色：模型/贴图/材质/动画 按业务子目录归并 | 美术（角色） |
| `Environments/` | 场景美术（地形/建筑/植被） | 美术（场景） |
| `Fonts/` | 字体（中文/英文/SDF） | 美术（UI） |
| `Props/` | 道具 | 美术（角色/场景） |
| `Shaders/` | `.shader` / `.shadergraph` | 美术 + 程序 |
| `ThirdParty/` | 买来的整包美术资源（按"包名"目录保留作者结构，便于升级） | 程序（采购） |
| `UI/` | 自制 UI 美术；第三方 UI 包（如 HONETi）放 `UI/<包名>/` | 美术（UI） |
| `UI/Atlas/` | SpriteAtlas | 美术（UI） |
| `UI/Icons/` | 业务图标 | 美术（UI） |
| `UI/Panels/` | 自制 Panel 贴图 | 美术（UI） |
| `UI/HONETi/` | 第三方 HONETi UI 包（Canvas/Demo/Textures/<主题>） | 程序（采购） |
| `VFX/` | 特效贴图/材质/Prefab | 美术（特效） |

## 准则

- **不放 `Resources/` 路径下能直接 `Resources.Load` 的资源**（避免打包膨胀）；
- 角色/场景按"模块"建子目录，例如 `Characters/Hero01/{Model,Texture,Material,Anim}`；
- 大型源文件（PSD/AI/FBX/Blend）放 **Unity 工程外** 的 `YFramework/ArtSource/`（建议 Git LFS）；
- 第三方包整体保留作者目录结构（如 `HONETi/`），不打散，便于将来升级合并。
