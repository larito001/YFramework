'use strict';

// ================= 全局状态 =================

const state = {
    trees: {},          // dir → { files: [...] }
    gitDiff: null,      // { modified, staged, untracked, all }
    selected: null,     // { dir, path, content?, frontmatter? }
    autoRefresh: true,
    refreshTimer: null,
};

const DIRS = ['策划案', '代码规划', '配表规划', '代码优化规划'];

// ================= 工具 =================

function $(sel, root = document) { return root.querySelector(sel); }
function $$(sel, root = document) { return Array.from(root.querySelectorAll(sel)); }

function showToast(msg, type = 'info') {
    const el = $('#toast');
    el.textContent = msg;
    el.classList.toggle('error', type === 'error');
    el.classList.remove('hidden');
    clearTimeout(showToast._timer);
    showToast._timer = setTimeout(() => el.classList.add('hidden'), 3000);
}

async function api(path, opts = {}) {
    const res = await fetch(path, opts);
    if (!res.ok) {
        const err = await res.json().catch(() => ({ error: res.statusText }));
        throw new Error(err.error || 'API error');
    }
    return res.json();
}

function escapeHtml(s) {
    if (s == null) return '';
    return String(s).replace(/[&<>"']/g, c => ({
        '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'
    }[c]));
}

function formatDate(iso) {
    if (!iso) return '';
    const d = new Date(iso);
    const now = Date.now();
    const diff = now - d.getTime();
    if (diff < 60_000) return '刚刚';
    if (diff < 3600_000) return `${Math.floor(diff / 60_000)} 分钟前`;
    if (diff < 86400_000) return `${Math.floor(diff / 3600_000)} 小时前`;
    return d.toISOString().slice(0, 10);
}

// ================= 加载与渲染 =================

async function loadTrees() {
    await Promise.all([
        ...DIRS.map(async dir => {
            try {
                state.trees[dir] = await api(`/api/tree?dir=${encodeURIComponent(dir)}`);
            } catch (e) {
                console.warn(`加载 ${dir} 失败:`, e);
                state.trees[dir] = { dir, files: [] };
            }
        }),
        (async () => {
            try {
                state.gitDiff = await api('/api/git/diff');
            } catch (e) {
                console.warn('加载 git diff 失败:', e);
                state.gitDiff = { modified: [], staged: [], untracked: [], all: [] };
            }
        })(),
    ]);
    renderSidebar();
}

function renderSidebar() {
    for (const dir of DIRS) {
        const section = document.querySelector(`.folder-section[data-dir="${dir}"]`);
        if (!section) continue;
        const list = section.querySelector('[data-list]');
        const count = section.querySelector('[data-count]');
        const files = (state.trees[dir]?.files) || [];
        count.textContent = files.length;
        list.innerHTML = '';
        if (files.length === 0) {
            list.innerHTML = `<li style="color: var(--fg-muted); font-size: 11px; padding-left: 32px;">（无文档）</li>`;
            continue;
        }
        for (const f of files) {
            const li = document.createElement('li');
            const fm = f.frontmatter || {};
            const id = fm.id || f.name.replace(/\.md$/, '');
            const title = fm.title || f.name;
            const status = fm.status || '';
            const statusBadge = status ? `<span class="status-badge status-${status}">${status}</span>` : '';
            li.innerHTML = `
                <div class="file-title" title="${escapeHtml(title)}">${escapeHtml(title)}</div>
                <div class="file-meta">
                    <span class="file-id">${escapeHtml(id)}</span>
                    ${statusBadge}
                    <span>${formatDate(f.mtime)}</span>
                </div>
            `;
            li.addEventListener('click', () => selectFile(dir, f.path));
            if (state.selected && state.selected.dir === dir && state.selected.path === f.path) {
                li.classList.add('active');
            }
            list.appendChild(li);
        }
    }

    // git diff
    const gitSection = document.querySelector('.folder-section[data-dir="git"]');
    const gitList = gitSection.querySelector('[data-list]');
    const gitCount = gitSection.querySelector('[data-count]');
    const gd = state.gitDiff || { all: [] };
    gitCount.textContent = gd.all?.length || 0;
    gitList.innerHTML = '';
    if (!gd.all || gd.all.length === 0) {
        gitList.innerHTML = `<li style="color: var(--fg-muted); font-size: 11px; padding-left: 32px;">（无未提交改动）</li>`;
    } else {
        // 用单个虚拟条目表示"评审整批 diff"
        const li = document.createElement('li');
        li.innerHTML = `
            <div class="file-title">📋 评审本批改动（${gd.all.length} 个文件）</div>
            <div class="file-meta"><span>点击右侧详情查看 / 启动 /code-review</span></div>
        `;
        li.addEventListener('click', () => selectGitDiff());
        if (state.selected && state.selected.dir === 'git') {
            li.classList.add('active');
        }
        gitList.appendChild(li);
    }
}

