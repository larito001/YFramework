---
name: excel-generation
description: 配表生成 — 接受一份配表规划文档（必填，C:\UnityProject\YFramework\配表规划\ 下，由 code-planning skill 在 Step 4.5 同步产出），按《项目规范》§5 与 `tools/配表工具复刻指南.md` 规则，在 C:\UnityProject\YFramework\excel\3xlsx\ 下生成 .xlsx 配表文件。同时在 C:\UnityProject\YFramework\tools\excel_builders\ 下落地一份持久化的 Python 生成器（可重复执行）。**只产 xlsx + builder.py**：不手写 .proto / .cs / .bytes，**不调** `publish_config.py` / `发布配表.bat`（由用户/外部 orchestrator 触发，遵循"skill 之间互相不感知、不串调"原则），不覆盖既有 xlsx。当用户说"为 GP-Xxx 生成配表"、"按规划造表"、"按 GP-Xxx-Excel-v1 生成 xlsx"、"build excel for X"、调用 /excel-generation 时触发。
---

# 配表生成 Skill

你现在的角色是**资深游戏工具开发**。任务：把已确定的需求/规划翻译成可被项目工具链发布的 xlsx 配表，落到 `excel/3xlsx/` 下。

**核心思路 — 为什么生成 builder 脚本而非直接写 xlsx**：

xlsx 是二进制 OOXML，Write 工具无法生成；且配表后续可能因策划微调而需重生成。所以本 skill 写一份 `tools/excel_builders/build_<table>.py`（用 openpyxl 构造表头与数据），然后 Bash 执行它产出 `.xlsx`。Python 脚本持久化、可审计、可重跑。这与 `prefab-generation` 用 Editor 脚本让 Unity 产 prefab 的思路一致。

## Step 0 · 始终先读规范与工具链

进入本 skill 后，**第一步必须**读取（或确认本会话已读）：

```
C:\UnityProject\YFramework\Docs\项目规范.md                  (§3.1 资源、§5 配表流程)
C:\UnityProject\YFramework\Docs\需求规范.md                  (§4 数据，含配表清单约定)
C:\UnityProject\YFramework\tools\配表工具复刻指南.md          (§4 表头结构、§4.3 类型系统、§4.4 目标键、§4.6 树形)
C:\UnityProject\YFramework\tools\tools_config.ini             (确认 config_dir 指向 ../excel)
C:\UnityProject\YFramework\tools\gtp\common\ConfigData.py     (类型白名单 isValidType)
C:\UnityProject\YFramework\tools\gtp\reader\ExcelReader.py    (生成的 xlsx 必须能被它无错读取)
```

**重点掌握的硬约束**：

- Row1=列名 / Row2=colID / Row3=type / Row4=target+key / Row5=ext(可空) / Row6=default / Row7+=数据。
- 树形表：Row4 含 `main`/`child`/`row` → 默认值移到 Row7，数据从 Row8 开始。
- 类型白名单（不在白名单 = 拒绝生成）：
  - 基础：`int / uint / float / bool / string`
  - 复合：`array.<基础>` / `array.vec2.<基础>` / `array.vec3.<基础>` / `vec2.<基础>` / `vec3.<基础>` / `map.<基础>.<基础>`
- 分隔符：array 用 `|`、vec 用 `~`、map 用 `|` 分对+`~` 分 K-V；分号 `;` 等价于 `|`，等号 `=` 等价于 `~`（excel 中两种写法都行）。
- 至少一个 key 列（target 含 `key`）；双键表两个 key 列；主键索引规则按指南 §4.5。

## Step 1 · 定位输入并展开 spec

### 1.1 解析输入

