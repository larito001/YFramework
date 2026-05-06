#!/usr/bin/env python3
"""Generate a UIPageBase-derived C# script from a UI design markdown.

Input  : Docs/UIPlans/UI/<View>.design.md (produced by UIDesignDocGenerator)
Output : Assets/Scripts/GamePlay/UI/<View>.cs by default

Element handling rules: see SKILL.md.
"""
import argparse
import re
import sys
from pathlib import Path


TITLE_RE = re.compile(r'^#\s*(.+?)\s*策划案\s*$', re.MULTILINE)
ELEMENT_HEAD_RE = re.compile(r'^###\s*(.+?)\s*[：:]\s*(.+?)\s*$', re.MULTILINE)
TYPE_RE = re.compile(r'^-\s*类型\s*[：:]\s*(.+?)\s*$', re.MULTILINE)
PATH_RE = re.compile(r'^-\s*路径\s*[：:]\s*(.+?)\s*$', re.MULTILINE)
COMMENT_RE = re.compile(r'^-\s*策划注释\s*[：:]\s*(.+?)\s*$', re.MULTILINE)
QUESTION_BLOCK_RE = re.compile(
    r'^####\s*(.+?)\s*\n+\s*<!--content-begin-->\s*\n(.*?)\n\s*<!--content-end-->',
    re.MULTILINE | re.DOTALL,
)
SECTION_SPLIT_RE = re.compile(r'(?m)^(?=###\s)')

PLACEHOLDERS = {'待填写。', '待填写', ''}
SUPPORTED = {'Button', 'Image', 'InputField', 'Slider', 'Text'}

_RANGE_RE = re.compile(r'(-?\d+(?:\.\d+)?)\s*(?:[,，\-到至~～]+|to)\s*(-?\d+(?:\.\d+)?)')


def is_placeholder(s: str) -> bool:
    return s.strip() in PLACEHOLDERS


def to_pascal(name: str) -> str:
    parts = re.split(r'[_\s\-]+', name)
    return ''.join(p[:1].upper() + p[1:] for p in parts if p)


def cs_string(s: str) -> str:
    return '"' + s.replace('\\', r'\\').replace('"', r'\"').replace('\n', r'\n').replace('\r', '') + '"'


def parse_design(md: str):
    title = TITLE_RE.search(md)
    view_id = title.group(1).strip() if title else 'UnnamedView'
    elements = []
    for sec in SECTION_SPLIT_RE.split(md):
        head = ELEMENT_HEAD_RE.match(sec)
        if not head:
            continue
        type_m = TYPE_RE.search(sec)
        if not type_m:
            continue
        eid = head.group(1).strip()
        path_m = PATH_RE.search(sec)
        comment_m = COMMENT_RE.search(sec)
        comment = comment_m.group(1).strip() if comment_m else ''
        questions = {}
        for q in QUESTION_BLOCK_RE.finditer(sec):
            qname = q.group(1).strip()
            qval = q.group(2).strip()
            if not is_placeholder(qval):
                questions[qname] = qval
        elements.append({
            'id': eid,
            'type': type_m.group(1).strip(),
            'path': path_m.group(1).strip() if path_m else '',
            'comment': '' if is_placeholder(comment) else comment,
            'questions': questions,
        })
    return {'view_id': view_id.strip(), 'elements': elements}


# ---------- helpers shared between generators ----------

