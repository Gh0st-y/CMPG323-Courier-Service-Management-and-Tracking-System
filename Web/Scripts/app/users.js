document.addEventListener('DOMContentLoaded', function () {
    const root = document.getElementById('user-mgmt');
    if (!root) return;

    const apiBase = root.dataset.apiBase;                       // e.g. "/api" or "/MyApp/api"
    const currentUserId = parseInt(root.dataset.currentUserId, 10) || 0;
    const roles = window.USER_ROLES || [];
    const tbody = document.getElementById('users-body');

    // Small local helper: GET/POST/PATCH to /api/users, same-origin so the session cookie goes with it.
    // Rejects with { status, error } like CourierApp.api does. ⭐ see footnote 2.
    function api(method, path, body) {
        return fetch(apiBase + path, {
            method: method,
            credentials: 'same-origin',
            headers: { 'Content-Type': 'application/json', 'Accept': 'application/json' },
            body: body === undefined ? undefined : JSON.stringify(body)
        }).then(function (r) {
            return r.json().catch(function () { return null; }).then(function (data) {
                if (!r.ok) return Promise.reject({ status: r.status, error: data && data.error });
                return data;
            });
        });
    }

    function showFailure(failure) {
        var message;
        if (failure && failure.status === 401) {
            message = 'Your session has expired. Please log in again.';
        } else if (failure && failure.status === 403) {
            message = 'You do not have permission to do this.';
        } else {
            message = (failure && failure.error && failure.error.message) || 'Something went wrong. Please try again.';
        }
        CourierApp.toast.error(message);
    }

    function formatLogin(value) {
        return value ? value.replace('T', ' ').replace('Z', ' UTC') : 'Never';
    }

    function cell(text) {
        var td = document.createElement('td');
        td.style.padding = '8px';
        td.textContent = text;     // textContent, never innerHTML, so user data can't inject HTML
        return td;
    }

    function loadUsers() {
        api('GET', '/users').then(function (users) {
            tbody.innerHTML = '';
            users.forEach(function (u) { tbody.appendChild(buildRow(u)); });
            if (users.length === 0) {
                var tr = document.createElement('tr');
                var td = cell('No users found.');
                td.colSpan = 6;
                tr.appendChild(td);
                tbody.appendChild(tr);
            }
        }, showFailure);
    }

    function buildRow(u) {
        var isSelf = u.userId === currentUserId;
        var tr = document.createElement('tr');
        tr.style.borderBottom = '1px solid #eee';

        tr.appendChild(cell(u.username));
        tr.appendChild(cell(u.email));

        // Role: dropdown for others, plain text for yourself
        var roleTd = cell('');
        if (isSelf) {
            roleTd.textContent = u.role;
        } else {
            var select = document.createElement('select');
            roles.forEach(function (r) {
                var opt = document.createElement('option');
                opt.value = r;
                opt.textContent = r;
                if (r === u.role) opt.selected = true;
                select.appendChild(opt);
            });
            select.addEventListener('change', function () {
                if (!confirm('Change ' + u.username + ' to ' + select.value + '?')) {
                    select.value = u.role;
                    return;
                }
                api('PATCH', '/users/' + u.userId, { role: select.value }).then(function () {
                    CourierApp.toast.success('Role updated.');
                    loadUsers();
                }, function (f) { showFailure(f); select.value = u.role; });
            });
            roleTd.appendChild(select);
        }
        tr.appendChild(roleTd);

        tr.appendChild(cell(u.isActive ? 'Active' : 'Inactive'));
        tr.appendChild(cell(formatLogin(u.lastLoginUtc)));

        // Actions: deactivate / reactivate (not on your own row)
        var actionTd = cell('');
        if (!isSelf) {
            var btn = document.createElement('button');
            btn.type = 'button';
            btn.className = 'btn btn-secondary btn-sm';
            btn.textContent = u.isActive ? 'Deactivate' : 'Activate';
            btn.addEventListener('click', function () {
                if (!confirm((u.isActive ? 'Deactivate ' : 'Activate ') + u.username + '?')) return;
                api('PATCH', '/users/' + u.userId, { isActive: !u.isActive }).then(function () {
                    CourierApp.toast.success(u.isActive ? 'User deactivated.' : 'User activated.');
                    loadUsers();
                }, showFailure);
            });
            actionTd.appendChild(btn);
        }
        tr.appendChild(actionTd);
        return tr;
    }

    // Create user
    document.getElementById('btn-create-user').addEventListener('click', function () {
        var username = document.getElementById('new-username').value.trim();
        var email = document.getElementById('new-email').value.trim();
        var password = document.getElementById('new-password').value;
        var role = document.getElementById('new-role').value;

        if (!username || !email || !password || !role) {
            CourierApp.toast.error('Username, email, password and role are all required.');
            return;
        }

        api('POST', '/users', { username: username, email: email, password: password, role: role })
            .then(function () {
                CourierApp.toast.success('User created.');
                if (window.closeCreateUserPanel) window.closeCreateUserPanel();
                loadUsers();
            }, showFailure);
    });

    loadUsers();
});