| 用户输入 | 操作 |
|---|---|
| 配表规划 id（如 `GP-Combat-Excel-v1`，**首选**） | Glob `配表规划/**/{id}.md` → 必须找到。Read 全文。从 `links.source` 取策划案 id、`links.plan` 取代码规划 id（仅用于交付报告引用，必要时可 Read 校验存在） |
| 策划案 id（如 `GP-Combat-v1`） | Glob 推算 `配表规划/**/<id 去 -v*>-Excel-v*.md`，找到 → 转为上一行流程；找不到 → **停**，提示先 `/code-planning <策划案 id>` 出配表规划 |
| 代码规划 id（如 `GP-Combat-Plan-v1`） | 推算 `配表规划/**/<id 去 -Plan-v*>-Excel-v*.md`，同上 |
| Feature 名（如 `Combat`、`战斗`） | Glob `配表规划/**/*Combat*.md`，多匹配 → `AskUserQuestion` 让用户选 |
| 显式 xlsx 名（如 `skill.xlsx`） | 全文 Grep `skill.xlsx` 在 `配表规划/**`，找到对应配表规划文档 |
| 绝对路径 | 直接 Read |
| `/excel-generation` 无参 | 开放询问"为哪份配表规划生成 xlsx？给 id（如 `GP-Combat-Excel-v1`）或 xlsx 名" |

找不到配表规划 → **停**。提示："`配表规划/` 下没有匹配 `<query>` 的文档。先调 `/code-planning <策划案 id>` 出代码规划+配表规划，再回来。"

### 1.2 校验输入

- 配表规划文档 frontmatter `links.source`（策划案 id）+ `links.plan`（代码规划 id）必填，**任一缺失 → 停**。
- 配表规划 §3 至少 1 张表的完整 schema（Row1~Row6 列定义表）；缺则**停**，提示"配表规划残缺，调 /code-planning 升 v 重出"。
- 通过 `links.source` 链回策划案，若策划案 `status=Draft` → 在交付报告强提醒"上游需求未定稿，配表可能跟随返工"，但**继续执行**。
- 通过 `links.plan` 链回代码规划，若代码规划文件不存在 → **停**：配表规划必须有对应代码规划。
- 配表规划自身 `status` 任意（一般 `Draft`，与代码规划同步），不阻塞。

### 1.3 展开"待生成表清单"（spec 抽取）

**所有 spec 字段（列名/类型/target/default/示例数据）一律从配表规划 §3 直接读**，不再两边凑。每张表对应配表规划 §3 中的一节（§3.1, §3.2, ...）。

> 如果发现配表规划 §3 与策划案 §4 数据 或 代码规划 §6 [配表] 列名不一致 → 报"上游不一致"，**停**，要求 code-planning 升 v 重出配表规划，**本 skill 不私自调和**。

对每张 §3 列出的 xlsx，构建内部 spec 对象（保存到对话上下文，不写文件）：

```python
{
    "table_name": "skill",          # 文件名 stem，小写、ASCII
    "purpose": "技能数值与冷却",
    "is_tree": False,
    "columns": [
        {"col_name":"技能ID", "col_id":"skill_id", "type":"uint", "target":"client_key", "ext":"", "default":"0"},
        {"col_name":"名称",   "col_id":"skill_name","type":"string","target":"client",     "ext":"", "default":""},
        # ...
    ],
    "sample_rows": [
        ["1001","火球术","50","2.5","fire|magic","atk~10"],
        # ...
    ],
    "excel_plan_doc": "配表规划/Gameplay/Combat/GP-Combat-Excel-v1.md",
    "source_doc":     "策划案/Gameplay/Combat/GP-Combat-v1.md",      # 从 links.source
    "plan_doc":       "代码规划/Gameplay/Combat/GP-Combat-Plan-v1.md",# 从 links.plan
}
```

### 1.4 校验 spec（一票否决）

每张表逐项检查：

| 检查 | 不通过 |
|---|---|
| 表名 lowercase + ASCII | 拒，要求改名 |
| 至少 1 个 key 列 | 拒，要求补 key |
| 每列 `type` 在白名单（参考 `tools/gtp/common/ConfigData.py` 的 `isValidType`） | 拒，列出非法类型 |
| 每列有 `default`（基础类型必填，复合可空 → 自动用类型默认值） | 自动填 |
| `col_id` 为 `[a-z][a-z0-9_]*`，无重复 | 拒 |
| 树形表（Row4 含 main/child/row）一致性：必须既有 main 又有 child（否则非树形） | 拒，要求修正 |
| 双键表恰好 2 个 key 列 | 拒 |
| `excel/3xlsx/<table>.xlsx` 已存在 | **停**，进入 §1.5 修改型流程；**绝不静默覆盖** |

### 1.5 已存在 xlsx 的处理（修改型变更）

