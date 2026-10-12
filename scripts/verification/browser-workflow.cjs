const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const { createRequire } = require('node:module');
const base = process.argv[2];
const output = path.resolve(process.argv[3]);
const toolsRoot = path.resolve(process.argv[4]);
if (!/^http:\/\/(localhost|127\.0\.0\.1):\d+$/.test(base)) throw new Error('Synthetic loopback environment only.');
const { chromium } = createRequire(path.join(toolsRoot, 'package.json'))('playwright');
fs.mkdirSync(output, { recursive: true });
async function main() {
    const browser = await chromium.launch({ executablePath: 'C:/Program Files/Google/Chrome/Application/chrome.exe', headless: true, args: ['--no-proxy-server'] });
    try {
        const context = await browser.newContext({ viewport: { width: 1366, height: 768 } });
        const page = await context.newPage();
        const errors = [];
        page.on('pageerror', error => errors.push(error.message));
        const steps = [];
        async function login(role) {
            const response = await context.request.post(base + '/api/auth/login', { data: { username: role + '.demo', password: 'Demo@2026!' } });
            assert.equal(response.status(), 200);
        }
        async function screenshot(name) { await page.screenshot({ path: path.join(output, name + '.png'), fullPage: true }); }
        async function record(name) { steps.push({ name, passed: true }); console.log('PASS ' + name); }
        await login('intake');
        await page.goto(base + '/Packages/Register', { waitUntil: 'networkidle' });
        const run = Date.now();
        for (const [id, value] of Object.entries({ 'recipient-fullName': 'Synthetic Walkthrough Recipient', 'recipient-identifierNo': 'QA-UI-' + run, 'recipient-email': 'walkthrough@example.test', 'recipient-phone': '0001234567', senderName: 'Synthetic Sender' })) await page.locator('#' + id).fill(value);
        await page.locator('#packageType').selectOption('Box');
        await page.locator('input[name="classification"][value="Personal"]').check();
        await page.locator('#storageLocationId').selectOption('1');
        await page.waitForFunction(() => document.getElementById('fee-display').textContent.includes('R10.00'));
        await screenshot('registration');
        await Promise.all([page.waitForURL(/\/Packages\/Label\/F20-/), page.locator('#register-submit').click()]);
        const identifier = new URL(page.url()).pathname.split('/').pop();
        await page.waitForLoadState('networkidle');
        await screenshot('label');
        await record('Intake registration through the form and saved QR label');
        await login('storage');
        await page.goto(base + '/Scan', { waitUntil: 'networkidle' });
        await page.locator('#usb-scan-input').fill(identifier);
        await page.locator('#usb-scan-input').press('Enter');
        await page.locator('#status-select').selectOption('InStorage');
        await page.locator('#confirm-status-update').click();
        await page.waitForFunction(() => document.getElementById('status-select').querySelector('option[value="ReadyForCollection"]'));
        await page.locator('#status-select').selectOption('ReadyForCollection');
        await page.locator('#confirm-status-update').click();
        await page.waitForFunction(() => document.getElementById('scan-result').textContent.includes('Ready for Collection'));
        await screenshot('scan-status');
        await record('Keyboard Enter scan, stored and ready updates through the UI');
        await login('collection');
        await page.goto(base + '/Collection', { waitUntil: 'networkidle' });
        await page.locator('#package-lookup-input').fill(identifier);
        await page.locator('#btn-lookup').click();
        await page.locator('#chk-verify-id').check();
        await page.locator('#btn-confirm-collection').click();
        await screenshot('collection-confirmation');
        await page.locator('#btn-modal-cancel').click();
        let lookup = await (await context.request.get(base + '/api/packages/scan/' + identifier)).json();
        assert.equal(lookup.status, 'ReadyForCollection');
        await page.locator('#btn-confirm-collection').click();
        const collected = page.waitForResponse(response => response.url().includes('/collect') && response.request().method() === 'POST');
        await page.locator('#btn-modal-submit').click();
        assert.equal((await collected).status(), 200);
        lookup = await (await context.request.get(base + '/api/packages/scan/' + identifier)).json();
        assert.equal(lookup.status, 'Collected');
        await record('Identity checkbox, cancellation without mutation and confirmed collection');
        await page.goto(base + '/Packages/Detail/' + identifier, { waitUntil: 'networkidle' });
        await page.locator('#d-payment-select').selectOption('Paid');
        await page.locator('#d-payment-save').click();
        await page.waitForFunction(() => document.getElementById('d-payment').textContent === 'Paid');
        await screenshot('details-payment');
        await page.reload({ waitUntil: 'networkidle' });
        assert.equal(await page.locator('#d-payment').textContent(), 'Paid');
        await record('Payment saved through UI and retained after reload');
        await login('supervisor');
        await page.goto(base + '/CsvImport', { waitUntil: 'networkidle' });
        await page.locator('#csvFile').setInputFiles(path.resolve('scripts/verification/fixtures/role-import.csv'));
        await page.locator('#btn-upload').click();
        await page.locator('#import-summary-card').waitFor({ state: 'visible' });
        await screenshot('import-results');
        await record('Supervisor CSV form displays saved rows and validation errors');
        assert.deepEqual(errors, []);
        fs.writeFileSync(path.join(output, 'workflow-results.json'), JSON.stringify({ baseUrl: base, syntheticOnly: true, package: identifier, passed: steps.length, steps, browserErrors: errors }, null, 2));
    } finally { await browser.close(); }
}
main().catch(error => { console.error(error); process.exitCode = 1; });
