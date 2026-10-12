const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const tick = () => new Promise(resolve => setImmediate(resolve));
function element() {
    return { value: '', style: {}, dataset: {}, children: [], handlers: {}, disabled: false,
        appendChild(child) { this.children.push(child); }, setAttribute() {},
        addEventListener(name, callback) { this.handlers[name] = callback; },
        set innerHTML(value) { assert.equal(value, ''); this.children = []; } };
}
async function scenario(confirmResult, failPatch = false) {
    const elements = {};
    const document = { getElementById(id) { return elements[id] ||= element(); }, createElement: element,
        addEventListener(name, callback) { if (name === 'DOMContentLoaded') callback(); } };
    document.getElementById('user-mgmt').dataset = { apiBase: '/demo/api', currentUserId: '1' };
    const users = [{ userId: 1, username: 'self', role: 'SystemAdmin', isActive: true },
        { userId: 2, username: '<img src=x>', role: 'IntakeClerk', isActive: true }];
    const requests = [], errors = [];
    const CourierApp = { confirm: () => Promise.resolve(confirmResult), toast: { success() {}, error(message) { errors.push(message); } } };
    const fetch = async (url, request) => {
        requests.push({ url, ...request });
        return { ok: !(failPatch && request.method === 'PATCH'), status: failPatch ? 500 : 200,
            json: async () => request.method === 'GET' ? users : { error: { message: 'Failed safely' } } };
    };
    vm.runInNewContext(fs.readFileSync('Web/Scripts/app/users.js', 'utf8'), {
        document, window: { USER_ROLES: ['IntakeClerk', 'SystemAdmin'] }, fetch, CourierApp
    });
    await tick();
    const rows = elements['users-body'].children;
    assert.equal(rows[0].children[5].children.length, 0, 'Self-deactivation stays unavailable');
    assert.equal(rows[1].children[0].textContent, '<img src=x>');
    const button = rows[1].children[5].children[0];
    button.handlers.click();
    button.handlers.click();
    await tick();
    const patches = requests.filter(r => r.method === 'PATCH');
    assert.equal(patches.length, confirmResult ? 1 : 0);
    if (confirmResult) assert.equal(JSON.parse(patches[0].body).isActive, false);
    assert.equal(button.disabled, false);
    const role = elements['users-body'].children[1].children[2].children[0];
    role.value = 'SystemAdmin';
    role.handlers.change();
    await tick();
    if (!confirmResult || failPatch) assert.equal(role.value, 'IntakeClerk');
    assert.equal(role.disabled, false);
    if (failPatch) assert.equal(errors.length, 2);
}
(async () => {
    await scenario(false);
    await scenario(true);
    await scenario(true, true);
    console.log('User management checks passed: cancel, confirmation, duplicate clicks, failures and self-protection.');
})().catch(error => { console.error(error); process.exitCode = 1; });
