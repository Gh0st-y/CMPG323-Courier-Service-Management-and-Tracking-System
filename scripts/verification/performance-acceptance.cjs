const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const { performance } = require('node:perf_hooks');
const { createRequire } = require('node:module');
const base = process.argv[2] || 'http://localhost:48141';
const output = path.resolve(process.argv[3]);
const toolsRoot = path.resolve(process.argv[4]);
const statusOffset = Number(process.env.PERF_STATUS_OFFSET || 0);
const seededPackages = Number(process.env.PERF_SEEDED_PACKAGES || 50200);
if (!/^http:\/\/(localhost|127\.0\.0\.1):\d+$/.test(base)) throw new Error('Isolated loopback benchmark only.');
fs.mkdirSync(output, { recursive: true });
async function session(username) {
    const response = await fetch(base + '/api/auth/login', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ username, password: 'Demo@2026!' }) });
    assert.equal(response.status, 200);
    return response.headers.get('set-cookie').split(';')[0];
}
async function request(cookie, route, method = 'GET', body) {
    const start = performance.now();
    const response = await fetch(base + route, { method, headers: { Cookie: cookie, ...(body ? { 'Content-Type': 'application/json' } : {}) }, body: body ? JSON.stringify(body) : undefined, signal: AbortSignal.timeout(30000) });
    const data = await response.json();
    assert.equal(response.status, 200, route + ' must succeed');
    return { ms: performance.now() - start, data };
}
function stats(samples) {
    const sorted = samples.slice().sort((a, b) => a - b);
    const percentile = p => sorted[Math.max(0, Math.ceil(sorted.length * p) - 1)];
    return { samples: sorted.length, minMs: sorted[0], medianMs: percentile(.5), p95Ms: percentile(.95), maxMs: sorted.at(-1), averageMs: sorted.reduce((a, b) => a + b, 0) / sorted.length };
}
async function main() {
    const storage = await session('storage.demo');
    const supervisor = await session('supervisor.demo');
    const scan = [], search = [], status = [], concurrentScan = [], concurrentSearch = [], failures = [];
    await request(storage, '/api/packages/scan/F20-10001');
    await request(storage, '/api/packages?query=F20-100&page=1&pageSize=25');
    for (let i = 0; i < 30; i++) {
        const lookup = await request(storage, '/api/packages/scan/F20-' + (10001 + i));
        assert.equal(lookup.data.f20Identifier, 'F20-' + (10001 + i)); scan.push(lookup.ms);
        const found = await request(storage, '/api/packages?query=F20-100&page=1&pageSize=25');
        assert.ok(found.data.items.length); search.push(found.ms);
    }
    for (let i = 1; i <= 20; i++) {
        const saved = await request(storage, '/api/packages/F20-' + (10000 + (i + statusOffset) * 4) + '/status', 'POST', { newStatus: 'InStorage', storageLocationId: 1 });
        assert.equal(saved.data.status, 'InStorage'); status.push(saved.ms);
    }
    const { chromium } = createRequire(path.join(toolsRoot, 'package.json'))('playwright');
    const browser = await chromium.launch({ executablePath: 'C:/Program Files/Google/Chrome/Application/chrome.exe', headless: true, args: ['--no-proxy-server'] });
    const context = await browser.newContext({ viewport: { width: 1366, height: 768 } });
    await context.request.post(base + '/api/auth/login', { data: { username: 'supervisor.demo', password: 'Demo@2026!' } });
    const page = await context.newPage();
    const pageLoad = [];
    for (let i = 0; i < 10; i++) {
        const start = performance.now();
        const response = await page.goto(base + '/Home', { waitUntil: 'domcontentloaded' });
        assert.equal(response.status(), 200);
        await page.waitForFunction(() => document.querySelector('#dashboard-stats')?.getAttribute('aria-busy') === 'false');
        pageLoad.push(performance.now() - start);
    }
    await browser.close();
    const rows = ['RecipientFullName,RecipientIdentifierNo,RecipientEmail,RecipientPhone,SenderName,PackageType,Classification,StorageLocation'];
    const run = Date.now();
    for (let i = 0; i < 500; i++) rows.push(`Synthetic Performance ${i},QA-PERF-${run}-${i},perf${i}@example.test,0001234567,Synthetic Sender,Box,Personal,Shelf A-1`);
    const csv = rows.join('\r\n');
    fs.writeFileSync(path.join(output, 'import-500.csv'), csv);
    const form = new FormData(); form.append('csvFile', new Blob([csv], { type: 'text/csv' }), 'import-500.csv');
    const start = performance.now();
    const imported = await fetch(base + '/api/import/csv', { method: 'POST', headers: { Cookie: supervisor }, body: form, signal: AbortSignal.timeout(120000) });
    assert.equal(imported.status, 200);
    const importResult = await imported.json();
    assert.equal(importResult.importedCount, 500); assert.equal(importResult.failedRows.length, 0);
    const importMs = performance.now() - start;
    const sessions = await Promise.all(Array.from({ length: 5 }, () => session('storage.demo')));
    const concurrentStarted = Date.now(), until = concurrentStarted + 60000;
    await Promise.all(sessions.map(async cookie => {
        while (Date.now() < until) {
            try {
                const lookup = await request(cookie, '/api/packages/scan/F20-10001'); concurrentScan.push(lookup.ms);
                const found = await request(cookie, '/api/packages?query=F20-100&page=1&pageSize=25'); concurrentSearch.push(found.ms);
                assert.ok(found.data.items.length);
            } catch (error) { failures.push(error.message); }
            await new Promise(resolve => setTimeout(resolve, 250));
        }
    }));
    const result = { at: new Date().toISOString(), baseUrl: base, syntheticOnly: true, seededPackages, afterCsvPackages: seededPackages + 500,
        scan: { ...stats(scan), targetMs: 1500 }, search: { ...stats(search), targetMs: 3000 },
        statusUpdate: { ...stats(status), targetMs: null }, pageUsable: { ...stats(pageLoad), targetMs: 2000, definition: 'DOM ready plus dashboard data and minimum spinner duration; ten navigations, cold first sample retained.' },
        import500: { ms: importMs, importedCount: 500, failedRows: 0, targetMs: null },
        concurrent: { users: 5, durationMs: Date.now() - concurrentStarted, scan: stats(concurrentScan), search: stats(concurrentSearch), failures: failures.length, errors: failures },
        cliLimitation: 'Postman local performance mode rejected v3 and then failed parsing createbBy for generated v2; this report uses the local Node/browser harness, not a successful Postman performance run.' };
    fs.writeFileSync(path.join(output, 'performance-results.json'), JSON.stringify(result, null, 2));
    console.log(JSON.stringify(result, null, 2));
    assert.ok(result.scan.maxMs <= 1500 && result.search.maxMs <= 3000 && result.pageUsable.maxMs <= 2000, 'A documented performance target was exceeded');
    assert.equal(failures.length, 0); assert.ok(result.concurrent.scan.p95Ms <= 1500 && result.concurrent.search.p95Ms <= 3000);
}
main().catch(error => { console.error(error); process.exitCode = 1; });
