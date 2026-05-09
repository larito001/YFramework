'use strict';

const express = require('express');
const path = require('path');
const fs = require('fs');
const os = require('os');
const { spawn, execSync } = require('child_process');

const PROJECT_ROOT = path.resolve(__dirname, '..');

// 受控目录：Web 端只能读这几个目录下的内容
const ALLOWED_DIRS = {
    '策划案': path.join(PROJECT_ROOT, '策划案'),
    '代码规划': path.join(PROJECT_ROOT, '代码规划'),
    '代码优化规划': path.join(PROJECT_ROOT, '代码优化规划'),
};

const app = express();
app.use(express.json({ limit: '1mb' }));
app.use(express.static(path.join(__dirname, 'public')));

// ---- helpers ----

function safeJoin(base, sub) {
    const full = path.resolve(base, sub);
    if (!full.startsWith(path.resolve(base) + path.sep) && full !== path.resolve(base)) {
        return null;
    }
    return full;
}

function walkDir(rootDir, baseDir) {
    if (!fs.existsSync(rootDir)) return [];
    const out = [];
    const stack = [rootDir];
    while (stack.length) {
        const cur = stack.pop();
        let entries;
        try {
            entries = fs.readdirSync(cur, { withFileTypes: true });
        } catch (e) {
            continue;
        }
        for (const entry of entries) {
            const full = path.join(cur, entry.name);
            if (entry.isDirectory()) {
                if (entry.name === 'assets' || entry.name.startsWith('.')) continue;
                stack.push(full);
            } else if (entry.name.toLowerCase().endsWith('.md')) {
                let stat;
                try { stat = fs.statSync(full); } catch { continue; }
                out.push({
                    path: path.relative(baseDir, full).replace(/\\/g, '/'),
                    name: entry.name,
                    size: stat.size,
                    mtime: stat.mtime.toISOString(),
                });
            }
        }
    }
    return out;
}

function parseFrontmatter(content) {
    const m = /^---\r?\n([\s\S]*?)\r?\n---/.exec(content);
    if (!m) return null;
    const fm = {};
    for (const raw of m[1].split(/\r?\n/)) {
        const kv = /^([a-zA-Z_][\w-]*):\s*(.*)$/.exec(raw);
        if (kv) {
            let v = kv[2].trim();
            // 去掉 yaml 里两边的引号
            if ((v.startsWith('"') && v.endsWith('"')) || (v.startsWith("'") && v.endsWith("'"))) {
                v = v.slice(1, -1);
            }
            fm[kv[1]] = v;
        }
    }
    return fm;
}

// ---- API: 列目录 ----

app.get('/api/tree', (req, res) => {
    const dir = req.query.dir;
    if (!ALLOWED_DIRS[dir]) return res.status(400).json({ error: 'Invalid dir' });
    const root = ALLOWED_DIRS[dir];
    const files = walkDir(root, root);
    files.sort((a, b) => b.mtime.localeCompare(a.mtime));
    for (const f of files) {
        try {
            const content = fs.readFileSync(path.join(root, f.path), 'utf8');
            f.frontmatter = parseFrontmatter(content);
        } catch (e) {
            f.frontmatter = null;
        }
    }
    res.json({ dir, files });
});

// ---- API: 读单文件 ----

app.get('/api/file', (req, res) => {
    const { dir, file } = req.query;
    if (!ALLOWED_DIRS[dir]) return res.status(400).json({ error: 'Invalid dir' });
    const full = safeJoin(ALLOWED_DIRS[dir], file || '');
    if (!full || !fs.existsSync(full)) return res.status(404).json({ error: 'Not found' });
    const content = fs.readFileSync(full, 'utf8');
    res.json({ dir, path: file, content });
});

// ---- API: 更新 frontmatter status ----

app.put('/api/file/status', (req, res) => {
    const { dir, file } = req.query;
    const { status } = req.body || {};
    const allowedStatus = ['Draft', 'Review', 'Approved', 'Implemented', 'Deprecated', 'Resolved', 'Stale'];
    if (!ALLOWED_DIRS[dir]) return res.status(400).json({ error: 'Invalid dir' });
    if (!allowedStatus.includes(status)) return res.status(400).json({ error: 'Invalid status' });
    const full = safeJoin(ALLOWED_DIRS[dir], file || '');
    if (!full || !fs.existsSync(full)) return res.status(404).json({ error: 'Not found' });
    let content = fs.readFileSync(full, 'utf8');
    const today = new Date().toISOString().slice(0, 10);
    const before = content;
    content = content.replace(/^(status:\s*)\S+/m, `$1${status}`);
    content = content.replace(/^(updated:\s*)\S+/m, `$1${today}`);
    if (content === before) return res.status(400).json({ error: 'frontmatter status/updated 字段未找到，无法修改' });
    fs.writeFileSync(full, content, 'utf8');
    res.json({ ok: true, status, updated: today });
});

// ---- API: git diff（用于代码评审 --diff 范围预览）----

