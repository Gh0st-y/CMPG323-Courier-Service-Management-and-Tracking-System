document.addEventListener('DOMContentLoaded', function () {
    const lookupInput = document.getElementById('package-lookup-input');
    const btnLookup = document.getElementById('btn-lookup');
    const btnTriggerScan = document.getElementById('btn-trigger-scan');
    const btnStopScan = document.getElementById('btn-stop-scan');
    const scannerContainer = document.getElementById('scanner-container');
    const detailsCard = document.getElementById('collection-details-card');
    const blockedAlert = document.getElementById('status-blocked-alert');
    const chkVerifyId = document.getElementById('chk-verify-id');
    const btnConfirmCollection = document.getElementById('btn-confirm-collection');
    const confirmModal = document.getElementById('collection-confirm-modal');
    const btnModalCancel = document.getElementById('btn-modal-cancel');
    const btnModalSubmit = document.getElementById('btn-modal-submit');

    // "InStorage" -> "In Storage", as the rest of the app shows it
    function statusLabel(status) {
        return ({ InStorage: "In Storage", ReadyForCollection: "Ready for Collection" })[status] || status;
    }

    let currentPackage = null;
    let html5QrCode = null;
    let resetTimer = null;

    // Helper function to update button styles for enabled/disabled states
    function setConfirmButtonState(isEnabled) {
        btnConfirmCollection.disabled = !isEnabled;
        if (isEnabled) {
            btnConfirmCollection.style.backgroundColor = '#6f42c1'; // Primary purple
            btnConfirmCollection.style.borderColor = '#6f42c1';
            btnConfirmCollection.style.color = '#ffffff';
            btnConfirmCollection.style.opacity = '1';
            btnConfirmCollection.style.cursor = 'pointer';
        } else {
            btnConfirmCollection.style.backgroundColor = '#6c757d'; // Disabled grey
            btnConfirmCollection.style.borderColor = '#6c757d';
            btnConfirmCollection.style.color = '#e0e0e0';
            btnConfirmCollection.style.opacity = '0.55';
            btnConfirmCollection.style.cursor = 'not-allowed';
        }
    }

    btnLookup.addEventListener('click', performLookup);
    lookupInput.addEventListener('keypress', function (e) {
        if (e.key === 'Enter') {
            e.preventDefault();
            performLookup();
        }
    });

    function performLookup() {
        const pkgId = lookupInput.value.trim();

        // Check if input is empty
        if (!pkgId) {
            // Hide the details card if one was previously shown
            detailsCard.style.display = 'none';

            // Trigger toast error notification if CourierApp is available
            if (window.CourierApp && window.CourierApp.toast) {
                window.CourierApp.toast.error("Please enter a Package ID or Barcode.");
            } else {
                alert("Please enter a Package ID or Barcode.");
            }

            // Focus back on the input field
            lookupInput.focus();
            return;
        }

        // Clear any active auto-hide timer when performing a new search
        if (resetTimer) {
            clearTimeout(resetTimer);
            resetTimer = null;
        }

        fetchPackageDetails(pkgId);
    }

    // Camera Scan Trigger using Html5Qrcode pattern
    btnTriggerScan.addEventListener('click', function () {
        if (typeof Html5Qrcode === "undefined") {
            if (window.CourierApp && window.CourierApp.toast) {
                window.CourierApp.toast.error("Html5Qrcode library failed to load.");
            }
            return;
        }

        scannerContainer.style.display = 'block';
        btnTriggerScan.disabled = true;

        if (!html5QrCode) {
            html5QrCode = new Html5Qrcode("qr-reader");
        }

        html5QrCode.start(
            { facingMode: "environment" },
            { fps: 10, qrbox: { width: 250, height: 250 } },
            function onScanSuccess(scannedCode) {
                lookupInput.value = scannedCode;
                stopScanning();
                performLookup();
            }
        ).then(function () {
            if (btnStopScan) btnStopScan.disabled = false;
        }).catch(function (err) {
            btnTriggerScan.disabled = false;
            scannerContainer.style.display = 'none';
            if (window.CourierApp && window.CourierApp.toast) {
                window.CourierApp.toast.error("Could not start camera: " + err);
            }
        });
    });

    btnStopScan.addEventListener('click', function () {
        stopScanning();
    });

    function stopScanning() {
        if (html5QrCode) {
            html5QrCode.stop().then(function () {
                html5QrCode.clear();
                scannerContainer.style.display = 'none';
                btnTriggerScan.disabled = false;
                if (btnStopScan) btnStopScan.disabled = true;
            }).catch(function (err) {
                console.error("Error stopping scanner: ", err);
                scannerContainer.style.display = 'none';
                btnTriggerScan.disabled = false;
            });
        } else {
            scannerContainer.style.display = 'none';
            btnTriggerScan.disabled = false;
        }
    }

    function fetchPackageDetails(packageId) {
        // The same lookup the scan screen uses (GET /api/packages/scan/{id}). silent: this screen words its own errors.
        CourierApp.api.get('/packages/scan/' + encodeURIComponent(packageId), { silent: true })
            .then(data => {
                currentPackage = data;
                renderPackageDetails(data);
            }, failure => {
                detailsCard.style.display = 'none';
                currentPackage = null;

                // Only a 404 means there is no such package. Anything else (not logged in, server trouble) says what happened.
                const message = failure && failure.status === 404
                    ? 'No package was found for that code.'
                    : (failure && failure.error && failure.error.message) || 'Could not retrieve the package. Please try again.';
                CourierApp.toast.error(message);
            });
    }
    function renderPackageDetails(pkg, isJustCollected = false) {
        detailsCard.style.display = 'block';

        // Display the F20 identifier (e.g., "F20-0007")
        const displayId = pkg.f20Identifier || pkg.packageId;
        document.getElementById('display-package-id').textContent = displayId;
        document.getElementById('display-recipient-name').textContent = pkg.recipientName;
        document.getElementById('display-recipient-phone').textContent = pkg.recipientPhone || 'N/A';
        document.getElementById('display-storage-location').textContent = pkg.storageLocation || 'N/A';

        const statusElem = document.getElementById('display-package-status');
        statusElem.textContent = statusLabel(pkg.status);

        // Shared base box styles for padding, borders, and margins
        blockedAlert.style.padding = '12px 16px';
        blockedAlert.style.borderRadius = '6px';
        blockedAlert.style.marginTop = '1rem';
        blockedAlert.style.marginBottom = '1rem';
        blockedAlert.style.fontWeight = '500';

        if (isJustCollected) {
            // GREEN SUCCESS BOX (Freshly collected)
            blockedAlert.className = 'alert alert-success mt-3';
            blockedAlert.style.backgroundColor = '#d1e7dd';
            blockedAlert.style.borderColor = '#badbcc';
            blockedAlert.style.color = '#0f5132';
            blockedAlert.style.border = '1px solid #badbcc';
            blockedAlert.textContent = `Package ${displayId} has been successfully collected.`;
            blockedAlert.style.display = 'block';

            chkVerifyId.disabled = true;
            chkVerifyId.checked = false;
            setConfirmButtonState(false);

            // Auto-hide green box after 4s, clear input box, and refocus cursor
            resetTimer = setTimeout(function () {
                blockedAlert.style.display = 'none';
                lookupInput.value = '';
                lookupInput.focus();
            }, 4000);

        } else if (pkg.status === 'ReadyForCollection') {
            // READY FOR COLLECTION: Hide alert box
            blockedAlert.style.display = 'none';
            chkVerifyId.disabled = false;
            chkVerifyId.checked = false;
            setConfirmButtonState(false);

        } else {
            // RED WARNING BOX with status-specific messages
            blockedAlert.className = 'alert alert-danger mt-3';
            blockedAlert.style.backgroundColor = '#f8d7da';
            blockedAlert.style.borderColor = '#f5c2c7';
            blockedAlert.style.color = '#842029';
            blockedAlert.style.border = '1px solid #f5c2c7';
            blockedAlert.style.borderLeft = '4px solid #dc3545';

            // Dynamic message format: "{packageId} is {status} and cannot be updated"
            blockedAlert.textContent = `${displayId} is ${statusLabel(pkg.status)} and cannot be updated`;

            blockedAlert.style.display = 'block';

            chkVerifyId.disabled = true;
            chkVerifyId.checked = false;
            setConfirmButtonState(false);
        }
    }

    // AC: Staff verify identity toggle
    chkVerifyId.addEventListener('change', function () {
        if (currentPackage && currentPackage.status === 'ReadyForCollection') {
            setConfirmButtonState(this.checked);
        } else {
            setConfirmButtonState(false);
        }
    });

    // AC: Confirm dialog
    btnConfirmCollection.addEventListener('click', function () {
        const pkgId = currentPackage.f20Identifier || currentPackage.packageId || currentPackage.id;
        document.getElementById('modal-pkg-id').textContent = pkgId;
        confirmModal.style.display = 'flex';
    });

    btnModalCancel.addEventListener('click', function () {
        confirmModal.style.display = 'none';
    });

    // AC: Success message
    btnModalSubmit.addEventListener('click', function () {
        confirmModal.style.display = 'none';
        const pkgId = currentPackage.f20Identifier || currentPackage.packageId || currentPackage.id;

        // POST /api/packages/{id}/collect (T20). The server only allows it when the package is Ready for Collection,
        // records who verified and when, and answers with the reason if not. The logged-in user is the verifier.
        CourierApp.api.post('/packages/' + encodeURIComponent(pkgId) + '/collect', {})
            .then(() => {
                CourierApp.toast.success(`Package ${pkgId} marked as Collected!`);

                // Update current package status locally
                currentPackage.status = 'Collected';

                // Render UI with green alert flag (isJustCollected = true)
                renderPackageDetails(currentPackage, true);
            }, failure => {
                // CourierApp.api has already shown the server's reason as a toast. If the package changed under us
                // (409), reload it so the screen shows what is true now.
                if (failure && failure.status === 409) {
                    fetchPackageDetails(pkgId);
                }
            });
    });
});
