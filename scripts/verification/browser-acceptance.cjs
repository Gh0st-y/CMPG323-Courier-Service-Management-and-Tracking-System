const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const { createRequire } = require('node:module');
const toolsRoot = path.resolve(process.argv[2]);
const output = path.resolve(process.argv[3]);
const baseUrl = process.argv[4] || 'http://localhost:48131';
if (!/^http:\/\/(localhost|127\.0\.0\.1):\d+$/.test(baseUrl)) throw new Error('Use an isolated loopback site.');
const { chromium, firefox } = createRequire(path.join(toolsRoot, 'package.json'))('playwright');
fs.mkdirSync(output, { recursive: true });
const results = [], failures = [];
const onlyRoles = process.env.ACCEPTANCE_ROLES ? process.env.ACCEPTANCE_ROLES.split(',') : null;
const rolePages = {
    intake: ['/Home', '/Packages/Register', '/Packages/Search', '/Packages/Detail/F20-0001', '/Packages/Label/F20-0001'],
    storage: ['/Home', '/Scan', '/Packages/Detail/F20-0001'],
    collection: ['/Home', '/Collection', '/Packages/Detail/F20-0001'],
    supervisor: ['/Home', '/Reports', '/CsvImport', '/AuditLog'],
    admin: ['/Home', '/Reports', '/UserManagement', '/AuditLog']
};
async function check(name, action) {
    try { const evidence = await action(); results.push({ name, passed: true, evidence }); console.log('PASS ' + name); }
    catch (error) { results.push({ name, passed: false, error: error.message }); failures.push(name); console.log('FAIL ' + name + ': ' + error.message); }
}
async function main() {
    for (const browserName of ['chrome', 'firefox']) {
        const browser = await (browserName === 'chrome' ? chromium.launch({ executablePath: 'C:/Program Files/Google/Chrome/Application/chrome.exe', headless: true, args: ['--no-proxy-server'] }) : firefox.launch({ headless: true, firefoxUserPrefs: { 'network.proxy.type': 0 } }));
        for (const [role, pages] of Object.entries(rolePages)) {
            if (onlyRoles && !onlyRoles.includes(role)) continue;
            const context = await browser.newContext({ viewport: { width: 1366, height: 768 } });
            const page = await context.newPage();
            page.setDefaultTimeout(10000);
            const pageErrors = [];
            page.on('pageerror', error => pageErrors.push(error.message));
            await check(browserName + ' ' + role + ' login', async () => {
                if (role === 'intake') {
                    await page.goto(baseUrl + '/Account/Login');
                    await page.screenshot({ path: path.join(output, browserName + '-login.png'), fullPage: true });
                    await page.locator('input[name="UsernameOrEmail"]').fill('intake.demo');
                    await page.locator('input[name="Password"]').fill('Demo@2026!');
                    await Promise.all([page.waitForURL(url => !url.pathname.includes('/Account/Login')), page.locator('button[type="submit"]').click()]);
                } else {
                    const response = await context.request.post(baseUrl + '/api/auth/login', { data: { username: role + '.demo', password: 'Demo@2026!' } });
                    assert.equal(response.status(), 200);
                }
            });
            for (const route of pages) {
                await check(browserName + ' ' + role + ' ' + route, async () => {
                    const response = await page.goto(baseUrl + route, { waitUntil: 'networkidle' });
                    assert.equal(response.status(), 200);
                    assert.equal(new URL(page.url()).pathname.includes('/Account/Login'), false, 'Staff page must not redirect back to login');
                    assert.equal(await page.locator('#toggle-mock-mode').count(), 0, 'Mock switch must be hidden');
                    if (route === '/Packages/Register') {
                        await page.locator('input[name="classification"][value="Personal"]').check();
                        await page.waitForFunction(() => document.getElementById('fee-display').textContent.includes('R10.00'));
                        assert.match(await page.locator('#fee-display').textContent(), /Personal — R10\.00/);
                        assert.match(await page.locator('#packageType').textContent(), /— Select —/);
                    }
                    if (route === '/Scan') {
                        assert.equal(await page.evaluate(() => document.activeElement.id), 'usb-scan-input');
                        await page.locator('#usb-scan-input').fill('F20-0001');
                        await page.locator('#usb-scan-input').press('Enter');
                        await page.waitForFunction(() => document.getElementById('scan-result').textContent.includes('F20-0001'));
                    }
                    if (route.startsWith('/Packages/Detail') && ['storage', 'collection'].includes(role)) assert.equal(await page.locator('#detail-label-link').count(), 0);
                    if (route === '/Reports') {
                        await page.waitForFunction(() => document.querySelector('#tOut a'));
                        assert.match(await page.locator('#tOut a').first().getAttribute('href'), /\/Packages\/Detail\/F20-/);
                    }
                    if (route === '/UserManagement') {
                        await page.waitForFunction(() => document.querySelectorAll('#users-body tr').length >= 5);
                        await page.locator('#btn-toggle-create').click();
                        await page.locator('#new-username').fill('synthetic.cancelled');
                        await page.locator('#new-password').fill('Synthetic123!');
                        await page.locator('#btn-cancel-create').click();
                        assert.equal(await page.locator('#new-username').inputValue(), '');
                        assert.equal(await page.locator('#new-password').inputValue(), '');
                        assert.equal(await page.locator('#create-user-panel').isVisible(), false);
                        const row = page.locator('#users-body tr').filter({ hasText: 'intake.demo' });
                        await row.locator('button').click();
                        await page.locator('.modal-dialog button').filter({ hasText: 'Cancel' }).click();
                        assert.equal(await row.locator('button').isEnabled(), true);
                        assert.match(await row.textContent(), /Active/);
                    }
                    const screenshot = browserName + '-' + role + '-' + route.replace(/\W+/g, '-').toLowerCase() + '.png';
                    await page.screenshot({ path: path.join(output, screenshot), fullPage: true });
                    assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth + 1), false, 'Page must fit 1366px viewport');
                    return screenshot;
                });
            }
            await check(browserName + ' ' + role + ' no JavaScript exceptions', async () => assert.deepEqual(pageErrors, []));
            if (role === 'admin') {
                for (const route of ['/Reports', '/UserManagement', '/AuditLog', '/Scan']) {
                    await check(browserName + ' narrow ' + route, async () => {
                        await page.setViewportSize({ width: 390, height: 844 });
                        await page.goto(baseUrl + route, { waitUntil: 'networkidle' });
                        assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth + 1), false, 'Page must fit narrow viewport');
                        await page.screenshot({ path: path.join(output, browserName + '-narrow-' + route.slice(1).toLowerCase() + '.png'), fullPage: true });
                    });
                }
            }
            await context.close();
        }
        await browser.close();
    }
    fs.writeFileSync(path.join(output, 'browser-results.json'), JSON.stringify({ baseUrl, at: new Date().toISOString(), passed: results.filter(r => r.passed).length, failed: failures.length, results }, null, 2));
    console.log('Browser checks: ' + results.filter(r => r.passed).length + ' passed, ' + failures.length + ' failed.');
    if (failures.length) process.exitCode = 1;
}
main().catch(error => { console.error(error); process.exitCode = 1; });
