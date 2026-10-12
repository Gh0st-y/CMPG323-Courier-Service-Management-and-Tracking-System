// Generates filesystem-first Postman v3 requests for an isolated synthetic site.
const fs = require('node:fs');
const path = require('node:path');
const root = path.resolve(process.argv[2] || 'postman/collections/Remaining acceptance');
fs.mkdirSync(path.join(root, '.resources'), { recursive: true });
const quote = value => "'" + String(value).replace(/'/g, "''") + "'";
fs.writeFileSync(path.join(root, '.resources/definition.yaml'), `$kind: collection\nname: Remaining acceptance\nvariables:\n  - key: base_url\n    value: 'http://localhost:48131'\n  - key: run_id\n    value: '${Date.now()}'\n  - key: demo_password\n    value: 'Demo@2026!'\n    description: 'Public synthetic seed password, never a production credential.'\nscripts:\n  - type: http:beforeRequest\n    language: text/javascript\n    code: |-\n      const base = pm.variables.get('base_url');\n      if (!/^http:\\/\\/(localhost|127\\.0\\.0\\.1):[0-9]+$/.test(base)) throw new Error('Acceptance writes require an isolated loopback test site.');\n      const cookie = pm.collectionVariables.get('session_cookie');\n      if (cookie) pm.request.headers.upsert({key: 'Cookie', value: cookie});\n`);
let order = 0;
function request(name, method, url, status, body, checks = '', form = false) {
    order += 1000;
    let yaml = `$kind: http-request\nname: ${quote(name)}\norder: ${order}\nmethod: ${method}\nurl: ${quote('{{base_url}}' + url)}\nsettings:\n  followRedirects: false\n`;
    if (body !== undefined) {
        if (form) yaml += `body:\n  type: formdata\n  content:\n    - key: csvFile\n      type: file\n      src: 'scripts/verification/fixtures/role-import.csv'\n`;
        else yaml += `headers:\n  - key: Content-Type\n    value: application/json\nbody:\n  type: json\n  content: |-\n${JSON.stringify(body, null, 2).split('\n').map(line => '    ' + line).join('\n')}\n`;
    }
    const test = `pm.test('${name} status', () => pm.response.to.have.status(${status}));\n` + checks;
    yaml += 'scripts:\n';
    if (order === 1000) yaml += "  - type: beforeRequest\n    language: text/javascript\n    code: |-\n      pm.collectionVariables.set('run_id', String(Date.now()));\n      pm.collectionVariables.unset('session_cookie');\n      pm.request.headers.remove('Cookie');\n";
    yaml += `  - type: afterResponse\n    language: text/javascript\n    code: |-\n${test.trim().split('\n').map(line => '      ' + line).join('\n')}\n`;
    fs.writeFileSync(path.join(root, String(order).padStart(5, '0') + '-' + name.replace(/[^a-z0-9]+/gi, '-').toLowerCase() + '.request.yaml'), yaml);
}
function login(username, role) {
    request('Login ' + role, 'POST', '/api/auth/login', 200, { username, password: '{{demo_password}}' }, `pm.test('Expected session role', () => pm.expect(pm.response.json().role).to.equal('${role}'));\nconst cookie=pm.response.headers.get('Set-Cookie'); if (cookie) pm.collectionVariables.set('session_cookie',cookie.split(';')[0]);\n` + (role === 'SystemAdmin' ? "pm.collectionVariables.set('admin_id', pm.response.json().userId);" : ''));
}
request('Unauthenticated package lookup', 'GET', '/api/packages/scan/F20-0001', 401);
login('intake.demo', 'IntakeClerk');
request('Intake registration page', 'GET', '/Packages/Register', 200, undefined, "pm.test('UTF-8 dash', () => { pm.expect(pm.response.text()).to.include('— Select —'); pm.expect(pm.response.text()).not.to.include('â€'); });");
request('Configured fees', 'GET', '/api/packages/fees', 200, undefined, "const fees = pm.response.json(); pm.test('Fee keys are numeric', () => { pm.expect(fees.Personal).to.be.a('number'); pm.expect(fees.WorkRelated).to.be.a('number'); }); pm.collectionVariables.set('personal_fee', fees.Personal);");
request('Register synthetic package', 'POST', '/api/packages', 201, { recipient: { fullName: 'Synthetic Acceptance', identifierNo: 'QA-{{run_id}}', email: 'acceptance@example.test', phone: '0001234567', department: 'Synthetic' }, senderName: 'Synthetic Sender', packageType: 'Box', classification: 'Personal', storageLocationId: 1, notes: 'Isolated acceptance only' }, "const data=pm.response.json(); pm.test('Saved fee and initial status', () => { pm.expect(data.fee).to.equal(Number(pm.collectionVariables.get('personal_fee'))); pm.expect(data.status).to.equal('Registered'); }); pm.collectionVariables.set('package_id', data.f20Identifier);");
request('Intake label page', 'GET', '/Packages/Label/{{package_id}}', 200);
request('Intake users forbidden', 'GET', '/api/users', 403);
request('Intake reports forbidden', 'GET', '/api/reports', 403);
login('storage.demo', 'StorageStaff');
request('Storage scan lookup', 'GET', '/api/packages/scan/{{package_id}}', 200, undefined, "pm.test('Same registered identifier', () => pm.expect(pm.response.json().f20Identifier).to.equal(pm.collectionVariables.get('package_id')));");
request('Storage label forbidden', 'GET', '/Packages/Label/{{package_id}}', 403);
request('Storage detail hides label', 'GET', '/Packages/Detail/{{package_id}}', 200, undefined, "pm.test('Forbidden label action hidden', () => pm.expect(pm.response.text()).not.to.include('id=\"detail-label-link\"'));");
request('Store package', 'POST', '/api/packages/{{package_id}}/status', 200, { newStatus: 'InStorage', storageLocationId: 1 }, "pm.test('Stored status', () => pm.expect(pm.response.json().status).to.equal('InStorage'));");
request('Mark ready', 'POST', '/api/packages/{{package_id}}/status', 200, { newStatus: 'ReadyForCollection' }, "pm.test('Ready status', () => pm.expect(pm.response.json().status).to.equal('ReadyForCollection'));");
request('Storage registration forbidden', 'GET', '/Packages/Register', 403);
request('Storage collection forbidden', 'POST', '/api/packages/{{package_id}}/collect', 403, {});
login('collection.demo', 'CollectionStaff');
request('Collection page', 'GET', '/Collection', 200);
request('Collect package', 'POST', '/api/packages/{{package_id}}/collect', 200, {}, "pm.test('Collected timestamp', () => { const data=pm.response.json(); pm.expect(data.status).to.equal('Collected'); pm.expect(data.collectedAtUtc).to.be.a('string'); });");
request('Record payment', 'PATCH', '/api/packages/{{package_id}}/payment', 200, { paymentStatus: 'Paid' }, "pm.test('Payment stored with actor and time', () => { const data=pm.response.json(); pm.expect(data.paymentStatus).to.equal('Paid'); pm.expect(data.paymentStatusUpdatedBy).to.equal('collection.demo'); pm.expect(data.paymentStatusUpdatedAtUtc).to.be.a('string'); });");
request('Reload package detail', 'GET', '/api/packages/{{package_id}}/detail', 200, undefined, "pm.test('Collected payment persists', () => { const data=pm.response.json(); pm.expect(data.package.status).to.equal('Collected'); pm.expect(data.package.paymentStatus).to.equal('Paid'); });");
request('Collection storage forbidden', 'POST', '/api/packages/{{package_id}}/status', 403, { newStatus: 'InStorage' });
login('supervisor.demo', 'Supervisor');
request('Supervisor report data', 'GET', '/api/reports', 200, undefined, "pm.test('Report sections', () => { const data=pm.response.json(); pm.expect(data).to.have.property('kpis'); pm.expect(data.outstanding).to.be.an('array'); });");
request('Supervisor audit data', 'GET', '/api/audit-log?page=1&pageSize=10', 200, undefined, "pm.test('Paged audit shape', () => pm.expect(pm.response.json().items).to.be.an('array'));");
request('Supervisor audit page', 'GET', '/AuditLog', 200);
request('Supervisor CSV import', 'POST', '/api/import/csv', 200, true, "pm.test('Saved rows and rejected row', () => { const data=pm.response.json(); pm.expect(data.importedCount).to.equal(2); pm.expect(data.failedRows).to.have.lengthOf(1); });", true);
request('Supervisor users forbidden', 'GET', '/api/users', 403);
request('Supervisor storage mutation forbidden', 'POST', '/api/storage-locations', 403, { code: 'QA-{{run_id}}', description: 'Synthetic' });
login('admin.demo', 'SystemAdmin');
request('Admin users', 'GET', '/api/users', 200, undefined, "pm.test('No password hashes exposed', () => { const users=pm.response.json(); pm.expect(users).to.be.an('array'); users.forEach(u => pm.expect(u).not.to.have.property('passwordHash')); });");
request('Create synthetic user', 'POST', '/api/users', 201, { username: 'qa.{{run_id}}', email: 'qa.{{run_id}}@example.test', password: 'Synthetic123!', role: 'IntakeClerk' }, "pm.collectionVariables.set('created_user',pm.response.json().userId);");
request('Change created role', 'PATCH', '/api/users/{{created_user}}', 200, { role: 'StorageStaff' }, "pm.test('Role persisted', () => pm.expect(pm.response.json().role).to.equal('StorageStaff'));");
request('Deactivate created user', 'PATCH', '/api/users/{{created_user}}', 200, { isActive: false }, "pm.test('Deactivated', () => pm.expect(pm.response.json().isActive).to.equal(false));");
request('Reactivate created user', 'PATCH', '/api/users/{{created_user}}', 200, { isActive: true }, "pm.test('Reactivated', () => pm.expect(pm.response.json().isActive).to.equal(true));");
request('Create synthetic location', 'POST', '/api/storage-locations', 201, { code: 'QA-{{run_id}}', description: 'Synthetic acceptance location' });
request('Logout', 'POST', '/api/auth/logout', 204);
console.log('Generated ' + order / 1000 + ' local acceptance requests at ' + root);
