# ArtSource

**Unity 工程外** 的美术源文件（PSD/AI/FBX/Blend/SPP 等），不放进 `client/Assets/`，避免：

- Unity 导入耗时变长、Library/ 膨胀；
- `.meta` 散落污染；
- 误打入构建产物。

## 子目录

| 子目录 | 内容 |
|---|---|
| `UI/` | UI 原稿 PSD/AI（按面板命名） |
| 后续按需新增 `Characters/`、`Environments/`、`VFX/` 等 | 同名对应 `client/Assets/Art/` 下的目录 |

## 工作流

1. 美术在本目录编辑 PSD/AI；
2. 导出 `.png` 进 `client/Assets/Art/<对应分类>/`；
3. 大文件建议走 **Git LFS**（`git lfs track "*.psd" "*.ai" "*.fbx"`）。
