document.addEventListener('DOMContentLoaded', function () {
    const uploadForm = document.getElementById('csv-upload-form');
    const fileInput = document.getElementById('csvFile');
    const btnUpload = document.getElementById('btn-upload');
    const btnDownloadTemplate = document.getElementById('btn-download-template');
    const fileError = document.getElementById('file-error');

    const summaryCard = document.getElementById('import-summary-card');
    const statTotal = document.getElementById('stat-total');
    const statSucceeded = document.getElementById('stat-succeeded');
    const statFailed = document.getElementById('stat-failed');
    const timestampSpan = document.getElementById('import-timestamp');

    const successfulContainer = document.getElementById('successful-rows-container');
    const successfulTbody = document.getElementById('successful-rows-tbody');
    const failedContainer = document.getElementById('failed-rows-container');
    const failedTbody = document.getElementById('failed-rows-tbody');

    if (btnDownloadTemplate) {
        btnDownloadTemplate.addEventListener('click', function () {
            const csvHeaders = "TrackingNumber,RecipientName,RecipientEmail,RecipientPhone,StorageLocation,Fee\n";
            const sampleData =
                "F20-1001,Lerato Mahlangu,lerato.mahlangu@example.com,0781234567,Shelf A-3,10.00\n" +
                "F20-1002,Thabo Nkosi,thabo.nkosi@example.com,0829876543,Shelf B-1,0.00\n";

            const blob = new Blob([csvHeaders + sampleData], { type: 'text/csv;charset=utf-8;' });
            const url = URL.createObjectURL(blob);

            const tempLink = document.createElement('a');
            tempLink.href = url;
            tempLink.setAttribute('download', 'package-import-200-rows-dummy_3.csv');
            document.body.appendChild(tempLink);
            tempLink.click();
            document.body.removeChild(tempLink);
            URL.revokeObjectURL(url);
        });
    }

    if (!uploadForm) return;

    uploadForm.addEventListener('submit', function (e) {
        e.preventDefault();
        fileError.textContent = '';

        if (!fileInput.files || fileInput.files.length === 0) {
            fileError.textContent = 'Please select a .csv file to process.';
            return;
        }

        const file = fileInput.files[0];
        if (!file.name.endsWith('.csv')) {
            fileError.textContent = 'Invalid file type. Please upload a valid CSV file.';
            return;
        }

        if (window.CourierApp && CourierApp.loading) {
            CourierApp.loading.show();
        }
        btnUpload.disabled = true;
        btnUpload.textContent = 'Processing...';

        const reader = new FileReader();
        reader.onload = function (evt) {
            processCsvData(evt.target.result);
        };
        reader.onerror = function () {
            if (window.CourierApp && CourierApp.toast) {
                CourierApp.toast.error('Failed to read the local CSV file.');
            }
            resetButton();
        };

        reader.readAsText(file);
    });

    function processCsvData(csvText) {
        const lines = csvText.split(/\r\n|\n/).filter(line => line.trim() !== '');

        if (lines.length <= 1) {
            if (window.CourierApp && CourierApp.toast) {
                CourierApp.toast.error('CSV file is empty or contains only headers.');
            }
            resetButton();
            return;
        }

        const dataRows = lines.slice(1);
        let total = dataRows.length;
        let succeededCount = 0;
        let failedCount = 0;

        const successfulRows = [];
        const failedRows = [];

        const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
        const phoneRegex = /^0\d{9}$/;

        dataRows.forEach((row, idx) => {
            const rowNumber = idx + 2;
            const cols = row.split(',').map(c => c.trim());

            const trackingNum = cols[0] || '';
            const recipientName = cols[1] || '';
            const recipientEmail = cols[2] || '';
            const recipientPhone = cols[3] || '';
            const storageLoc = cols[4] || '';
            const feeRaw = cols[5] || '';

            const feeVal = parseFloat(feeRaw);

            // Validation flags
            const trackingValid = !!trackingNum;
            const nameValid = !!recipientName;
            const emailValid = !!recipientEmail && emailRegex.test(recipientEmail);
            const phoneValid = !!recipientPhone && phoneRegex.test(recipientPhone);
            const storageValid = !!storageLoc;
            const feeValid = !isNaN(feeVal) && feeVal >= 0;

            if (!trackingValid || !nameValid || !emailValid || !phoneValid || !storageValid || !feeValid) {
                failedCount++;
                let reason = [];
                if (!trackingValid) reason.push("Missing Tracking #");
                if (!nameValid) reason.push("Missing Recipient Name");
                if (!emailValid) reason.push(`Invalid Email ("${recipientEmail}")`);
                if (!phoneValid) reason.push(`Invalid Phone ("${recipientPhone}")`);
                if (!storageValid) reason.push("Missing Storage Location");
                if (isNaN(feeVal)) reason.push(`Non-numeric Fee ("${feeRaw}")`);
                else if (feeVal < 0) reason.push(`Negative Fee ("${feeRaw}")`);

                failedRows.push({
                    rowNumber,
                    trackingNum,
                    recipientName,
                    recipientEmail,
                    recipientPhone,
                    storageLoc,
                    feeRaw,
                    trackingValid,
                    nameValid,
                    emailValid,
                    phoneValid,
                    storageValid,
                    feeValid,
                    reason: reason.join(', ')
                });
            } else {
                succeededCount++;
                successfulRows.push({
                    rowNumber,
                    trackingNumber: trackingNum,
                    name: recipientName,
                    email: recipientEmail,
                    phone: recipientPhone,
                    location: storageLoc,
                    fee: `R${feeVal.toFixed(2)}`,
                    status: 'Registered'
                });
            }
        });

        statTotal.textContent = total;
        statSucceeded.textContent = succeededCount;
        statFailed.textContent = failedCount;
        timestampSpan.textContent = 'Processed at: ' + new Date().toLocaleTimeString();

        // Populate Successful Table
        successfulTbody.innerHTML = '';
        if (successfulRows.length > 0) {
            successfulRows.forEach(item => {
                const tr = document.createElement('tr');
                tr.className = 'results-table__row';
                tr.innerHTML = `
                    <td class="results-table__id">#${item.rowNumber}</td>
                    <td><strong>${escapeHtml(item.trackingNumber)}</strong></td>
                    <td>${escapeHtml(item.name)}</td>
                    <td>${escapeHtml(item.email)}</td>
                    <td>${escapeHtml(item.phone)}</td>
                    <td><span class="detail-status">${escapeHtml(item.location)}</span></td>
                    <td>${escapeHtml(item.fee)}</td>
                    <td><span class="results-table__status results-table__status--Registered">${escapeHtml(item.status)}</span></td>
                `;
                successfulTbody.appendChild(tr);
            });
            successfulContainer.style.display = 'block';
        } else {
            successfulContainer.style.display = 'none';
        }

        // Populate Failed Table with Visual Errors
        failedTbody.innerHTML = '';
        if (failedRows.length > 0) {
            failedRows.forEach(item => {
                const tr = document.createElement('tr');
                tr.className = 'results-table__row row-failed-styled';
                tr.innerHTML = `
                    <td class="results-table__id">#${item.rowNumber}</td>
                    <td class="${item.trackingValid ? '' : 'cell-invalid'}">${escapeHtml(item.trackingNum || '[MISSING]')}</td>
                    <td class="${item.nameValid ? '' : 'cell-invalid'}">${escapeHtml(item.recipientName || '[MISSING]')}</td>
                    <td class="${item.emailValid ? '' : 'cell-invalid'}">${escapeHtml(item.recipientEmail || '[MISSING]')}</td>
                    <td class="${item.phoneValid ? '' : 'cell-invalid'}">${escapeHtml(item.recipientPhone || '[MISSING]')}</td>
                    <td class="${item.storageValid ? '' : 'cell-invalid'}">${escapeHtml(item.storageLoc || '[MISSING]')}</td>
                    <td class="${item.feeValid ? '' : 'cell-invalid'}">${escapeHtml(item.feeRaw || '[MISSING]')}</td>
                    <td class="reason-cell">${escapeHtml(item.reason)}</td>
                `;
                failedTbody.appendChild(tr);
            });
            failedContainer.style.display = 'block';
        } else {
            failedContainer.style.display = 'none';
        }

        summaryCard.style.display = 'block';

        if (window.CourierApp && CourierApp.toast) {
            CourierApp.toast.success(`Import complete: ${succeededCount} imported, ${failedCount} failed.`);
        }

        resetButton();
    }

    function resetButton() {
        btnUpload.disabled = false;
        btnUpload.textContent = 'Upload & Process CSV';
        if (window.CourierApp && CourierApp.loading) {
            CourierApp.loading.hide();
        }
    }

    function escapeHtml(str) {
        return String(str)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;');
    }
});