// Run from the repository root: node Tests/Frontend/registration-fees.test.js
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const view = fs.readFileSync('Web/Views/Packages/Register.cshtml', 'utf8');
const script = view.match(/<script>([\s\S]*?)<\/script>/)[1].replace(/@Url\.Action\("Label", "Packages"\)/g, '/Packages/Label').replace(/@@/g, '@');
async function run(fees) {
    const elements = {};
    const errors = [], posts = [];
    let selected = { value: 'Personal' };
    const element = () => ({ value: 'valid', textContent: '', className: '', disabled: false,
        addEventListener(name, callback) { this[name] = callback; }, appendChild() {} });
    const document = { getElementById(id) { return elements[id] ||= element(); }, createElement: element,
        querySelectorAll() { return []; }, querySelector(selector) { return selector.includes('classification') ? selected : null; } };
    const CourierApp = { api: { get(path) { return path === '/packages/fees' ? (fees ? Promise.resolve(fees) : Promise.reject(new Error('unavailable'))) : Promise.resolve([]); },
        post(path, body) { posts.push(body); return Promise.resolve({ f20Identifier: 'F20-1' }); } },
        toast: { error(message) { errors.push(message); }, success() {} } };
    vm.runInNewContext(script, { document, CourierApp, window: { location: {} }, console });
    await new Promise(resolve => setImmediate(resolve));
    return { elements, posts, errors };
}
(async () => {
    const loaded = await run({ Personal: 12.5, WorkRelated: 0 });
    assert.match(loaded.elements['fee-display'].textContent, /R12\.50/);
    const missing = await run(null);
    missing.elements['register-form'].submit({ preventDefault() {} });
    assert.equal(missing.posts.length, 0);
    assert.match(missing.errors[0], /Fees are unavailable/);
    console.log('Registration fee checks passed: database fee display and failed-lookup submission protection.');
})().catch(error => { console.error(error); process.exitCode = 1; });
