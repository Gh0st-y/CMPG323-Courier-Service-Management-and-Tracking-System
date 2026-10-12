document.addEventListener('DOMContentLoaded', function () {
    "use strict";
    const form = document.getElementById('csv-upload-form');
    if (!form) return;
    const fileInput = document.getElementById('csvFile');
    const button = document.getElementById('btn-upload');
    const error = document.getElementById('file-error');
    const summary = document.getElementById('import-summary-card');
    const failedContainer = document.getElementById('failed-rows-container');
    const failedBody = document.getElementById('failed-rows-tbody');

    form.addEventListener('submit', function (event) {
        event.preventDefault();
        if (button.disabled) return;
        error.textContent = '';
        summary.style.display = 'none';
        const file = fileInput.files && fileInput.files[0];
        if (!file || !/\.csv$/i.test(file.name)) {
            error.textContent = 'Please select a CSV file.';
            return;
        }
        if (CourierApp.config.mockMode) {
            error.textContent = 'Turn off mock mode on the Dashboard before importing packages.';
            return;
        }
        const body = new FormData();
        body.append('csvFile', file);
        button.disabled = true;
        button.textContent = 'Importing...';
        CourierApp.api.post('/import/csv', body, { silent: true })
            .then(function (result) {
                if (!result || !Number.isInteger(result.importedCount) || result.importedCount < 0 ||
                    !Array.isArray(result.failedRows)) {
                    throw new Error('The server returned an unexpected result. Check package records before retrying.');
                }
                document.getElementById('stat-total').textContent = result.importedCount + result.failedRows.length;
                document.getElementById('stat-succeeded').textContent = result.importedCount;
                document.getElementById('stat-failed').textContent = result.failedRows.length;
                document.getElementById('import-timestamp').textContent = 'Completed at: ' + new Date().toLocaleTimeString();
                failedBody.textContent = '';
                result.failedRows.forEach(function (failure) {
                    const row = document.createElement('tr');
                    [failure.row, failure.reason].forEach(function (value) {
                        const cell = document.createElement('td');
                        cell.textContent = value;
                        row.appendChild(cell);
                    });
                    failedBody.appendChild(row);
                });
                failedContainer.style.display = result.failedRows.length ? 'block' : 'none';
                summary.style.display = 'block';
                CourierApp.toast.success(result.importedCount + ' imported, ' + result.failedRows.length + ' failed validation.');
            })
            .catch(function (failure) {
                const uncertain = failure && (failure.status === 0 || failure.status >= 500);
                const message = uncertain
                    ? 'Import completion could not be confirmed. Check package records before retrying to avoid duplicates.'
                    : (failure && failure.error && failure.error.message) || failure.message || 'Import failed.';
                error.textContent = message;
                CourierApp.toast.error(message);
            })
            .finally(function () {
                button.disabled = false;
                button.textContent = 'Upload & Process CSV';
            });
    });
});
