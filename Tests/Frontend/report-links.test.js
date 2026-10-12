const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const elements = {};
function element() {
    return { value: '', style: {}, dataset: {}, children: [], classList: { toggle() {} },
        appendChild(child) { this.children.push(child); }, setAttribute() {}, addEventListener() {},
        set innerHTML(value) { assert.equal(value, ''); this.children = []; } };
}
const document = { body: element(), documentElement: element(), createElement: element,
    getElementById(id) { return elements[id] ||= element(); } };
document.getElementById('reports-root').dataset.packageDetailUrl = '/demo/Packages/Detail';
const identifier = 'F20-1/"<script>';
const CourierApp = { toast: { error() {}, show(message) { throw new Error(message); } }, api: { get() {
    return Promise.resolve({ kpis: {}, perDay: [], byStatus: [], byClassification: [], byPaymentStatus: [],
        outstanding: [{ f20Identifier: identifier, recipientName: '<img src=x>', status: 'InStorage', ageDays: 2 }] });
} } };
vm.runInNewContext(fs.readFileSync('Web/Scripts/app/reports.js', 'utf8'), {
    document, window: { CourierApp }, CourierApp, console, URLSearchParams,
    getComputedStyle() { return { getPropertyValue() { return ''; } }; }, setTimeout(callback) { callback(); }
});
setImmediate(() => {
    try {
        const row = elements.tOut.children[0];
        const link = row.children[0].children[0];
        assert.equal(link.href, '/demo/Packages/Detail/' + encodeURIComponent(identifier));
        assert.equal(link.textContent, identifier);
        assert.equal(row.children[1].textContent, '<img src=x>');
        console.log('Report link checks passed: application path, encoded identifier and safe recipient text.');
    } catch (error) { console.error(error); process.exitCode = 1; }
});