async function selectFile(dir, filePath) {
    try {
        const data = await api(`/api/file?dir=${encodeURIComponent(dir)}&file=${encodeURIComponent(filePath)}`);
        state.selected = {
            dir,
            path: filePath,
            content: data.content,
            frontmatter: parseFrontmatterClient(data.content),
        };
        renderSidebar();
        renderPreview();
    } catch (e) {
        showToast('加载文档失败：' + e.message, 'error');
    }
}

function selectGitDiff() {
    state.selected = { dir: 'git', path: null };
    renderSidebar();
    renderPreview();
}

function parseFrontmatterClient(content) {
    const m = /^---\r?\n([\s\S]*?)\r?\n---/.exec(content || '');
    if (!m) return null;
    const fm = {};
    // 缩进的子键扁平化到顶层（如 links: 块下的 source / excel_plan / plan / excel）
    for (const raw of m[1].split(/\r?\n/)) {
        const kv = /^\s*([a-zA-Z_][\w-]*):\s*(.*)$/.exec(raw);
        if (!kv) continue;
        let v = kv[2].trim();
        // 剥行尾注释（yaml 行尾 # 开始的注释）
        const hashIdx = v.indexOf(' #');
        if (hashIdx >= 0) v = v.slice(0, hashIdx).trim();
        if ((v.startsWith('"') && v.endsWith('"')) || (v.startsWith("'") && v.endsWith("'"))) {
            v = v.slice(1, -1);
        }
        // 空值（如 `links:` 行）跳过
        if (v === '' && kv[1] === 'links') continue;
        fm[kv[1]] = v;
    }
    return fm;
}

function stripFrontmatter(content) {
    return (content || '').replace(/^---\r?\n[\s\S]*?\r?\n---\r?\n?/, '');
}

function renderPreview() {
    const sel = state.selected;
    const pathEl = $('#preview-path');
    const statusEl = $('#preview-status');
    const actionsEl = $('#preview-actions');
    const bodyEl = $('#preview-body');

    if (!sel) {
        pathEl.textContent = '从左侧选择文档预览';
        statusEl.innerHTML = '';
        actionsEl.innerHTML = '';
        return;
    }

    if (sel.dir === 'git') {
        const gd = state.gitDiff || { modified: [], staged: [], untracked: [], all: [] };
        pathEl.textContent = `未提交改动（${gd.all.length} 个文件）`;
        statusEl.innerHTML = `
            <span>修改: ${gd.modified.length}</span>
            <span>已暂存: ${gd.staged.length}</span>
            <span>未跟踪: ${gd.untracked.length}</span>
        `;
        actionsEl.innerHTML = '';
        const reviewBtn = document.createElement('button');
        reviewBtn.className = 'primary';
        reviewBtn.textContent = '🔍 启动 /code-review --diff';
        reviewBtn.addEventListener('click', () => {
            launchClaude({
                title: 'Claude — 代码评审 (diff)',
                prompt: '/code-review --diff',
            });
        });
        actionsEl.appendChild(reviewBtn);

        const items = [];
        for (const f of gd.staged) items.push({ tag: 'staged', label: '已暂存', path: f });
        for (const f of gd.modified) {
            if (!gd.staged.includes(f)) items.push({ tag: 'modified', label: '修改', path: f });
        }
        for (const f of gd.untracked) items.push({ tag: 'untracked', label: '新增', path: f });

        bodyEl.innerHTML = `
            <h2 style="margin-bottom: 12px;">本批改动文件清单</h2>
            <p style="color: var(--fg-dim); margin-bottom: 16px;">
                启动后，code-review skill 会针对这些改动做多维度扫描，结论输出到 <code>代码优化规划/</code>，
                若无问题则直接口头答复。
            </p>
            ${items.length === 0
                ? '<div class="empty-state"><div class="empty-text">暂无未提交改动。</div></div>'
                : `<ul class="git-diff-list">${items.map(i => `
                    <li>
                        <span class="diff-tag ${i.tag}">${i.label}</span>
                        <span>${escapeHtml(i.path)}</span>
                    </li>`).join('')}</ul>`
            }
        `;
        return;
    }

    // 文档预览
    const fm = sel.frontmatter || {};
    pathEl.textContent = `${sel.dir}/${sel.path}`;
    const id = fm.id || sel.path.replace(/\.md$/, '');
    const status = fm.status || '—';
    const statusBadge = `<span class="status-badge status-${status}">${status}</span>`;

    const metaParts = [`<span><b>${escapeHtml(id)}</b></span>`, statusBadge];
    if (fm.owner) metaParts.push(`<span>owner: ${escapeHtml(fm.owner)}</span>`);
    if (fm.updated) metaParts.push(`<span>更新: ${escapeHtml(fm.updated)}</span>`);
    if (fm.source) metaParts.push(`<span>← source: <code>${escapeHtml(fm.source)}</code></span>`);
    statusEl.innerHTML = metaParts.join('');

    actionsEl.innerHTML = '';
    renderActions(sel, fm).forEach(b => actionsEl.appendChild(b));

    const md = stripFrontmatter(sel.content);
    const fmBlock = renderFrontmatterCard(fm);
    bodyEl.innerHTML = `${fmBlock}<div class="md">${marked.parse(md)}</div>`;
}

