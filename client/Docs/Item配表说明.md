# Item 物品配表说明

物品数据走打表工具(Excel → protobuf),**不使用 ScriptableObject**。
源表:`excel/3xlsx/item.xlsx`,发布后产物:

| 产物 | 路径 | 用途 |
|---|---|---|
| proto | `tools/_publish/proto/item.proto` | 协议定义 |
| C# 包装类 | `client/Assets/ScriptGenerated/Config/ItemConfig.cs` | 挂到 `ConfigManager.itemConfig` |
| C# 消息类 | `client/Assets/ScriptGenerated/Proto/Item.cs` | protobuf 生成的 `Item` |
| 数据 | `client/Assets/Resources/Config/Data/Item.bytes` | 运行时加载的二进制 |

运行时读取:`ConfigManager.itemConfig.Get(id)` 返回一个 `Item`(命名空间 `YFramework.Config`)。
背包层封装在 `BagSystem.GetItem(id)` / `GetItemType(id)` / `GetItemQuality(id)`。

---

## 字段定义

源表为 6 行表头 + 数据行(详见 `DOCS/配表工具…` 或现有 hero/skill 表)。各列含义:

| 字段ID | 类型 | 含义 | 说明 |
|---|---|---|---|
| `id` | uint | 物品 ID | **主键**,全局唯一,存档/查询的 key |
| `name` | string | 名称 | 显示在格子左上角 + tooltip |
| `desc` | string | 描述 | tooltip 显示 |
| `type` | uint | 类型 | 见下方 `ItemType` 枚举 |
| `quality` | uint | 品质 | 见下方 `ItemQuality` 枚举,决定格背景色 |
| `iconPath` | string | 图标路径 | `Resources` 相对路径(如 `UI/Item/coin`),`ResMgr.Load<Sprite>` 加载 |
| `width` | int | 占格宽 | 包围盒宽(格),≥1 |
| `height` | int | 占格高 | 包围盒高(格),≥1 |
| `shape` | string | 形状掩码 | 见下方「形状掩码」,留空=按 width×height 填满矩形 |
| `maxStack` | int | 叠加上限 | 单堆最大数量,1=不可叠加 |
| `value` | int | 预估价值 | tooltip 显示,可用于售卖/估值 |
| `sortPriority` | int | 排序权重 | 整理时同类按此降序 |

> ⚠️ `id=0` 保留不用(proto3 默认值);`width/height ≤ 0` 运行时容错为 1。

---

## ItemType 类型枚举

配表填 int,代码 `(ItemType)item.Type` 读取。定义见 `GamePlay/BagSystem/ItemEnums.cs`。

| 值 | 枚举 | 含义 | 特殊规则 |
|---|---|---|---|
| 0 | `Other` | 其它 | — |
| 1 | `Consumable` | 消耗品 | 注册使用处理器后可「使用」,成功后扣 1 |
| 2 | `Equipment` | 装备 | — |
| 3 | `Material` | 材料 | — |
| 4 | `QuestItem` | 任务物品 | **不可丢弃**(`BagSystem.Discard` 拦截) |
| 5 | `Currency` | 货币 | — |

新增类型:先在 Excel 的 `type` 列约定取值,再在 `ItemType` 枚举补一项。

---

## ItemQuality 品质枚举

配表填 int,决定背包格背景色与 tooltip 名称颜色(暗黑/塔科夫风格)。
配色集中在 `ItemQualityPalette`(`ItemEnums.cs`),改色只动这一处。

| 值 | 枚举 | 颜色 |
|---|---|---|
| 0 | `Common` | 灰白 |
| 1 | `Uncommon` | 绿 |
| 2 | `Rare` | 蓝 |
| 3 | `Epic` | 紫 |
| 4 | `Legendary` | 橙 |

---

## 形状掩码 shape

支持 L/T 等**不规则多边形**。规则:

- 按行用 `|` 分隔,字符 `1`=占据该格、`0`=空。
- 包围盒尺寸应与 `width`×`height` 一致(不一致时以掩码为准并告警)。
- **留空 = 按 `width`×`height` 填满矩形**(普通物品都留空即可)。
- 解析见 `GamePlay/BagSystem/ItemShape.cs`,运行时预算 4 个旋转朝向。

示例:

| 掩码 | 形状 | 占格 |
|---|---|---|
| (空) | 矩形 | width×height 全占 |
| `10\|11` | L 形(2×2 缺右上) | (0,0)(0,1)(1,1) |
| `111\|010` | T 形(3×2) | 上排 3 格 + 下排中间 1 格 |

> 行内字符数可不等长,以最长行为包围盒宽;`0` 补位即可。

---

## 现有物品一览(示例数据)

| id | 名称 | type | quality | 尺寸/形状 | maxStack | value |
|---|---|---|---|---|---|---|
| 1001 | 小型治疗药水 | 消耗品 | 普通 | 1×1 | 20 | 15 |
| 1002 | 大型治疗药水 | 消耗品 | 优秀 | 1×2 | 10 | 40 |
| 1003 | 能量饮料 | 消耗品 | 优秀 | 1×1 | 20 | 25 |
| 2001 | 铁剑 | 装备 | 优秀 | 1×3 | 1 | 120 |
| 2002 | 铁甲 | 装备 | 稀有 | 2×2 | 1 | 200 |
| 3001 | 铁矿石 | 材料 | 普通 | 1×1 | 50 | 8 |
| 3002 | 木材 | 材料 | 普通 | 2×1 | 50 | 5 |
| 4001 | 远古信物 | 任务 | 史诗 | L 形 `10\|11` | 1 | 0 |
| 4002 | 曲柄扳手 | 材料 | 稀有 | T 形 `111\|010` | 1 | 90 |
| 5001 | 金币 | 货币 | 普通 | 1×1 | 999 | 1 |

---

## 修改流程

1. 编辑 `excel/3xlsx/item.xlsx`(**先关掉 Excel,否则发布会因文件占用失败**)。
2. 跑发布:`cd tools && python publish_config.py`(或单表 `python publish_config.py ../excel/3xlsx/item.xlsx`)。
3. 回 Unity 等编译;新增/改 `type`/`quality` 取值时,记得同步 `ItemType`/`ItemQuality` 枚举。
4. 图标:把 Sprite 放到 `iconPath` 指向的 `Resources` 路径下。

---

## 常用 API(`BagSystem`)

```csharp
var bag = ctx.Get<BagSystem>();
bag.AddItem(3001, 80);        // 放入 80 个铁矿石(超单堆上限自动分多堆),返回放不下的剩余
bag.GetItem(3001);           // 取配表 Item(name/desc/value/...)
bag.GetItemType(3001);       // ItemType
bag.GetItemQuality(2002);    // ItemQuality
bag.IsStackable(3001);       // 是否可叠加(maxStack>1)
bag.MaxStack(3001);          // 单堆上限
```

相关代码:`GamePlay/BagSystem/`(模型 + 系统)、`GamePlay/UI/Bag/`(面板 + 控件)。
