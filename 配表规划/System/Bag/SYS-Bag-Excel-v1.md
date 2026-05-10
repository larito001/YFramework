---
id: SYS-Bag-Excel-v1
title: 背包系统 v1 · 配表规划
type: ExcelPlan
status: Draft
owner: 程序待指派
reviewers: [科斯魔]
created: 2026-05-10
updated: 2026-05-10
version: 0.1
links:
  source: SYS-Bag-v1
  plan: SYS-Bag-Plan-v1
  excel: [item.xlsx]
---

## 1. 概览

本规划基于 [SYS-Bag-v1] 策划案 §4.1 数据 + [SYS-Bag-Plan-v1] 代码规划 §6，定义 `excel/3xlsx/item.xlsx` 的完整 schema。**配表 schema 的最终决定权在程序**（类型 / 键 / 默认值 / target），列字段名来自策划。

设计取舍说明：

- 货币不再独立配表 → **复用 `item.xlsx` 中 `type=3`（Currency）的行**。该决定来自规划阶段对策划的澄清问询，已对齐 [SYS-Bag-v1] §3.1 / §4.1 / §4.3 三处描述。
- `type` 用 `uint`（0~3）而非 `string` 枚举名 —— 与业务侧 `BagItemType` 枚举值一一对应，运行时直接强转，避免字符串比较与拼写漂移。
- `iconPath` 是相对 `Resources/` 的路径字符串，由 `ResMgr.LoadHandleAsync<Sprite>` 消费，与 `Resources/UI/...` 现有约定一致。

## 2. 表清单

| xlsx | 性质 | 主键 | 列数 | 示例数据行数 |
|---|---|---|---|---|
| item.xlsx | 新增 | id (uint, client_key) | 7 | 4（道具 / 材料 / 任务物品 / 货币 各 1，占位待策划补全） |

## 3. 详细 schema

### 3.1 excel/3xlsx/item.xlsx (新增)

- **用途**：背包系统所有物品（含货币）的定义；id / 显示名 / 描述 / 类型 / 图标 / 堆叠上限 / 排序权重。
- **主键**：`id` (uint, client_key)
- **是否树形 / 双键**：否，单键单层。
- **客户端列**：全部
- **服务端列**：无（v1 单机本地存档，无服务端）

| 序 | Row1 列名 | Row2 colID | Row3 type | Row4 target | Row5 ext | Row6 default |
|---|---|---|---|---|---|---|
| 1 | 物品ID | id | uint | client_key | | 0 |
| 2 | 名称 | name | string | client | | |
| 3 | 描述 | desc | string | client | | |
| 4 | 类型 | type | uint | client | | 0 |
| 5 | 图标路径 | iconPath | string | client | | |
| 6 | 堆叠上限 | maxStack | int | client | | 99 |
| 7 | 排序权重 | sortPriority | int | client | | 0 |

**type 列取值约定**（与 GamePlay 侧 `BagItemType` 枚举一致）：

| type 值 | BagItemType | 说明 |
|---|---|---|
| 0 | Item | 道具 |
| 1 | Material | 材料 |
| 2 | Quest | 任务物品 |
| 3 | Currency | 货币（不占 20 格，进 BagData.currencyEntries） |

**示例数据**（占位 4 行；策划提供完整清单后由 excel-generation 重生成时覆盖）：

| id | name | desc | type | iconPath | maxStack | sortPriority |
|---|---|---|---|---|---|---|
| 1001 | 占位道具A | 这是一个用于联调的占位道具。 | 0 | UI/Item/1001 | 99 | 0 |
| 2001 | 占位材料A | 用于联调材料分类。 | 1 | UI/Item/2001 | 99 | 0 |
| 3001 | 占位任务物品A | 用于联调任务物品分类。 | 2 | UI/Item/3001 | 99 | 0 |
| 9001 | 金币 | 通用货币。 | 3 | UI/Currency/9001 | 99 | 0 |

> 说明：示例 4 行**仅为联调跑通**，名称 / 描述非终稿文案。策划补全 §4.1 物品清单（决策日期 2026-05-17）后，由 excel-generation skill 重新生成 xlsx。

## 4. 类型 / 校验约束

- 所有 type 取值在白名单：`uint / int / string`（仅基础类型，未使用 array / vec / map），符合 `tools/配表工具复刻指南.md` §4.3。
- target：仅使用 `client_key`（id 列）与 `client`（其他列），不涉及 `server / all / main / child / rowkey`。
- 至少 1 个 `*_key` 列：✅ id 为唯一 key 列，单键表。
- 文件名 lowercase + ASCII：✅ `item.xlsx`。
- 表名 = 文件名（不含扩展名）：✅ `item` → 自动生成 `ItemConfig` 类，partial 注入 `ConfigManager.itemConfig`。
- maxStack 默认 99（与 BagSlot 单格上限一致）；若策划后续要求某些物品自定义堆叠上限，直接在数据行覆盖默认值即可，无需改 schema。
- sortPriority 默认 0：相同 type 内按 (sortPriority asc, id asc) 排序；策划保留通过填非零值微调"一键整理"展示顺序的能力。

## 5. 变更记录

- v0.1 - 2026-05-10 - 程序待指派 - 初版，对应 [SYS-Bag-Plan-v1]