如果目标 `excel/3xlsx/<table>.xlsx` 已存在：

1. 用 `tools/gtp/reader/ExcelReader.py` 读取既有列结构（`ConfigData.colID/colType/colTarget`）。
2. 与 spec diff：
   - **纯加列**（既有列保留、type/target 不变）→ **本 skill 不动既有 xlsx**，输出"补列指引文档"`配表规划/<同目录>/<原 id 去 -Excel-vN>-ExcelPatch-v1.md`，列出"在 X 列后插入 Y 列、Row1~Row6 各填什么"，由策划/工具人在 Excel 内手工添加（保留既有数据）。
   - **改名/删列/改类型** → **拒**：这是破坏性变更，可能让既有数据丢失。提示用户"用 Excel 打开手工迁移；或在策划案里说明'本表整体重建'后 v 进位手工 rm 旧文件再重跑"。
3. 修改型变更下 **不生成 builder.py**（避免覆盖风险）。
4. 修改型变更下用户在 Excel 内手工编辑 xlsx 完成后自行 `.\发布配表.bat`（本 skill 任何流程都不调 publish）。

### 1.6 列范围给用户确认

输出一段话：

```
将生成以下 xlsx：

新增（N）：
  excel/3xlsx/skill.xlsx     6 列，2 示例行；主键 skill_id (uint)
  excel/3xlsx/buff.xlsx      4 列，0 示例行；主键 buff_id (uint)

修改（M，仅出补列指引，不动 xlsx）：
  excel/3xlsx/hero.xlsx      新增 1 列 init_pos (vec2.int)
                             → 指引文档：配表规划/Gameplay/Combat/GP-Combat-ExcelPatch-v1.md

跳过（K）：
  excel/3xlsx/quest.xlsx     已存在且结构一致

builder 脚本输出位置：
  tools/excel_builders/_helpers.py        (共享，已存在则跳过)
  tools/excel_builders/build_skill.py
  tools/excel_builders/build_buff.py

来源：
  配表规划/Gameplay/Combat/GP-Combat-Excel-v1.md (status=Draft, §3 含 2 张表)
  ├─ links.source: 策划案/Gameplay/Combat/GP-Combat-v1.md (status=Approved)
  └─ links.plan:   代码规划/Gameplay/Combat/GP-Combat-Plan-v1.md (status=Draft)
```

**用户不显式确认前不写任何文件**。

## Step 2 · 生成共享 helpers（首次或缺失）

文件：`tools/excel_builders/_helpers.py`

**已存在 → 跳过**（不覆盖人工调过的）。骨架：

