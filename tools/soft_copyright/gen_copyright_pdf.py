# -*- coding: utf-8 -*-
"""
软件著作权登记 - 源程序材料生成工具
===================================================
按软著要求，从项目源代码中提取「前连续 30 页 + 后连续 30 页」（共 60 页，
每页 50 行），输出为 PDF。若源代码总页数不足 60 页，则全部输出。

特性：
  - 仅收录自研代码（.cs），自动排除第三方目录 / Plugins。
  - 自动检测是否含中文：纯英文用 Consolas 等宽字体；含中文用中文字体。
  - 长行自动换行，并按「视觉行」计入 50 行/页（不丢任何代码）。
  - 页脚带页码与软件名称。

用法：
  python gen_copyright_pdf.py                # 用下方默认配置
  python gen_copyright_pdf.py --name "我的软件 V1.0" --out out.pdf

依赖：pip install fpdf2
"""

import os
import sys
import argparse
from fpdf import FPDF

# Windows 控制台默认 GBK，强制改为 UTF-8 避免中文/符号报错
try:
    sys.stdout.reconfigure(encoding="utf-8")
    sys.stderr.reconfigure(encoding="utf-8")
except Exception:
    pass

# ============================ 默认配置 ============================
# 源代码根目录（可多个）
DEFAULT_SRC_ROOTS = [
    r"C:\UnityProject\YFramework\client\Assets\Scripts",
]
# 收录的文件扩展名
SOURCE_EXTS = (".cs",)
# 排除目录关键字（路径中包含任一即跳过）——第三方 / 自动生成
EXCLUDE_KEYWORDS = [
    "com.arongranberg.astar",   # A* Pathfinding Project 第三方库
    os.sep + "Plugins" + os.sep,
    os.sep + "ThirdParty" + os.sep,
    ".designer.cs",
]
LINES_PER_PAGE = 50      # 每页行数（软著标准 50 行/页）
PAGES_EACH = 30          # 前 N 页 + 后 N 页
SOFTWARE_NAME = "YFramework 游戏客户端软件"   # 页脚显示名，可用 --name 覆盖
DEFAULT_OUT = r"C:\UnityProject\YFramework\软著源程序_前30后30.pdf"

# 字体（Windows 自带）
FONT_MONO = r"C:\Windows\Fonts\consola.ttf"     # Consolas 等宽（纯英文）
FONT_CJK = r"C:\Windows\Fonts\Deng.ttf"          # 等线（含中文+拉丁）
FONT_CJK_FALLBACK = r"C:\Windows\Fonts\simsun.ttc"  # 宋体兜底

# 排版参数
FONT_SIZE = 9.0          # pt
TAB_WIDTH = 4
# ================================================================


def is_excluded(path: str) -> bool:
    p = path.replace("/", os.sep).lower()
    return any(k.lower() in p for k in EXCLUDE_KEYWORDS)


def collect_files(roots):
    """按相对路径稳定排序收集源文件。"""
    files = []
    for root in roots:
        root = os.path.abspath(root)
        for dirpath, _dirs, names in os.walk(root):
            for n in names:
                if n.lower().endswith(SOURCE_EXTS):
                    full = os.path.join(dirpath, n)
                    if not is_excluded(full):
                        files.append(full)
    files.sort(key=lambda p: p.replace("\\", "/").lower())
    return files


def read_lines(path):
    """读取文件为行列表（去掉行尾换行，制表符展开为空格）。"""
    try:
        with open(path, "r", encoding="utf-8-sig", errors="replace") as f:
            text = f.read()
    except Exception as e:
        print(f"  [跳过] {path}: {e}")
        return []
    out = []
    for ln in text.splitlines():
        out.append(ln.replace("\t", " " * TAB_WIDTH).rstrip())
    return out


def has_cjk(s: str) -> bool:
    return any(ord(c) > 0x2E7F for c in s)


def disp_cols(s: str) -> int:
    """显示列宽：CJK 记 2 列，其余 1 列。"""
    w = 0
    for c in s:
        w += 2 if ord(c) > 0x2E7F else 1
    return w


