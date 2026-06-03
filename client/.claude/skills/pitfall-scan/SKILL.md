---
name: pitfall-scan
description: 巡检本仓已知的"Editor 正常、真机/打包/平台才炸"的坑(shader 裁剪品红、URP 内置 shader、休眠网络服务、.bat 编码、缺 .meta、绕过框架取资源)。当用户说"打包前查一下/真机出问题了/扫一下坑/pitfall"、或在改 shader·材质·打包设置·网络·存档后、或上线打包前触发。
---

# pitfall-scan

按 `Docs/踩坑记录.md` 把**能机械检测**的坑 grep 出来,逐条给现象/位置/修法。**先读文档,再扫,后报告**。不改代码(除非用户明确要 `--fix`)。

## 流程

### 1. 先读 `Docs/踩坑记录.md`

必读全文(尤其文末「skill 检查项映射」表)。文档是事实来源,本 skill 只是它的自动巡检器。文档里没有的坑不要凭空报。

### 2. 逐项跑下面的检查

工作目录是 `client/`。用 Grep 工具(ripgrep)。每命中一条,记下 `文件:行` 与上下文,**按"判定"栏过滤掉安全情形**,只报真正可疑的。

#### 检查 1 — 运行时 `Shader.Find` 品红风险(§1,最高优先级)
- 搜:`Shader\.Find` 于 `Assets/Scripts/GamePlay/` 与 `Assets/Scripts/Framework/`(**排除 `Assets/Scripts/Editor/`**——编辑期 builder 把材质烘进预制体,通常安全)。
- 判定为**坑**:命中处是运行时路径,且形如 `new Material(Shader.Find("..."))` 或把 `Shader.Find` 的结果直接建材质,**且**全仓没有一个 `Assets/Resources/**/*.mat` 用同一个 shader(没有序列化资产占住变体 → 打包会被裁 → 真机品红)。
- 判定为**安全**(不报或仅提示):shader 来自 `Resources.Load<Shader>(...)`(在 Resources 下),或已有 `Resources/` 材质引用它,或仅作模板缺失时的兜底分支。
- 已知正解参照:`WeaponModelUtil.MakeUnlit` + `Resources/Materials/UnlitPreview.mat`。

#### 检查 2 — URP 里用内置 shader(§1)
- 搜代码:`Shader\.Find\("Standard"\)`、`Shader\.Find\("Diffuse"\)` 等内置名出现在运行时路径。
- 搜材质:`Assets/**/*.mat` 里 `m_Shader` 指向内置 shader(guid 全零 `0000000000000000f000000000000000` 且 fileID 是 Standard/Diffuse 那几个)。URP 下内置 shader = 品红。
- 报告时建议换 URP/Lit(guid `933532a4fcc9baf4fa0491de14d08ed7`)或 URP/Unlit(guid `650dd9526735d5b46b79224bc6e94025`)。

#### 检查 3 — 用了休眠的网络服务(§4)
- 搜:`ctx\.Get<INetworkSession>|ctx\.Get<ILobbyService>|ctx\.Get<INetworkTransport>|Get<NetworkRuntime>`。
- 打开 `Assets/Scripts/Framework/GameContext/GameBootstrapper.cs`,确认对应 `ctx.Register<INetworkSession>` / `<ILobbyService>` / transport 那几行**是否仍被注释**。若仍注释而代码在取用 → 运行抛异常,报坑。

#### 检查 4 — `.bat` 非 CRLF / 非 ASCII(§6)
- 对 `tools/*.bat` 和仓库根 `*.bat`:用 Bash 检查是否含 CRLF、是否纯 ASCII。
  例:`file tools/*.bat`;或 `rg -l $'[^\x00-\x7F]' tools/*.bat`(非 ASCII);`rg -L $'\r$' <file>` 判断有无 CR。
- LF-only 或含非 ASCII 字节 → 报坑(双击闪退/乱码),建议 PowerShell 强制 CRLF+ASCII 重写。

#### 检查 5 — 新增文件缺 `.meta`(§5)
- 在 `client/Assets/` 下找出有 `.cs`/资源文件但**缺同名 `.meta`** 的(反之 `.meta` 多余也提一句)。
  例:`git -C .. status --porcelain` 看新增文件里 `.cs`/`.prefab`/`.mat`/`.asset` 是否都带了配套 `.meta`。
- 缺 `.meta` → 报坑(队友 GUID 漂移)。

#### 检查 6 — 绕过框架直取资源(代码规范 §6/§10,次要)
- 搜运行时路径(非 Editor)里的 `Resources\.Load`(应走 `ResMgr`)与 `GameObject\.Find`(应走 `SceneReferenceProvider`)。
- 这是风格/可维护性提示,非真机崩溃;低优先级,标注即可。`ResMgr` 内部、`SceneReferenceService` 兜底的那处属预期,排除。

### 3. 报告

按严重度排序输出,每条:

```
[严重] 检查N 坑名 — 文件:行
  现象/风险:一句话
  修法:一句话 + 指向 Docs/踩坑记录.md §X
```

- 真机崩溃/品红类(检查 1/2/3) = 高;`.meta`/`.bat`(4/5) = 中;风格(6) = 低。
- 没命中就明说"未发现已知坑",别硬凑。
- 末尾提醒:**改完 shader/材质/打包设置/宏后必须干净重打 APK 才进真机**。

### 4. 只有用户带 `--fix` 才动手改

否则只报告。要改时按 `Docs/踩坑记录.md` 的「修法」执行,改完复述动到哪些文件。

## 发现新类型的坑时

如果排查出一个**文档里还没有、且可 grep 检测**的新坑:
1. 先在 `Docs/踩坑记录.md` 加一节(现象/根因/排查/修法/防复发)+ 在文末映射表加一行;
2. 再回本 SKILL.md 的「检查」清单加一条对应检查;
3. 顺序别反——文档是事实来源,skill 跟着文档走。

## 不要
- 不要报文档里没有的"通用最佳实践";只拾取 `踩坑记录.md` 登记过的坑。
- 不要把编辑期 `Editor/` 下的 `Shader.Find`、`ResMgr` 内部的 `Resources.Load` 这类预期用法当坑报。
- 不要在没带 `--fix` 时擅自改代码。