function renderFrontmatterCard(fm) {
    if (!fm || Object.keys(fm).length === 0) return '';
    const keys = ['id', 'title', 'type', 'status', 'owner', 'reviewers', 'created', 'updated', 'version',
                  'source', 'plan', 'excel_plan', 'excel', 'scope'];
    const rows = keys
        .filter(k => fm[k])
        .map(k => `<dt>${k}</dt><dd>${escapeHtml(fm[k])}</dd>`)
        .join('');
    return `<dl class="frontmatter-card">${rows}</dl>`;
}

// ================= 按钮：上下文相关 =================

function renderActions(sel, fm) {
    const buttons = [];

    if (sel.dir === '策划案') {
        const id = fm.id || sel.path.replace(/\.md$/, '');
        const status = fm.status;

        // 继续/修改
        buttons.push(makeBtn('✏ 继续修改', '', () => openModal({
            title: '基于现有需求继续修改',
            hint: `要修改 <code>${escapeHtml(id)}</code> 的哪部分？描述会作为 /requirement-analysis 的初始指令。`,
            placeholder: '例如：补充验收标准的异常分支；加上一个连击系统；……',
            confirmLabel: '启动 Claude',
            onConfirm: text => {
                if (!text.trim()) return showToast('请填写要修改的内容', 'error');
                launchClaude({
                    title: `Claude — 修改 ${id}`,
                    prompt: `/requirement-analysis 基于 ${id} 继续修改：${text.trim()}`,
                });
            },
        })));

        // 通过审查
        if (status === 'Draft' || status === 'Review') {
            buttons.push(makeBtn('✓ 通过审查', 'ok', async () => {
                if (!confirm(`确认把 ${id} 状态设为 Approved？`)) return;
                try {
                    await api(`/api/file/status?dir=${encodeURIComponent(sel.dir)}&file=${encodeURIComponent(sel.path)}`, {
                        method: 'PUT',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify({ status: 'Approved' }),
                    });
                    showToast(`${id} 已标记为 Approved`);
                    await loadTrees();
                    await selectFile(sel.dir, sel.path);
                } catch (e) {
                    showToast('更新失败：' + e.message, 'error');
                }
            }));
        }

        // 打回
        if (status === 'Approved' || status === 'Review') {
            buttons.push(makeBtn('✗ 打回', 'warn', () => openModal({
                title: '打回需求文档',
                hint: `打回理由会作为 /requirement-analysis 的初始指令；同时把 <code>${escapeHtml(id)}</code> 的状态回退到 Draft。`,
                placeholder: '例如：异常分支只列了 2 条，要求补足 3 条；数值缺单位……',
                confirmLabel: '打回并启动 Claude',
                onConfirm: async text => {
                    if (!text.trim()) return showToast('请填写打回理由', 'error');
                    try {
                        await api(`/api/file/status?dir=${encodeURIComponent(sel.dir)}&file=${encodeURIComponent(sel.path)}`, {
                            method: 'PUT',
                            headers: { 'Content-Type': 'application/json' },
                            body: JSON.stringify({ status: 'Draft' }),
                        });
                        launchClaude({
                            title: `Claude — 打回修改 ${id}`,
                            prompt: `/requirement-analysis 审查打回 ${id}，需修改：${text.trim()}`,
                        });
                        await loadTrees();
                        await selectFile(sel.dir, sel.path);
                    } catch (e) {
                        showToast('打回失败：' + e.message, 'error');
                    }
                },
            })));
        }

        // 模块分析（只在 Approved 后启用）
        const planBtn = makeBtn('⚙ 模块分析', 'primary', () => {
            launchClaude({
                title: `Claude — 模块分析 ${id}`,
                prompt: `/code-planning ${id}`,
            });
        });
        if (status !== 'Approved') {
            planBtn.disabled = true;
            planBtn.title = '需求需先通过审查（status=Approved）才能进入模块分析';
        }
        buttons.push(planBtn);
    }

    if (sel.dir === '代码规划') {
        const id = fm.id || sel.path.replace(/\.md$/, '');
        const sourceId = fm.source || '';
        const excelPlanId = fm.excel_plan || '';

        // 配表生成（吃 frontmatter.links.excel_plan 指向的配表规划）
        if (excelPlanId) {
            buttons.push(makeBtn('📊 生成配表', '', () => {
                launchClaude({
                    title: `Claude — 配表生成 ${excelPlanId}`,
                    prompt: `/excel-generation ${excelPlanId}`,
                });
            }));
        } else {
            const noExcelBtn = makeBtn('📊 生成配表', '', () => {});
            noExcelBtn.disabled = true;
            noExcelBtn.title = '本代码规划 frontmatter.links 无 excel_plan 字段（无 xlsx 变更，或规划版本未升级到含 §4.5 配表规划文档）。';
            buttons.push(noExcelBtn);
        }

        buttons.push(makeBtn('⚡ 生成代码', 'primary', () => {
            launchClaude({
                title: `Claude — 代码生成 ${id}`,
                prompt: `/code-generation ${id}`,
            });
        }));

        // 按规划生成预制体（吃 §6 资源清单与 Bootstrapper 注册路径）
        buttons.push(makeBtn('🎨 生成预制体', '', () => {
            launchClaude({
                title: `Claude — 预制体生成 ${id}`,
                prompt: `/prefab-generation ${id}`,
            });
        }));

        if (sourceId) {
            buttons.push(makeBtn('🔄 重新规划', '', () => {
                launchClaude({
                    title: `Claude — 重新规划 ${sourceId}`,
                    prompt: `/code-planning ${sourceId} 重新规划，参考现有 ${id} 的结论`,
                });
            }));
        }

        // 顺手对生成的代码做评审
        buttons.push(makeBtn('🔍 评审生成代码', '', () => openModal({
            title: '评审本规划生成的代码',
            hint: `给 /code-review 一个明确范围，例如对应模块路径或类名。`,
            placeholder: '例如：client/Assets/Scripts/GamePlay/Combat/  或  CombatManager',
            confirmLabel: '启动评审',
            onConfirm: text => {
                if (!text.trim()) return showToast('请填写评审范围', 'error');
                launchClaude({
                    title: `Claude — 评审 ${text.trim()}`,
                    prompt: `/code-review ${text.trim()}`,
                });
            },
        })));
    }

    if (sel.dir === '配表规划') {
        const id = fm.id || sel.path.replace(/\.md$/, '');
        const planId = fm.plan || '';

        buttons.push(makeBtn('📊 生成 xlsx', 'primary', () => {
            launchClaude({
                title: `Claude — 配表生成 ${id}`,
                prompt: `/excel-generation ${id}`,
            });
        }));

        buttons.push(makeBtn('🚀 发布配表', 'ok', async () => {
            if (!confirm('在新窗口跑 .\\发布配表.bat（全量重发所有 xlsx）？耗时几秒到十几秒。')) return;
            try {
                await api('/api/publish-excel', { method: 'POST' });
                showToast('已启动发布配表窗口（请在新 PowerShell 窗口查看输出）');
            } catch (e) {
                showToast('启动失败：' + e.message, 'error');
            }
        }));

        if (planId) {
            buttons.push(makeBtn('↩ 回到代码规划', '', () => {
                // 跳转到对应代码规划文档
                const planPath = sel.path.replace(/-Excel-v(\d+)\.md$/, '-Plan-v$1.md');
                selectFile('代码规划', planPath).catch(() => {
                    showToast(`未找到 ${planPath}，可能 -v 不一致`, 'error');
                });
            }));
        }
    }

    if (sel.dir === '代码优化规划') {
        const id = fm.id || sel.path.replace(/\.md$/, '');
        // 优化报告暂只读，提供"按报告执行修复"的入口（启动一个新窗口让用户自己决定）
        buttons.push(makeBtn('🛠 按报告执行修复', '', () => {
            launchClaude({
                title: `Claude — 修复 ${id}`,
                prompt: `请阅读 代码优化规划/${state.selected.path} 中 §5 优化执行计划，逐条按 Critical → Major → Minor 顺序在代码中实施修复。每修一条做一次自检，全部完成后总结。`,
            });
        }));
    }

    return buttons;
}