def wrap_line(line: str, max_cols: int):
    """按显示列宽换行，返回视觉行列表（保持原代码不丢字符）。"""
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
    ap = argparse.ArgumentParser(description="软著源程序 PDF 生成（前30页+后30页）")
    ap.add_argument("--roots", nargs="*", default=DEFAULT_SRC_ROOTS, help="源代码根目录")
    ap.add_argument("--name", default=SOFTWARE_NAME, help="软件名称（页脚显示）")
    ap.add_argument("--out", default=DEFAULT_OUT, help="输出 PDF 路径")
    ap.add_argument("--lines", type=int, default=LINES_PER_PAGE, help="每页行数")
    ap.add_argument("--pages", type=int, default=PAGES_EACH, help="前/后各取页数")
    ap.add_argument("--listfile", default="", help="另存收录文件清单到此路径")
    args = ap.parse_args()

    print("=" * 60)
    print("软著源程序材料生成")
    print("=" * 60)

    files = collect_files(args.roots)
    print(f"收录源文件: {len(files)} 个（已排除第三方/Plugins）")
    if not files:
        print("未找到任何源文件，请检查 --roots。")
        sys.exit(1)

    # 拼接所有逻辑行
    all_lines = []
    file_summary = []
    for fp in files:
        ls = read_lines(fp)
        all_lines.extend(ls)
        file_summary.append((fp, len(ls)))

    cjk = any(has_cjk(x) for x in all_lines)
    print(f"代码总行数: {len(all_lines)} 行")
    print(f"含中文字符: {'是 -> 使用中文字体' if cjk else '否 -> 使用 Consolas 等宽字体'}")

    if args.listfile:
        with open(args.listfile, "w", encoding="utf-8") as f:
            for fp, c in file_summary:
                f.write(f"{c:>6}  {fp}\n")
        print(f"文件清单已保存: {args.listfile}")

    # 选字体并初始化 PDF（A4）
    pdf = FPDF(orientation="P", unit="mm", format="A4")
    pdf.set_auto_page_break(auto=False)

    if cjk:
        font_path = FONT_CJK if os.path.exists(FONT_CJK) else FONT_CJK_FALLBACK
        pdf.add_font("code", "", font_path)
        font_name = "code"
        mono = False
    else:
        pdf.add_font("code", "", FONT_MONO)
        font_name = "code"
        mono = True
    pdf.set_font(font_name, size=FONT_SIZE)

    # 版面计算
    PAGE_W, PAGE_H = 210.0, 297.0
    MARGIN_X, MARGIN_TOP, MARGIN_BOT = 12.0, 12.0, 14.0
    usable_w = PAGE_W - 2 * MARGIN_X
    usable_h = PAGE_H - MARGIN_TOP - MARGIN_BOT
    row_h = usable_h / args.lines
    # 单个 ASCII 字符宽度（用于计算每行最大列数）
    one_col_w = pdf.get_string_width("0") or 1.9
    max_cols = max(20, int(usable_w / one_col_w))

    # 逻辑行 -> 视觉行
    visual = []
    for ln in all_lines:
        visual.extend(wrap_line(ln, max_cols))
    total_rows = len(visual)
    total_pages = (total_rows + args.lines - 1) // args.lines
    print(f"换行后视觉行: {total_rows} 行  ->  共 {total_pages} 页（{args.lines} 行/页）")

    # 选取前 N 页 + 后 N 页
    front_rows = args.pages * args.lines
    back_rows = args.pages * args.lines
    if total_pages <= args.pages * 2:
        selected = visual
        note = f"总页数 {total_pages} <= {args.pages*2}，全部输出"
    else:
        selected = visual[:front_rows] + visual[-back_rows:]
        note = f"前 {args.pages} 页 + 后 {args.pages} 页 = {args.pages*2} 页"
    print(f"输出范围: {note}")

    # 渲染
    out_total_pages = (len(selected) + args.lines - 1) // args.lines
    pdf.set_text_color(0, 0, 0)
    for i, row in enumerate(selected):
        col = i % args.lines
        if col == 0:
            pdf.add_page()
            cur_page = i // args.lines + 1
        y = MARGIN_TOP + col * row_h
        pdf.set_xy(MARGIN_X, y)
        # 用 cell 输出单行（不自动换行，已预先 wrap）
        pdf.cell(usable_w, row_h, row, new_x="LMARGIN", new_y="TOP")
        # 页脚
        if col == args.lines - 1 or i == len(selected) - 1:
            pdf.set_xy(MARGIN_X, PAGE_H - MARGIN_BOT + 2)
            pdf.set_font_size(7.5)
            footer = f"{args.name}    第 {cur_page} / {out_total_pages} 页"
            pdf.cell(usable_w, 5, footer, align="C")
            pdf.set_font_size(FONT_SIZE)

    os.makedirs(os.path.dirname(os.path.abspath(args.out)), exist_ok=True)
    pdf.output(args.out)
    print("-" * 60)
    print(f"✔ 已生成: {args.out}")
    print(f"  共 {out_total_pages} 页")
    print("=" * 60)


if __name__ == "__main__":
    main()