```python
"""excel-generation skill 的共享辅助。手工修改风险自负。"""

from openpyxl import Workbook
from openpyxl.styles import Font, PatternFill

HEADER_FILL_NAME    = PatternFill("solid", fgColor="DDEBF7")  # Row1
HEADER_FILL_ID      = PatternFill("solid", fgColor="E2EFDA")  # Row2
HEADER_FILL_TYPE    = PatternFill("solid", fgColor="FFF2CC")  # Row3
HEADER_FILL_TARGET  = PatternFill("solid", fgColor="FCE4D6")  # Row4
HEADER_FILL_EXT     = PatternFill("solid", fgColor="F4F4F4")  # Row5
HEADER_FILL_DEFAULT = PatternFill("solid", fgColor="FFFFFF")  # Row6/7

BASE_TYPES = {"int", "uint", "float", "bool", "string"}


def is_valid_type(t: str) -> bool:
    if t in BASE_TYPES:
        return True
    parts = t.split(".")
    if len(parts) == 1:
        return False
    if parts[0] == "array":
        return is_valid_type(t[6:])
    if len(parts) == 2 and parts[0] in ("vec2", "vec3") and parts[1] in BASE_TYPES:
        return True
    if len(parts) == 3 and parts[0] == "map" and parts[1] in BASE_TYPES and parts[2] in BASE_TYPES:
        return True
    return False


def write_header(ws, columns, is_tree=False):
    """写 Row1~Row6（树形表 default 在 Row7）。"""
    bold = Font(bold=True)
    for i, c in enumerate(columns, start=1):
        cell1 = ws.cell(row=1, column=i, value=c["col_name"])
        cell1.fill = HEADER_FILL_NAME
        cell1.font = bold
        ws.cell(row=2, column=i, value=c["col_id"]).fill = HEADER_FILL_ID
        ws.cell(row=3, column=i, value=c["type"]).fill = HEADER_FILL_TYPE
        ws.cell(row=4, column=i, value=c["target"]).fill = HEADER_FILL_TARGET
        ws.cell(row=5, column=i, value=c.get("ext", "")).fill = HEADER_FILL_EXT
        default_row = 7 if is_tree else 6
        ws.cell(row=default_row, column=i, value=c.get("default", "")).fill = HEADER_FILL_DEFAULT
        col_letter = ws.cell(row=1, column=i).column_letter
        ws.column_dimensions[col_letter].width = max(12, len(str(c["col_id"])) + 4)


def write_data_rows(ws, columns, rows, is_tree=False):
    start_row = 8 if is_tree else 7
    for r, row in enumerate(rows, start=start_row):
        for c_idx, val in enumerate(row, start=1):
            ws.cell(row=r, column=c_idx, value=val)


def build_xlsx(out_path, table_name, columns, rows, is_tree=False):
    # 校验
    for c in columns:
        if not is_valid_type(c["type"]):
            raise ValueError(f"Invalid type '{c['type']}' for column '{c['col_id']}'")
    if not any("key" in c["target"] for c in columns):
        raise ValueError("table must have at least one key column (target containing 'key')")
    wb = Workbook()
    ws = wb.active
    ws.title = table_name
    write_header(ws, columns, is_tree)
    write_data_rows(ws, columns, rows, is_tree)
    wb.save(out_path)
    return out_path
```

## Step 3 · 生成单个 builder（每张新增表一个文件）

文件：`tools/excel_builders/build_<table>.py`

骨架（以 skill.xlsx 为例）：

```python
"""
Generated by excel-generation skill, DO NOT MODIFY by hand.
若需调整列结构：先改对应配表规划文档（升 v 进位），再重跑 /excel-generation。

excel_plan_doc: 配表规划/Gameplay/Combat/GP-Combat-Excel-v1.md
source_doc:     策划案/Gameplay/Combat/GP-Combat-v1.md
plan_doc:       代码规划/Gameplay/Combat/GP-Combat-Plan-v1.md
created:        2026-05-10
"""

import os
import sys

# 让 _helpers 在同目录下可被 import
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from _helpers import build_xlsx

TABLE_NAME = "skill"
OUT_PATH = os.path.normpath(os.path.join(
    os.path.dirname(os.path.abspath(__file__)),
    "..", "..", "excel", "3xlsx", TABLE_NAME + ".xlsx",
))

COLUMNS = [
    {"col_name": "技能ID", "col_id": "skill_id",   "type": "uint",            "target": "client_key", "ext": "", "default": "0"},
    {"col_name": "名称",   "col_id": "skill_name", "type": "string",          "target": "client",     "ext": "", "default": ""},
    {"col_name": "伤害",   "col_id": "damage",     "type": "int",             "target": "client",     "ext": "", "default": "0"},
    {"col_name": "冷却",   "col_id": "cooldown",   "type": "float",           "target": "client",     "ext": "", "default": "0"},
    {"col_name": "词条",   "col_id": "tags",       "type": "array.string",    "target": "client",     "ext": "", "default": ""},
    {"col_name": "加成",   "col_id": "bonus",      "type": "map.string.int",  "target": "client",     "ext": "", "default": ""},
]

ROWS = [
    [1001, "火球术", 50, 2.5, "fire|magic", "atk~10"],
    [1002, "冰锥术", 40, 3.0, "ice|magic",  "atk~8"],
]

IS_TREE = False


if __name__ == "__main__":
    if os.path.exists(OUT_PATH):
        print(f"[excel-generation] REFUSE: {OUT_PATH} 已存在，不覆盖。手工 rm 后再跑或走修改型流程。")
        sys.exit(2)
    os.makedirs(os.path.dirname(OUT_PATH), exist_ok=True)
    p = build_xlsx(OUT_PATH, TABLE_NAME, COLUMNS, ROWS, IS_TREE)
    print(f"[excel-generation] WROTE {p}")
```

