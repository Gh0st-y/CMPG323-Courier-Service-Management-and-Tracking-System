// Run from the repository root: node Tests/Frontend/audit-log-suggestions.test.js
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const payload = '<img src=x onerror=alert(1)>\'"';
function element() {
    return {
        value: '', style: {}, dataset: {}, children: [], handlers: {}, textContent: '',
        addEventListener(name, callback) { this.handlers[name] = callback; },
        appendChild(child) { this.children.push(child); },
        replaceChildren() { this.children = []; },
        contains(target) { return target === this || this.children.includes(target); },
        closest(selector) { return selector === '.suggestion-item' && this.className === 'suggestion-item' ? this : null; },
        set innerHTML(value) { assert.equal(value, '', 'Audit values must never be parsed as HTML'); }
    };
}
const elements = {};
for (const id of ['startDate', 'endDate', 'dateRangeError', 'auditFilterForm', 'searchTerm', 'customSuggestionsDropdown', 'searchSuggestionsData']) {
    elements[id] = element();
}
elements.searchSuggestionsData.getAttribute = name => JSON.stringify(name === 'data-types' ? [payload] : []);
const document = {
    getElementById(id) { return elements[id]; },
    createElement: element,
    addEventListener(name, callback) { if (name === 'DOMContentLoaded') callback(); }
};
vm.runInNewContext(fs.readFileSync('Web/Scripts/app/audit-log-filter.js', 'utf8'), {
    document, window: { location: { search: '?searchTerm=img' } }, performance: { navigation: { type: 0 } }, flatpickr() {}, console
});
const input = elements.searchTerm;
const dropdown = elements.customSuggestionsDropdown;
input.value = 'img';
input.handlers.input.call(input);
assert.equal(dropdown.children.length, 2);
const option = dropdown.children[1];
assert.equal(option.textContent, payload);
assert.equal(option.dataset.value, payload);
assert.equal(option.children.length, 0);
dropdown.handlers.click({ target: option });
assert.equal(input.value, payload);
assert.equal(dropdown.style.display, 'none');
input.value = 'no match';
input.handlers.input.call(input);
assert.equal(dropdown.children.length, 0);
input.value = '';
input.handlers.input.call(input);
assert.equal(dropdown.style.display, 'none');
console.log('Audit suggestion checks passed: malicious markup stays text, selection round-trips, and empty/no-match results clear.');