def _validation_lines(validate_text, value_var='value', indent='        '):
    """Convert 合法性校验 文本 into C# body lines (with indent prefix)."""
    if not validate_text:
        return []
    lines = [f'{indent}// 合法性校验：{validate_text}']
    low = validate_text.lower()

    if '邮箱' in validate_text or 'email' in low or 'mail' in low:
        lines.append(f'{indent}bool isValid = System.Text.RegularExpressions.Regex.IsMatch({value_var}, @"^[^@\\s]+@[^@\\s]+\\.[^@\\s]+$");')
        lines.append(f'{indent}// TODO: 根据 isValid 处理提示/状态')
        return lines

    m = re.search(r'(?:大于等于|不少于|>=)\s*(\d+)', validate_text)
    if m:
        lines.append(f'{indent}bool isValid = {value_var}.Length >= {m.group(1)};')
        lines.append(f'{indent}// TODO: 根据 isValid 处理提示/状态')
        return lines

    m = re.search(r'(?:大于|超过|至少|多于|>)\s*(\d+)', validate_text)
    if m:
        lines.append(f'{indent}bool isValid = {value_var}.Length > {m.group(1)};')
        lines.append(f'{indent}// TODO: 根据 isValid 处理提示/状态')
        return lines

    m = re.search(r'(?:小于等于|不超过|至多|最多|<=)\s*(\d+)', validate_text)
    if m:
        lines.append(f'{indent}bool isValid = {value_var}.Length <= {m.group(1)};')
        lines.append(f'{indent}// TODO: 根据 isValid 处理提示/状态')
        return lines

    m = re.search(r'(?:小于|少于|<)\s*(\d+)', validate_text)
    if m:
        lines.append(f'{indent}bool isValid = {value_var}.Length < {m.group(1)};')
        lines.append(f'{indent}// TODO: 根据 isValid 处理提示/状态')
        return lines

    if '数字' in validate_text or 'number' in low or 'digit' in low:
        lines.append(f'{indent}bool isValid = System.Text.RegularExpressions.Regex.IsMatch({value_var}, @"^-?\\d+(?:\\.\\d+)?$");')
        lines.append(f'{indent}// TODO: 根据 isValid 处理提示/状态')
        return lines

    return lines


_KEYWORD_TO_FRAGMENTS = {
    '账号': ['account', 'user', 'login', 'name'],
    '用户': ['user', 'account'],
    '密码': ['password', 'pwd'],
    '邮箱': ['email', 'mail'],
    '手机': ['phone', 'mobile', 'tel'],
}


def _find_focus_target(submit_text, current_fid, all_input_ids):
    """Try to map a 提交或失焦行为 description to another InputField id in this view."""
    others = [x for x in all_input_ids if x != current_fid]
    if not others:
        return None
    text_lower = submit_text.lower()

    # 1) Direct token match: another id's name fragment appears verbatim in submit text.
    for other in others:
        for tok in re.split(r'[_\s\-]+', other.lower()):
            if len(tok) >= 3 and tok in text_lower:
                return other

    # 2) Chinese keyword fallback (账号 → account, 密码 → password, ...).
    for kw, fragments in _KEYWORD_TO_FRAGMENTS.items():
        if kw not in submit_text:
            continue
        for other in others:
            ol = other.lower()
            if any(f in ol for f in fragments):
                return other

    # 3) If there's only one peer InputField, default to it.
    if len(others) == 1:
        return others[0]
    return None


def _content_type_for_input(fid, validate_text, meaning_text):
    name_low = fid.lower()
    body = (validate_text or '') + (meaning_text or '')
    body_low = body.lower()
    if 'password' in name_low or 'pwd' in name_low or '密码' in body:
        return 'InputField.ContentType.Password'
    if '邮箱' in body or 'email' in body_low or 'mail' in body_low:
        return 'InputField.ContentType.EmailAddress'
    if '手机' in body or 'phone' in body_low or 'mobile' in body_low:
        return 'InputField.ContentType.IntegerNumber'
    if '数字' in body or 'number' in body_low or 'digit' in body_low:
        return 'InputField.ContentType.IntegerNumber'
    return None


# ---------- per-element generators ----------

def gen_image(e):
    fid = e['id']
    on_load = []
    res = e['questions'].get('资源命名', '').strip()
    if res:
        on_load.append(f'        {fid}.sprite = Resources.Load<Sprite>({cs_string(res)});')
    return {
        'fields': [f'    public Image {fid};'],
        'on_load': on_load,
        'on_show': [],
        'methods': [],
    }


