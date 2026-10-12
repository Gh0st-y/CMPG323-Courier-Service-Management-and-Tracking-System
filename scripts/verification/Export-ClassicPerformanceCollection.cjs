// CLI performance mode currently accepts classic v2 only. The maintained v3 requests remain the source.
const fs = require('node:fs');
const path = require('node:path');
const root = path.resolve('postman/collections/Performance reads');
const output = process.argv[2];
if (!output) throw new Error('Supply a local artifact output path.');
const item = fs.readdirSync(root).filter(f => f.endsWith('.request.yaml')).map(file => {
    const text = fs.readFileSync(path.join(root, file), 'utf8');
    const url = text.match(/^url: '([^']+)'$/m)[1];
    const method = text.match(/^method: (\w+)$/m)[1];
    const test = text.split('    code: |-\n')[1].trimEnd().split('\n').map(line => line.slice(6));
    return { name: file.replace('.request.yaml', ''), request: { method, url, header: [{ key: 'Cookie', value: '{{session_cookie}}' }] }, event: [{ listen: 'test', script: { type: 'text/javascript', exec: test } }] };
});
const collection = {
    info: { name: 'Local performance reads', schema: 'https://schema.getpostman.com/json/collection/v2.1.0/collection.json' },
    variable: [{ key: 'base_url', value: 'http://localhost:48141' }],
    event: [{ listen: 'prerequest', script: { type: 'text/javascript', exec: [
        "if (!/^http:\\/\\/(localhost|127\\.0\\.0\\.1):[0-9]+$/.test(pm.variables.get('base_url'))) throw new Error('Local synthetic benchmark only.');",
        "const cookie = pm.iterationData.get('session_cookie'); if (!cookie) throw new Error('Missing synthetic session'); pm.variables.set('session_cookie', cookie);"
    ] } }], item
};
fs.writeFileSync(output, JSON.stringify(collection, null, 2));
console.log('Exported ' + item.length + ' maintained v3 requests for classic-only performance mode.');
