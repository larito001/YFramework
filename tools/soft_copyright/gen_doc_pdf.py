# -*- coding: utf-8 -*-
"""
软件著作权登记 - 文档鉴别材料生成工具
===================================================
将项目文档（设计说明、模块规范、各系统使用手册等 Markdown）合并、轻量排版，
输出「前连续 30 页 + 后连续 30 页」（共 60 页，每页 40 行，满足软著"每页不少
于 30 行"）的 PDF。若整个文档不足 60 页，则输出全部。

特性：
  - 按指定顺序合并多个 Markdown 文档，每个文档加分节标题。
  - 轻量转换 Markdown -> 可读文本（标题/列表/代码块/表格/链接/图片占位）。
  - 长行自动换行并计入 40 行/页（不丢内容）。
  - 中文字体，页脚含软件名与页码。

用法：
  python gen_doc_pdf.py
  python gen_doc_pdf.py --name "我的软件 V1.0" --out doc.pdf

依赖：pip install fpdf2
"""

import os
import re
import sys
import argparse
from fpdf import FPDF

try:
    sys.stdout.reconfigure(encoding="utf-8")
    sys.stderr.reconfigure(encoding="utf-8")
except Exception:
    pass

# ============================ 默认配置 ============================
REPO = r"C:\UnityProject\YFramework"
# 文档合并顺序（相对仓库根目录）。排除第三方/插件文档。
DEFAULT_DOCS = [
    "README.md",
    "Docs/README.md",
    "Docs/项目规范.md",
    "Docs/模块规范.md",
    "Docs/代码规范.md",
    "client/Docs/README.md",
    "client/Docs/FrameworkUsage.md",
    "client/Docs/NetworkUsage.md",
    "client/Docs/StoreManagerUsage.md",
    "client/Docs/Item配表说明.md",
    "client/Docs/背包与宝箱系统说明.md",
    "client/Docs/技能系统使用指南.md",
    "client/Docs/Animancer 武器动画指南.md",
    "client/Docs/TapTap与广告SDK集成.md",
    "tools/配表工具复刻指南.md",
]
LINES_PER_PAGE = 40       # 每页行数（软著要求 >=30 行/页）
PAGES_EACH = 30           # 前 N 页 + 后 N 页
SOFTWARE_NAME = "YFramework 游戏客户端软件"
DOC_LABEL = "软件文档"
DEFAULT_OUT = r"C:\UnityProject\YFramework\软著文档_前30后30.pdf"

FONT_CJK = r"C:\Windows\Fonts\Deng.ttf"
FONT_CJK_FALLBACK = r"C:\Windows\Fonts\simsun.ttc"

FONT_SIZE = 10.0
TAB_WIDTH = 4
# ================================================================


# --------------------- Markdown 轻量转换 ---------------------
RE_IMG = re.compile(r"!\[([^\]]*)\]\([^)]*\)")
RE_LINK = re.compile(r"\[([^\]]+)\]\(([^)]+)\)")
RE_BOLD = re.compile(r"\*\*([^*]+)\*\*")
RE_ITAL = re.compile(r"(?<!\*)\*([^*]+)\*(?!\*)")
RE_CODE_INLINE = re.compile(r"`([^`]+)`")
RE_HEADING = re.compile(r"^(#{1,6})\s+(.*)$")
RE_BULLET = re.compile(r"^(\s*)[-*+]\s+(.*)$")
RE_HR = re.compile(r"^\s*([-*_])\1{2,}\s*$")


def md_to_text(lines):
    """把 Markdown 行列表转为可读纯文本行列表。代码块内保持原样。"""
    out = []
    in_code = False
    for raw in lines:
        line = raw.replace("\t", " " * TAB_WIDTH).rstrip("\n").rstrip()

        # 代码围栏
        if line.strip().startswith("```"):
            in_code = not in_code
            lang = line.strip()[3:].strip()
            out.append("    " + ("┄┄┄ 代码 ┄┄┄" if not in_code else
                                 (f"┄┄┄ 代码({lang}) ┄┄┄" if lang else "┄┄┄ 代码 ┄┄┄")))
            continue
        if in_code:
            out.append("    " + line)        # 代码原样、缩进
            continue

        # 图片占位
        line = RE_IMG.sub(lambda m: f"【图：{m.group(1) or '图示'}】", line)

        # 标题
        m = RE_HEADING.match(line)
        if m:
            level = len(m.group(1))
            text = clean_inline(m.group(2))
            out.append("")
            if level <= 2:
                out.append("■ " + text)
                out.append("─" * min(40, max(8, len(text) * 2)))
            else:
                out.append(("  " * (level - 2)) + "◆ " + text)
            continue

        # 水平分割线
        if RE_HR.match(line):
            out.append("─" * 40)
            continue

        # 列表
        m = RE_BULLET.match(line)
        if m:
            indent = len(m.group(1))
            out.append(" " * indent + "• " + clean_inline(m.group(2)))
            continue

        # 普通段落 / 表格 / 引用
        out.append(clean_inline(line))
    return out