**强制不变量**：

- 顶部注释含 `excel_plan_doc` / `source_doc` / `plan_doc` / `created` 四行（这是 builder.py 的"frontmatter"等价物，给 skill-chain-maintenance 审计用）。
- `if os.path.exists(OUT_PATH): refuse` 必须有 —— 防止误覆盖。
- 不 import 项目代码，只 import openpyxl 与 `_helpers`（让 builder 在没有 Unity 工程的环境也能跑）。
- 默认值用字符串字面（`"0"` 而非 `0`）—— 与 `ExcelReader.getPythonValue` 兼容（它会按 type 转）。

## Step 4 · 执行 builder 并自检

### 4.1 执行

按依赖顺序（一般无序）依次跑：

```
python tools/excel_builders/build_skill.py
python tools/excel_builders/build_buff.py
```

任何一份 exit code != 0 → **停**，把 stderr 列给用户。

### 4.2 自检

逐张已生成的 xlsx：

- [ ] 文件存在于 `excel/3xlsx/<table>.xlsx`
- [ ] 用 `tools/gtp/reader/ExcelReader.py` 静态读一遍：

  ```bash
  cd tools && python -c "import sys; sys.path.insert(0,'.'); from gtp.reader.ExcelReader import readExcel; d = readExcel('../excel/3xlsx/skill.xlsx'); print('OK', d.id, len(d.colID), len(d.data))"
  ```

  预期输出 `OK skill <列数> <行数>`。任何异常 → **停**，把异常贴出，回 Step 3 修 builder。
- [ ] 主键列在 `colTarget` 中含 `C`（客户端可见）。
- [ ] 类型白名单全过（在 reader 读取时已校验）。

### 4.3 不动既有产物

- [ ] `git diff --name-only -- client/` 应为空（本 skill 不修改 Unity 工程）。
- [ ] `git diff --name-only -- excel/3xlsx/` 只能含本次新增 .xlsx。**修改既有 .xlsx → 立即回滚**。
- [ ] **不调** `tools/publish_config.py` / `发布配表.bat`（这些由用户触发；本 skill 不串调下游工具）。

## Step 5 · 修改型变更：补列指引文档

仅在 §1.5 触发。

路径：与对应配表规划文档同目录，文件名：`<原 id 去 -Excel-vN>-ExcelPatch-v1.md`，例如
`配表规划/Gameplay/Combat/GP-Combat-ExcelPatch-v1.md`。

frontmatter：

```yaml
---
id: GP-Combat-ExcelPatch-v1
title: <Feature> 配表补列指引 v1
type: ExcelPatch
status: Draft
owner: <策划负责人>
created: <today, YYYY-MM-DD>
updated: <today, YYYY-MM-DD>
version: 0.1
links:
  source:     GP-Combat-v1            # 策划案
  plan:       GP-Combat-Plan-v1       # 代码规划
  excel_plan: GP-Combat-Excel-v1      # 配表规划（本指引衍生自此）
  excel:      [hero.xlsx]             # 涉及的 xlsx
---
```

正文模板：

```markdown
## 1. 概览
- 目标 xlsx：`excel/3xlsx/hero.xlsx`
- 变更性质：纯加列（不动既有列）
- 操作人：<策划/工具人>，建议在 Excel 内手工完成

## 2. 现状（既有列，按读取顺序）
| 序 | colID | type | target |
|---|---|---|---|
| 1 | hero_id | uint | client_key |
| 2 | hero_name | string | client |
| ... | ... | ... | ... |

## 3. 待新增列
| 插入位置（在第 N 列后） | Row1 | Row2 colID | Row3 type | Row4 target | Row6 default |
|---|---|---|---|---|---|
| 第 9 列后 | 初始位置 | init_pos | vec2.int | client | 0~0 |

## 4. 操作步骤
1. 在 Excel 打开 `excel/3xlsx/hero.xlsx`。
2. 在第 9 列右侧插入新列（保持 Row1~Row6 数据不错位）。
3. Row1~Row6 按上表填入。
4. 数据行（Row7+）保留为空 → 自动用 default `0~0`；如需具体值，逐行填。
5. 保存。**不要**改既有列名/类型。
6. 在仓库根跑 `.\发布配表.bat` 验证。

## 5. 验收
- [ ] `python tools/publish_config.py path/to/hero.xlsx` 无报错
- [ ] 生成的 `client/Assets/ScriptGenerated/Config/HeroConfig.cs` 含新字段 `init_pos`
- [ ] 既有数据行未丢失

## 6. 变更记录
- v0.1 - <today> - <owner> - 初版指引
```

