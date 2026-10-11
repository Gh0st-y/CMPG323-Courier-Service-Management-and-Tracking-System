// Run from the repository root: node Tests/Frontend/package-page-identifiers.test.js
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
for (const [page, expectedId] of [['Detail', 'detail-id'], ['Label', 'f20-id']]) {
    const view = fs.readFileSync('Web/Views/Packages/' + page + '.cshtml', 'utf8');
    const assignment = view.slice(view.indexOf('@section scripts')).match(/var f20Identifier = ([^;]+);/)[0];
    for (const input of ['F20-0001', '\";globalThis.injected=true;//', '</script><script>globalThis.injected=true</script>', '"\\\n&']) {
        const context = { document: { getElementById(id) { assert.equal(id, expectedId); return { textContent: input }; } } };
        vm.runInNewContext(assignment, context);
        assert.equal(context.f20Identifier, input);
        assert.equal(context.injected, undefined);
    }
    assert.ok(!view.includes('@Html.Raw(f20Identifier)'), 'Identifier must not be inserted into script source');
}
console.log('Package page identifier regression checks passed for normal identifiers, quotes, script markup and escape characters.');