def clean_inline(s: str) -> str:
    s = RE_LINK.sub(lambda m: f"{m.group(1)}（{m.group(2)}）", s)
    s = RE_BOLD.sub(r"\1", s)
    s = RE_ITAL.sub(r"\1", s)
    s = RE_CODE_INLINE.sub(r"\1", s)
    if s.startswith(">"):
        s = "  ┃ " + s.lstrip(">").strip()
    return s
# ------------------------------------------------------------


def wrap_line(line: str, max_cols: int):
    if line == "":
        return [""]
    rows, cur, curw = [], [], 0
    for c in line:
        cw = 2 if ord(c) > 0x2E7F else 1
        if curw + cw > max_cols and cur:
            rows.append("".join(cur))
            cur, curw = [], 0
        cur.append(c)
        curw += cw
    if cur:
        rows.append("".join(cur))
    return rows


def main():
    ap = argparse.ArgumentParser(description="软著文档 PDF 生成（前30页+后30页）")
    ap.add_argument("--repo", default=REPO)
    ap.add_argument("--docs", nargs="*", default=DEFAULT_DOCS, help="文档相对路径列表（按顺序）")
    ap.add_argument("--name", default=SOFTWARE_NAME)
    ap.add_argument("--out", default=DEFAULT_OUT)
    ap.add_argument("--lines", type=int, default=LINES_PER_PAGE)
    ap.add_argument("--pages", type=int, default=PAGES_EACH)
    args = ap.parse_args()

    print("=" * 60)
    print("软著文档鉴别材料生成")
    print("=" * 60)

    # 读取并合并
    all_text = []
    used = []
    for rel in args.docs:
        fp = os.path.join(args.repo, rel)
        if not os.path.exists(fp):
            print(f"  [缺失] {rel}")
            continue
        with open(fp, "r", encoding="utf-8-sig", errors="replace") as f:
            raw = f.read().splitlines()
        title = os.path.splitext(os.path.basename(rel))[0]
        # 文档分节横幅
        all_text.append("")
        all_text.append("═" * 40)
        all_text.append("【文档】" + title)
        all_text.append("═" * 40)
        all_text.append("")
        all_text.extend(md_to_text(raw))
        used.append((rel, len(raw)))

    print(f"合并文档: {len(used)} 个")
    for rel, c in used:
        print(f"    {c:>5} 行  {rel}")
    print(f"转换后总行数: {len(all_text)} 行")

    # 初始化 PDF
    pdf = FPDF(orientation="P", unit="mm", format="A4")
    pdf.set_auto_page_break(auto=False)
    font_path = FONT_CJK if os.path.exists(FONT_CJK) else FONT_CJK_FALLBACK
    pdf.add_font("doc", "", font_path)
    pdf.set_font("doc", size=FONT_SIZE)

    PAGE_W, PAGE_H = 210.0, 297.0
    MARGIN_X, MARGIN_TOP, MARGIN_BOT = 15.0, 14.0, 14.0
    usable_w = PAGE_W - 2 * MARGIN_X
    usable_h = PAGE_H - MARGIN_TOP - MARGIN_BOT
    row_h = usable_h / args.lines
    one_col_w = pdf.get_string_width("0") or 2.0
    max_cols = max(20, int(usable_w / one_col_w))

    # 逻辑行 -> 视觉行
    visual = []
    for ln in all_text:
        visual.extend(wrap_line(ln, max_cols))
    total_rows = len(visual)
    total_pages = (total_rows + args.lines - 1) // args.lines
    print(f"换行后视觉行: {total_rows} 行  ->  共 {total_pages} 页（{args.lines} 行/页）")

    if total_pages <= args.pages * 2:
        selected = visual
        print(f"输出范围: 总页数 {total_pages} <= {args.pages*2}，全部输出")
    else:
        selected = visual[:args.pages * args.lines] + visual[-args.pages * args.lines:]
        print(f"输出范围: 前 {args.pages} 页 + 后 {args.pages} 页 = {args.pages*2} 页")

    out_total = (len(selected) + args.lines - 1) // args.lines
    cur_page = 0
    pdf.set_text_color(0, 0, 0)
    for i, row in enumerate(selected):
        col = i % args.lines
        if col == 0:
            pdf.add_page()
            cur_page = i // args.lines + 1
        pdf.set_xy(MARGIN_X, MARGIN_TOP + col * row_h)
        pdf.cell(usable_w, row_h, row, new_x="LMARGIN", new_y="TOP")
        if col == args.lines - 1 or i == len(selected) - 1:
            pdf.set_xy(MARGIN_X, PAGE_H - MARGIN_BOT + 3)
            pdf.set_font_size(7.5)
            pdf.cell(usable_w, 5,
                     f"{args.name}  {DOC_LABEL}    第 {cur_page} / {out_total} 页",
                     align="C")
            pdf.set_font_size(FONT_SIZE)

    os.makedirs(os.path.dirname(os.path.abspath(args.out)), exist_ok=True)
    pdf.output(args.out)
    print("-" * 60)
    print(f"[OK] 已生成: {args.out}  共 {out_total} 页")
    print("=" * 60)


if __name__ == "__main__":
    main()