function makeBtn(label, cls, handler) {
    const btn = document.createElement('button');
    if (cls) btn.className = cls;
    btn.textContent = label;
    btn.addEventListener('click', handler);
    return btn;
}

// ================= 启动 Claude 窗口 =================

async function launchClaude({ prompt, title }) {
    try {
        await api('/api/launch', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ prompt, title }),
        });
        showToast(`已启动新 Claude 窗口：${title}`);
    } catch (e) {
        showToast('启动失败：' + e.message, 'error');
    }
}

// ================= Modal =================

function openModal({ title, hint, placeholder, confirmLabel, onConfirm }) {
    const modal = $('#modal');
    $('#modal-title').textContent = title;
    $('#modal-hint').innerHTML = hint || '';
    const ta = $('#modal-input');
    ta.value = '';
    ta.placeholder = placeholder || '';
    $('#modal-confirm').textContent = confirmLabel || '确认';
    modal.classList.remove('hidden');
    setTimeout(() => ta.focus(), 50);

    const close = () => modal.classList.add('hidden');
    const cancel = $('#modal-cancel');
    const confirm = $('#modal-confirm');
    const onCancel = () => {
        cleanup();
        close();
    };
    const onConfirmClick = () => {
        const text = ta.value;
        cleanup();
        close();
        Promise.resolve(onConfirm(text)).catch(e => showToast('错误：' + e.message, 'error'));
    };
    const onKey = e => {
        if (e.key === 'Escape') onCancel();
        if (e.key === 'Enter' && (e.metaKey || e.ctrlKey)) onConfirmClick();
    };
    function cleanup() {
        cancel.removeEventListener('click', onCancel);
        confirm.removeEventListener('click', onConfirmClick);
        document.removeEventListener('keydown', onKey);
    }
    cancel.addEventListener('click', onCancel);
    confirm.addEventListener('click', onConfirmClick);
    document.addEventListener('keydown', onKey);
}