## Step 6 · 交付报告

```
## 配表生成完成 — <配表规划 id>

### 产物
- excel/3xlsx/skill.xlsx        (新增, 6 列, 2 示例行)
- excel/3xlsx/buff.xlsx         (新增, 4 列, 0 示例行)
- tools/excel_builders/_helpers.py     (共享，已存在则跳过)
- tools/excel_builders/build_skill.py
- tools/excel_builders/build_buff.py

### 修改型指引（不动 xlsx）
- 配表规划/Gameplay/Combat/GP-Combat-ExcelPatch-v1.md
  → 指引在 hero.xlsx 上加列 init_pos (vec2.int)

### 自检结果
- builder 落地 ............ ✅
- xlsx 文件存在 ........... ✅
- ExcelReader 静态读取 .... ✅（skill: 6 列 2 行；buff: 4 列 0 行）
- 类型白名单 .............. ✅
- 主键列 client 可见 ...... ✅
- 未污染 GamePlay/Framework  ✅
- 未调 publish ............ ✅（本 skill 不串调下游工具）

### 下一步（人工 / 外部 orchestrator）
1. 在仓库根跑 `.\发布配表.bat`（或 `python tools/publish_config.py`）→ 生成 `ScriptGenerated/Config/*Config.cs` + `Resources/Config/Data/*.bytes` + `ScriptGenerated/Proto/*.cs`。
2. 修改型变更：按 ExcelPatch-v1 在 Excel 内补列后，同样跑上一步 publish。
3. 让 Unity Editor 打开工程一次，让 .cs.meta 自动生成（项目规范 §1.4），再 git add。

### 当前状态
- 新增 xlsx 数：N（待人工 publish）
- 修改型 xlsx 数（指引）：M（待人工编辑 + publish）
- 跳过（已存在且一致）：K
- 来源：<配表规划 id> @ status=<x>（链回 <策划案 id> @ <s_status> + <代码规划 id> @ <p_status>）
```

## 严守红线

- **不覆盖既有 xlsx**。任何 `excel/3xlsx/<x>.xlsx` 已存在 → 默认拒写；只能走修改型补列指引（Step 5）。**绝不静默 overwrite**。
- **不调发布工具链**。不跑 `publish_config.py` / `发布配表.bat` / `protoc`。这些由用户/外部 orchestrator 触发。本 skill 不串调下游工具，遵循"skill 之间互相不感知"原则（见 skill-chain-maintenance Step A7）。
- **不写 .proto / .cs / .bytes**。这些是发布配表的产物，本 skill 不碰；不直接 Edit/Write 任何 `ScriptGenerated/` 或 `_publish/` 下的文件。
- **不发明列**。每列必须在配表规划 §3 出现，凭"通用习惯"加 `created_at`、`enabled` 等列一律拒。
- **schema 唯一权威源是配表规划 §3**。不直接读策划案/代码规划 §6 推 schema；如配表规划与策划案/代码规划不一致 → 报"上游不一致"，停，要求 code-planning 升 v 重出配表规划，不私自调和。
- **类型必须在白名单**。`tools/gtp/common/ConfigData.py:isValidType` 拒掉的就是拒掉的。新类型 → 走"待框架扩展"流程修工具链，不在本 skill 处理。
- **不修改业务代码**。`client/` 与 `Framework/` 一行不动；用 `git diff --name-only` 自查。
- **不私自加示例数据**。策划/规划没给数据行 → 只产默认值行（树形表 Row7、普通表 Row6）。不"造一个 1001/1002 测试数据"塞进去。
- **builder.py 顶部注释三件套必填**：`source_doc / plan_doc / created`。审计依据。
- **始终中文**撰写交付报告；builder.py 内代码标识符英文，注释中文/英文皆可。
- **始终用绝对日期** `YYYY-MM-DD`。