app.get('/api/git/diff', (req, res) => {
    try {
        const modified = execSync('git diff --name-only HEAD', { cwd: PROJECT_ROOT, encoding: 'utf8' })
            .split(/\r?\n/).filter(Boolean);
        const staged = execSync('git diff --name-only --cached', { cwd: PROJECT_ROOT, encoding: 'utf8' })
            .split(/\r?\n/).filter(Boolean);
        let untracked = [];
        try {
            untracked = execSync('git ls-files --others --exclude-standard', { cwd: PROJECT_ROOT, encoding: 'utf8' })
                .split(/\r?\n/).filter(Boolean);
        } catch { /* ignore */ }
        const all = Array.from(new Set([...modified, ...staged, ...untracked])).sort();
        res.json({ modified, staged, untracked, all });
    } catch (e) {
        res.status(500).json({ error: String(e.message || e) });
    }
});

// ---- API: 启动新的 Claude 窗口 ----

app.post('/api/launch', (req, res) => {
    const { prompt, title } = req.body || {};
    if (!prompt || typeof prompt !== 'string' || prompt.trim().length === 0) {
        return res.status(400).json({ error: 'prompt required' });
    }
    const safeTitle = String(title || 'Claude').replace(/["\r\n]/g, '').slice(0, 80);

    // 把所有 Unicode（中文 / 破折号 / emoji）封到 .ps1 内部，spawn 参数保持纯 ASCII
    // —— 避免 cmd.exe 在中文 GBK / em-dash 上炸出 Windows 错误弹窗
    const tmpScript = path.join(
        os.tmpdir(),
        `claude-launch-${Date.now()}-${Math.random().toString(36).slice(2, 8)}.ps1`
    );
    const escPrompt = prompt.replace(/'/g, "''");
    const escCwd = PROJECT_ROOT.replace(/'/g, "''");
    const escTitle = safeTitle.replace(/'/g, "''");
    const scriptContent = [
        `# webCtrl 自动生成 ${new Date().toISOString()}`,
        `$OutputEncoding = [System.Text.Encoding]::UTF8`,
        `[Console]::OutputEncoding = [System.Text.Encoding]::UTF8`,
        `[Console]::InputEncoding = [System.Text.Encoding]::UTF8`,
        `try { $Host.UI.RawUI.WindowTitle = '${escTitle}' } catch {}`,
        `Set-Location -LiteralPath '${escCwd}'`,
        `Write-Host '[webCtrl] 标题: ${escTitle}' -ForegroundColor Cyan`,
        `Write-Host '[webCtrl] 启动 Claude，初始指令：' -ForegroundColor Cyan`,
        `Write-Host '${escPrompt}' -ForegroundColor Yellow`,
        `Write-Host ''`,
        `claude '${escPrompt}'`,
    ].join('\r\n');
    // 加 UTF-8 BOM 保证 PowerShell 5.1 正确识别中文
    fs.writeFileSync(tmpScript, '﻿' + scriptContent, { encoding: 'utf8' });

    // 关键：spawn 的参数列表全部 ASCII，避免 Node 在 cmd.exe 命令行上做的引号转义碰到中文导致解析失败。
    // - "" 是 start 命令的空标题占位（标题已在 .ps1 里通过 $Host.UI.RawUI.WindowTitle 设过）
    // - powershell.exe 跟随其后是被 start 启动的真实程序
    const child = spawn(
        'cmd.exe',
        ['/c', 'start', '""', 'powershell.exe', '-NoExit', '-ExecutionPolicy', 'Bypass', '-File', tmpScript],
        {
            detached: true,
            stdio: 'ignore',
            windowsHide: false,
            windowsVerbatimArguments: true,
        }
    );
    child.on('error', err => {
        console.error('[launch] spawn error:', err);
    });
    child.unref();

    res.json({ ok: true, script: tmpScript, prompt, title: safeTitle });
});

// ---- API: 健康检查 ----

app.get('/api/health', (_req, res) => {
    res.json({
        ok: true,
        projectRoot: PROJECT_ROOT,
        dirs: Object.fromEntries(
            Object.entries(ALLOWED_DIRS).map(([k, v]) => [k, fs.existsSync(v)])
        ),
    });
});

// ---- start ----

const HOST = '127.0.0.1';
const PORT_CANDIDATES = process.env.PORT
    ? [Number(process.env.PORT)]
    : [7777, 5757, 4747, 3737, 8181, 0]; // 0 = 让系统分配可用端口

function tryListen(idx = 0) {
    if (idx >= PORT_CANDIDATES.length) {
        console.error('[webCtrl] 所有候选端口都无法绑定，退出。');
        process.exit(1);
    }
    const port = PORT_CANDIDATES[idx];
    const server = app.listen(port, HOST, () => {
        const actual = server.address().port;
        console.log(`\n[webCtrl] 监听: http://${HOST}:${actual}`);
        console.log(`[webCtrl] 浏览器请打开: http://localhost:${actual}`);
        console.log(`[webCtrl] 项目根: ${PROJECT_ROOT}`);
        for (const [k, v] of Object.entries(ALLOWED_DIRS)) {
            console.log(`[webCtrl] ${k.padEnd(12)} → ${v} ${fs.existsSync(v) ? '' : '(目录不存在)'}`);
        }
    });
    server.on('error', err => {
        if (err.code === 'EACCES' || err.code === 'EADDRINUSE') {
            console.warn(`[webCtrl] 端口 ${port} ${err.code === 'EACCES' ? '被系统保留（拒绝访问）' : '已被占用'}，尝试下一个候选...`);
            tryListen(idx + 1);
        } else {
            console.error(`[webCtrl] 启动失败:`, err);
            process.exit(1);
        }
    });
}

tryListen();
