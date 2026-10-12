// Run from the repository root: node Tests/Frontend/csv-import.test.js
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
function element() {
    return { style: {}, disabled: false, textContent: '', children: [], files: [],
        classList: { add() {}, remove() {} }, setAttribute() {},
        appendChild(child) { this.children.push(child); },
        addEventListener(name, handler) { this[name] = handler; } };
}
async function scenario(response, options = {}) {
    const elements = {};
    const document = { body: element(), createElement: element,
        getElementById(id) { return elements[id] ||= element(); },
        addEventListener(name, handler) { handler(); } };
    let mock = options.mock || false;
    const calls = [], successes = [], errors = [];
    const window = { localStorage: { getItem() { return String(mock); }, setItem(k, v) { mock = v === 'true'; } },
        setTimeout() {}, location: { pathname: '/CsvImport', search: '', href: '' } };
    const fetch = async (url, request) => {
        calls.push({ url, request });
        if (options.networkError) throw new Error('offline');
        return { ok: !options.status || options.status < 400, status: options.status || 200,
            json: async () => response };
    };
    const context = vm.createContext({ window, document, fetch, FormData, console });
    vm.runInContext(fs.readFileSync('Web/Scripts/app/courier-app.js', 'utf8'), context);
    context.CourierApp = window.CourierApp;
    window.CourierApp.toast = { success(message) { successes.push(message); }, error(message) { errors.push(message); } };
    vm.runInContext(fs.readFileSync('Web/Scripts/app/csv-import.js', 'utf8'), context);
    document.getElementById('csvFile').files = [new File(['csv contents'], 'packages.CSV')];
    const form = document.getElementById('csv-upload-form');
    form.submit({ preventDefault() {} });
    form.submit({ preventDefault() {} }); // A second click cannot start a duplicate import.
    await new Promise(resolve => setImmediate(resolve));
    return { elements, calls, successes, errors, window };
}
(async function () {
    const result = await scenario({ importedCount: 2, failedRows: [{ row: 4, reason: '<script>invalid</script>' }] });
    assert.equal(result.calls.length, 1);
    assert.equal(result.calls[0].url, '/api/import/csv');
    assert.equal(result.calls[0].request.method, 'POST');
    assert.equal(result.calls[0].request.credentials, 'same-origin');
    assert.ok(result.calls[0].request.body instanceof FormData);
    assert.equal(result.calls[0].request.body.get('csvFile').name, 'packages.CSV');
    assert.equal(result.calls[0].request.headers['Content-Type'], undefined);
    assert.equal(result.elements['stat-succeeded'].textContent, 2);
    assert.equal(result.elements['stat-total'].textContent, 3);
    assert.equal(result.elements['failed-rows-tbody'].children[0].children[1].textContent, '<script>invalid</script>');
    assert.equal(result.elements['btn-upload'].disabled, false);
    const rejected = await scenario({ error: { message: 'Missing required columns.' } }, { status: 400 });
    assert.equal(rejected.successes.length, 0);
    assert.equal(rejected.elements['file-error'].textContent, 'Missing required columns.');
    assert.equal(rejected.elements['import-summary-card'].style.display, 'none');
    const unauthorized = await scenario({}, { status: 401 });
    assert.match(unauthorized.window.location.href, /Account\/Login/);
    const offline = await scenario(null, { networkError: true });
    assert.equal(offline.successes.length, 0);
    assert.match(offline.elements['file-error'].textContent, /before retrying/);
    const malformed = await scenario({});
    assert.equal(malformed.successes.length, 0);
    assert.match(malformed.elements['file-error'].textContent, /unexpected result/);
    const mocked = await scenario({}, { mock: true });
    assert.equal(mocked.calls.length, 0);
    assert.match(mocked.elements['file-error'].textContent, /mock mode/);
    console.log('CSV upload regression checks passed: multipart, results, duplicate submit, validation, auth, network, malformed response and mock mode.');
})().catch(error => { console.error(error); process.exitCode = 1; });