// ================= 顶部按钮 =================

function bindHeaderActions() {
    $('#btn-new-req').addEventListener('click', () => {
        openModal({
            title: '新建需求',
            hint: '用一两句话描述你想做的功能。Claude 会进入 /requirement-analysis skill 与你迭代细化。',
            placeholder: '例如：做个战斗系统，玩家能近战和远程攻击，命中有飘字与震屏反馈。',
            confirmLabel: '启动 Claude',
            onConfirm: text => {
                if (!text.trim()) return showToast('请填写需求描述', 'error');
                launchClaude({
                    title: 'Claude — 新建需求',
                    prompt: `/requirement-analysis ${text.trim()}`,
                });
            },
        });
    });

    $('#btn-publish-excel').addEventListener('click', async () => {
        if (!confirm('在新窗口跑 .\\发布配表.bat（全量重发所有 excel/3xlsx/*.xlsx）？\n会刷新 ScriptGenerated/Config/*Config.cs + Resources/Config/Data/*.bytes + ScriptGenerated/Proto/*.cs。')) return;
        try {
            await api('/api/publish-excel', { method: 'POST' });
            showToast('已启动发布配表窗口（请在新 PowerShell 窗口查看输出）');
        } catch (e) {
            showToast('启动失败：' + e.message, 'error');
        }
    });

    $('#btn-review-any').addEventListener('click', () => {
        openModal({
            title: '评审任意范围',
            hint: '可填：文件路径 / 目录 / 模块名 / 类名 / "整个 GamePlay" / "--diff"。',
            placeholder: '例如：client/Assets/Scripts/GamePlay/Combat/',
            confirmLabel: '启动评审',
            onConfirm: text => {
                if (!text.trim()) return showToast('请填写评审范围', 'error');
                launchClaude({
                    title: `Claude — 评审 ${text.trim().slice(0, 40)}`,
                    prompt: `/code-review ${text.trim()}`,
                });
            },
        });
    });

    $('#btn-build-prefabs').addEventListener('click', () => {
        openModal({
            title: '生成预制体',
            hint: '可填：代码规划 id（推荐，从 §6 资源清单抽 prefab 列表）/ Panel 类名 / 类名清单 / <code>--diff</code>。生成后在 Unity 内点 [YFramework/Build Prefabs/[All]] 实际产出 .prefab。',
            placeholder: '例如：GP-Combat-Plan-v1   或   CombatPanel,CombatHUDPanel   或   --diff',
            confirmLabel: '启动生成',
            onConfirm: text => {
                if (!text.trim()) return showToast('请填写生成范围', 'error');
                launchClaude({
                    title: `Claude — 预制体 ${text.trim().slice(0, 40)}`,
                    prompt: `/prefab-generation ${text.trim()}`,
                });
            },
        });
    });

    $('#btn-refresh').addEventListener('click', () => {
        loadTrees().then(() => {
            if (state.selected && state.selected.dir !== 'git') {
                selectFile(state.selected.dir, state.selected.path);
            }
            showToast('已刷新');
        });
    });

    $('#chk-auto-refresh').addEventListener('change', e => {
        state.autoRefresh = e.target.checked;
        if (state.autoRefresh) startAutoRefresh();
        else stopAutoRefresh();
    });

    // 文件夹标题点击折叠
    for (const dir of [...DIRS, 'git']) {
        const sec = document.querySelector(`.folder-section[data-dir="${dir}"]`);
        if (!sec) continue;
        sec.querySelector('h3').addEventListener('click', () => {
            sec.querySelector('.file-list').classList.toggle('collapsed');
        });
    }

    // pipeline stages 点击 → 给个引导
    $$('.stage').forEach(stage => {
        stage.addEventListener('click', () => {
            const n = stage.dataset.stage;
            const guide = {
                '1': '点击右上角 [+ 新建需求] 启动 /requirement-analysis',
                '2': '在左侧"策划案"选中文档 → 右侧 [✓ 通过审查] 或 [✗ 打回]',
                '3': '在左侧"策划案"选中已 Approved 的文档 → 右侧 [⚙ 模块分析]；如有 xlsx 变更，会同步产出配表规划',
                '4': '在左侧"代码规划"选中文档 → 右侧 [📊 生成配表]，或在"配表规划"选中文档 → [📊 生成 xlsx]',
                '5': '在"配表规划"选中文档 → 右侧 [🚀 发布配表]，或顶部 [🚀 发布配表]；webCtrl 直接调 .\\发布配表.bat（外部 orchestrator 触发，skill 不串调）',
                '6': '在左侧"代码规划"选中文档 → 右侧 [⚡ 生成代码]',
                '7': '点击 [🔍 评审任意范围]，或选中"未提交改动"评审本批 diff',
                '8': '在左侧"代码规划"选中文档 → 右侧 [🎨 生成预制体]，或顶部 [🎨 生成预制体] 给类名清单/--diff；产出 Editor 构建器，需在 Unity 点菜单实际产出 .prefab',
            };
            showToast(guide[n] || '');
        });
    });
}

// ================= 自动刷新 =================

function startAutoRefresh() {
    stopAutoRefresh();
    state.refreshTimer = setInterval(() => {
        loadTrees().catch(() => {});
    }, 5000);
}

function stopAutoRefresh() {
    if (state.refreshTimer) {
        clearInterval(state.refreshTimer);
        state.refreshTimer = null;
    }
}

// ================= 入口 =================

(async function init() {
    bindHeaderActions();
    await loadTrees();
    if (state.autoRefresh) startAutoRefresh();
})();