def gen_button(e):
    fid = e['id']
    method = f'On{to_pascal(fid)}Click'
    behavior = e['questions'].get('点击后行为', '').strip()
    label = e['questions'].get('按钮文本', '').strip()

    if behavior and any(k in behavior for k in ['关闭', '退出']):
        body = '        CloseSelf();'
    elif behavior and re.search(r'[A-Z][A-Za-z0-9_]*Panel', behavior):
        m = re.search(r'([A-Z][A-Za-z0-9_]*Panel)', behavior)
        body = f'        Show<{m.group(1)}>();'
    elif behavior:
        body = f'        // TODO: {behavior}'
    else:
        body = '        // TODO: 实现点击行为'

    on_load = []
    if label:
        on_load.append(
            '        {\n'
            f'            var label = {fid}.GetComponentInChildren<Text>(true);\n'
            f'            if (label != null) label.text = {cs_string(label)};\n'
            '        }'
        )
    on_load.append(f'        {fid}.onClick.AddListener({method});')

    return {
        'fields': [f'    public Button {fid};'],
        'on_load': on_load,
        'on_show': [],
        'methods': [f'    private void {method}()\n    {{\n{body}\n    }}'],
    }


def gen_input(e, all_input_ids):
    fid = e['id']
    pascal = to_pascal(fid)
    on_changed = f'On{pascal}Changed'

    placeholder = e['questions'].get('默认占位文案', '').strip()
    validate = e['questions'].get('合法性校验', '').strip()
    submit = e['questions'].get('提交或失焦行为', '').strip()
    meaning = e['questions'].get('输入内容含义', '').strip()

    on_load = []

    content_type = _content_type_for_input(fid, validate, meaning)
    if content_type:
        on_load.append(f'        {fid}.contentType = {content_type};')

    if placeholder:
        on_load.append(
            '        {\n'
            f'            var ph = {fid}.placeholder as Text;\n'
            f'            if (ph != null) ph.text = {cs_string(placeholder)};\n'
            '        }'
        )

    on_load.append(f'        {fid}.onValueChanged.AddListener({on_changed});')

    methods = []
    val_lines = _validation_lines(validate, 'value')
    body = '\n'.join(val_lines) if val_lines else ''
    methods.append(
        f'    private void {on_changed}(string value)\n'
        f'    {{\n'
        f'{body}{chr(10) if body else ""}'
        f'    }}'
    )

    if submit:
        on_end = f'On{pascal}EndEdit'
        on_load.append(f'        {fid}.onEndEdit.AddListener({on_end});')

        body_lines = []
        target = _find_focus_target(submit, fid, all_input_ids) if ('切换' in submit or '焦' in submit or 'tab' in submit.lower() or '下一' in submit) else None
        if target:
            body_lines.append(f'        // {submit}')
            body_lines.append(f'        {target}.Select();')
            body_lines.append(f'        {target}.ActivateInputField();')
        else:
            body_lines.append(f'        // TODO: {submit}')

        methods.append(
            f'    private void {on_end}(string value)\n'
            f'    {{\n'
            + '\n'.join(body_lines) + '\n'
            f'    }}'
        )

    return {
        'fields': [f'    public InputField {fid};'],
        'on_load': on_load,
        'on_show': [],
        'methods': methods,
    }


def gen_slider(e):
    fid = e['id']
    pascal = to_pascal(fid)
    on_changed = f'On{pascal}Changed'
    on_load = []

    rng = e['questions'].get('最小值与最大值', '').strip()
    if rng:
        m = _RANGE_RE.search(rng)
        if m:
            on_load.append(f'        {fid}.minValue = {m.group(1)}f;')
            on_load.append(f'        {fid}.maxValue = {m.group(2)}f;')

    on_load.append(f'        {fid}.onValueChanged.AddListener({on_changed});')

    behavior = e['questions'].get('拖动后行为', '').strip()
    body = f'        // TODO: {behavior}' if behavior else f'        // TODO: 处理 {fid} 数值变化'

    return {
        'fields': [f'    public Slider {fid};'],
        'on_load': on_load,
        'on_show': [],
        'methods': [f'    private void {on_changed}(float value)\n    {{\n{body}\n    }}'],
    }


def gen_text(e):
    fid = e['id']
    default = e['questions'].get('默认文案', '').strip()
    on_show = []
    if default:
        on_show.append(f'        {fid}.text = {cs_string(default)};')
    return {
        'fields': [f'    public Text {fid};'],
        'on_load': [],
        'on_show': on_show,
        'methods': [],
    }


# ---------- assembly ----------

def generate_code(view):
    cls = view['view_id']
    input_ids = [e['id'] for e in view['elements'] if e['type'] == 'InputField']

    fields, on_loads, on_shows, methods = [], [], [], []
    skipped = []
    for e in view['elements']:
        t = e['type']
        if t == 'Button':
            r = gen_button(e)
        elif t == 'Image':
            r = gen_image(e)
        elif t == 'InputField':
            r = gen_input(e, input_ids)
        elif t == 'Slider':
            r = gen_slider(e)
        elif t == 'Text':
            r = gen_text(e)
        else:
            skipped.append(e)
            continue
        fields.extend(r['fields'])
        on_loads.extend(r['on_load'])
        on_shows.extend(r['on_show'])
        methods.extend(r['methods'])

    out = []
    out.append('using UnityEngine;')
    out.append('using UnityEngine.UI;')
    out.append('')
    out.append(f'public class {cls} : UIPageBase')
    out.append('{')
    if fields:
        out.extend(fields)
    for s in skipped:
        out.append(f'    // {s["type"]} 暂未生成代码: {s["id"]} (path: {s["path"]})')
    out.append('')
    out.append('    public override void OnLoad()')
    out.append('    {')
    out.extend(on_loads)
    out.append('    }')
    out.append('')
    out.append('    public override void OnShow()')
    out.append('    {')
    out.extend(on_shows)
    out.append('    }')
    out.append('')
    out.append('    public override void OnHide()')
    out.append('    {')
    out.append('    }')
    out.append('')
    out.append('    public override void OnResize()')
    out.append('    {')
    out.append('    }')
    if methods:
        out.append('')
        out.append('\n\n'.join(methods))
    out.append('}')
    out.append('')
    return '\n'.join(out)


def main():
    p = argparse.ArgumentParser()
    p.add_argument('--design', required=True, help='Path to <View>.design.md')
    p.add_argument('--out', help='Output cs path (default: Assets/Scripts/GamePlay/UI/<ViewId>.cs)')
    p.add_argument('--force', action='store_true', help='Overwrite existing cs')
    p.add_argument('--script-root', default='Assets/Scripts/GamePlay/UI')
    args = p.parse_args()

    md_path = Path(args.design)
    if not md_path.is_file():
        print(f'ERROR: design file not found: {md_path}', file=sys.stderr)
        return 2
    md = md_path.read_text(encoding='utf-8-sig')
    view = parse_design(md)
    if not view['elements']:
        print('WARNING: 解析到 0 个元素，请检查策划案格式（### 元素ID：名称 / - 类型：X / - 路径：X）', file=sys.stderr)

    out_path = Path(args.out) if args.out else Path(args.script_root) / f'{view["view_id"]}.cs'
    if out_path.exists() and not args.force:
        print(f'ERROR: {out_path} already exists. Use --force to overwrite.', file=sys.stderr)
        return 3
    out_path.parent.mkdir(parents=True, exist_ok=True)
    out_path.write_text(generate_code(view), encoding='utf-8')

    print(f'Generated: {out_path}')
    print(f'View ID  : {view["view_id"]}')
    print(f'Elements : {len(view["elements"])}')
    counts = {}
    for e in view['elements']:
        counts[e['type']] = counts.get(e['type'], 0) + 1
    for t, c in sorted(counts.items()):
        flag = '' if t in SUPPORTED or t == 'ScrollView' else ' (未支持类型)'
        print(f'  - {t}: {c}{flag}')
    return 0


if __name__ == '__main__':
    sys.exit(main())
