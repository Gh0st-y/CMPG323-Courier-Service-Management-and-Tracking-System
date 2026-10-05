/*
 * T10 spike: USB HID-mode scan and phone-camera QR scan, both calling
 * GET /api/packages/scan/{f20Identifier} (docs/API_CONTRACT.md) via CourierApp.api.
 * Findings: docs/SCANNING_SPIKE.md.
 */
(function () {
    "use strict";

    var resultEl = document.getElementById("scan-result");
    var html5QrCode = null;
    var resultCardEl = document.getElementById("scan-result-card") || resultEl.parentElement;
    var currentPackage = null;

    // The 4-state lifecycle (DECISIONS.md #7). The server enforces it (DR-009); this only decides which options to offer.
    // Collection is its own screen with its own role, so it is never offered here.
    var STATUS_LABELS = {
        Registered: "Registered",
        InStorage: "In Storage",
        ReadyForCollection: "Ready for Collection",
        Collected: "Collected"
    };
    var NEXT_STATUS = {
        Registered: ["InStorage"],
        InStorage: ["ReadyForCollection"],
        ReadyForCollection: [],
        Collected: []
    };

    function escapeHtml(value) {
        return String(value == null ? "" : value)
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;")
            .replace(/'/g, "&#39;");
    }

    // "ReadyForCollection" or "Ready for Collection" -> the key used above
    function statusKey(status) {
        return (status || "").toString().replace(/\s+/g, "").toLowerCase();
    }

    function canonicalStatus(status) {
        var key = statusKey(status);
        for (var name in STATUS_LABELS) {
            if (name.toLowerCase() === key) {
                return name;
            }
        }
        return null;
    }

    function statusLabel(status) {
        var canonical = canonicalStatus(status);
        return canonical ? STATUS_LABELS[canonical] : status;
    }

    // =========================================================================
    // [T35 HELPERS] These functions handle audio feedback and UI card updates
    // =========================================================================
    function playAudioFeedback(type) {
        try {
            var AudioContext = window.AudioContext || window.webkitAudioContext;
            if (!AudioContext) return;
            var ctx = new AudioContext();
            var osc = ctx.createOscillator();
            var gain = ctx.createGain();

            osc.connect(gain);
            gain.connect(ctx.destination);

            if (type === "success") {
                osc.type = "sine";
                osc.frequency.setValueAtTime(880, ctx.currentTime); // High-pitched beep
                gain.gain.setValueAtTime(0.1, ctx.currentTime);
                osc.start();
                osc.stop(ctx.currentTime + 0.15);
            } else {
                osc.type = "sawtooth";
                osc.frequency.setValueAtTime(220, ctx.currentTime); // Low error tone
                gain.gain.setValueAtTime(0.15, ctx.currentTime);
                osc.start();
                osc.stop(ctx.currentTime + 0.25);
            }
        } catch (e) {
            // Audio Context muted or restricted by browser policy
        }
    }

    function renderPackageUI(pkg, source, isFallbackMatch) {
        playAudioFeedback("success");

        if (resultCardEl) {
            resultCardEl.style.border = "2px solid #28a745";
            resultCardEl.style.boxShadow = "0 0 10px rgba(40, 167, 69, 0.4)";
        }

        var sourceLabel = isFallbackMatch ? source + " (Matched via Search)" : source;
        // Everything here comes from the server or the scanner, so it is escaped: a recipient name must never run as script.
        var html = "<div>" +
            "<div><strong>Source:</strong> " + escapeHtml(sourceLabel) + "</div>" +
            "<div><strong>Package ID:</strong> " + escapeHtml(pkg.f20Identifier || pkg.packageId || pkg.code || "N/A") + "</div>" +
            "<div><strong>Recipient:</strong> " + escapeHtml(pkg.recipientName || "N/A") + "</div>" +
            "<div><strong>Status:</strong> " + escapeHtml(statusLabel(pkg.status) || "N/A") + "</div>" +
            "<div><strong>Storage Location:</strong> " + escapeHtml(pkg.storageLocation || "N/A") + "</div>" +
            "</div>";

        resultEl.innerHTML = html;
        // Show and populate the status update UI card
        setupStatusUpdateUI(pkg);
    }

    function renderErrorUI(source, message) {
        playAudioFeedback("error");

        if (resultCardEl) {
            resultCardEl.style.border = "2px solid #dc3545";
            resultCardEl.style.boxShadow = "0 0 10px rgba(220, 53, 69, 0.4)";
        }

        resultEl.innerHTML = "<div style='color: #dc3545;'><strong>" + escapeHtml(source) + " Error:</strong> " + escapeHtml(message) + "</div>";

        // Nothing was found, so there is nothing to update
        var updateCard = document.getElementById("status-update-card");
        if (updateCard) {
            updateCard.style.display = "none";
        }
        currentPackage = null;
    }

    function showNotFound(cleanInput, source) {
        var message = "No package was found for \"" + cleanInput + "\".";
        CourierApp.toast.error(message);
        renderErrorUI(source, message);
    }

    // Both the USB scanner and the camera end up here (IR-007): they only differ in how the text arrived.
    function lookup(f20Identifier, source) {
        var cleanInput = (f20Identifier || "").trim().toUpperCase();
        resultEl.textContent = "Looking up " + cleanInput + " (via " + source + ")...";

        // silent: this screen reports the outcome itself, so the wrapper must not also toast
        CourierApp.api.get("/packages/scan/" + encodeURIComponent(cleanInput), { silent: true }).then(function (pkg) {
            var returnedId = ((pkg && pkg.f20Identifier) || "").toString().toUpperCase();

            if (!pkg || pkg.error || (returnedId && returnedId !== cleanInput)) {
                showNotFound(cleanInput, source);
                return;
            }

            CourierApp.toast.success("Found " + cleanInput + " (" + source + ")");
            renderPackageUI(pkg, source, false);
        }, function (failure) {
            // Only a 404 means "no such package". Anything else (not logged in, server trouble) says what really happened.
            if (failure && failure.status === 404) {
                showNotFound(cleanInput, source);
                return;
            }

            var message = (failure && failure.error && failure.error.message) || "Could not look that package up. Please try again.";
            CourierApp.toast.error(message);
            renderErrorUI(source, message);
        });
    }

    // ---------------------------------------------------------------------
    // (a) USB HID-mode scanner: a focused input that a scanner "types" into,
    // terminated by Enter (the default suffix on most barcode scanner configs).
    // ---------------------------------------------------------------------

    var usbInput = document.getElementById("usb-scan-input");

    function focusUsbInput() {
        usbInput.focus();
    }

    usbInput.addEventListener("keydown", function (event) {
        if (event.key !== "Enter") {
            return;
        }
        event.preventDefault();

        var value = usbInput.value.trim();
        usbInput.value = "";

        if (value) {
            lookup(value, "USB scan");
        }
    });

    document.getElementById("usb-refocus").addEventListener("click", focusUsbInput);
    focusUsbInput();

    // ---------------------------------------------------------------------
    // (b) Phone camera QR scan via html5-qrcode. getUserMedia needs a secure
    // context (HTTPS, or http://localhost) — flag it up front instead of
    // letting the camera silently fail to start.
    // ---------------------------------------------------------------------

    var secureWarningEl = document.getElementById("qr-secure-warning");
    var startBtn = document.getElementById("qr-start");
    var stopBtn = document.getElementById("qr-stop");

    if (!window.isSecureContext) {
        secureWarningEl.style.display = "block";
        startBtn.disabled = true;
    }

    startBtn.addEventListener("click", function () {
        if (typeof Html5Qrcode === "undefined") {
            CourierApp.toast.error("html5-qrcode failed to load (offline / CDN blocked?).");
            return;
        }

        html5QrCode = new Html5Qrcode("qr-reader");
        startBtn.disabled = true;

        html5QrCode.start(
            { facingMode: "environment" },
            { fps: 10, qrbox: { width: 250, height: 250 } },
            function onScanSuccess(decodedText) {
                lookup(decodedText, "QR scan");
                stopScanning();
            }
            // No onScanFailure handler — html5-qrcode calls that on every frame
            // with no QR code in view, which is expected and not an error.
        ).then(function () {
            stopBtn.disabled = false;
        }).catch(function (err) {
            startBtn.disabled = false;
            CourierApp.toast.error("Could not start the camera: " + err);
        });
    });

    var mobileStartBtn = document.getElementById("qr-start-mobile");

    if (mobileStartBtn) {
        mobileStartBtn.addEventListener("click", function () {
            if (typeof Html5Qrcode === "undefined") {
                CourierApp.toast.error("html5-qrcode failed to load (offline / CDN blocked?).");
                return;
            }

            // Check if the user is on a mobile device
            var isMobileDevice = /Android|webOS|iPhone|iPad|iPod|BlackBerry|IEMobile|Opera Mini/i.test(navigator.userAgent);

            if (!isMobileDevice) {
                CourierApp.toast.error("Mobile camera scan is only available on mobile phones or tablets. Please use 'Start camera scan' or open this site on your mobile phone.");
                return;
            }

            html5QrCode = new Html5Qrcode("qr-reader");
            if (startBtn) startBtn.disabled = true;
            mobileStartBtn.disabled = true;

            html5QrCode.start(
                { facingMode: { exact: "environment" } }, // Forces rear camera on mobile
                { fps: 10, qrbox: { width: 250, height: 250 } },
                function onScanSuccess(decodedText) {
                    lookup(decodedText, "Mobile QR scan");
                    stopScanning();
                }
            ).then(function () {
                if (stopBtn) stopBtn.disabled = false;
            }).catch(function (err) {
                // Soft fallback to standard environment preference on mobile devices
                html5QrCode.start(
                    { facingMode: "environment" },
                    { fps: 10, qrbox: { width: 250, height: 250 } },
                    function onScanSuccess(decodedText) {
                        lookup(decodedText, "Mobile QR scan");
                        stopScanning();
                    }
                ).catch(function (fallbackErr) {
                    if (startBtn) startBtn.disabled = false;
                    mobileStartBtn.disabled = false;
                    CourierApp.toast.error("Could not start mobile camera: " + fallbackErr);
                });
            });
        });
    }

    // ---------------------------------------------------------------------
    // Status update (T19): moves the scanned package to its next status with
    // POST /api/packages/{id}/status. The server decides whether the move is
    // allowed (DR-009); the dropdown only offers the next step so staff don't
    // have to guess.
    // ---------------------------------------------------------------------

    var storageLocations = null;

    function ensureStorageLocations(done) {
        if (storageLocations) {
            done();
            return;
        }

        CourierApp.api.get("/storage-locations", { silent: true }).then(function (list) {
            storageLocations = Array.isArray(list) ? list : [];
            done();
        }, function () {
            storageLocations = [];
            done();
        });
    }

    function fillLocationOptions(selectedId) {
        var select = document.getElementById("location-select");
        if (!select) return;

        select.innerHTML = "";

        var keep = document.createElement("option");
        keep.value = "";
        keep.textContent = "(keep current location)";
        select.appendChild(keep);

        storageLocations.forEach(function (location) {
            var option = document.createElement("option");
            option.value = location.storageLocationId;
            option.textContent = location.code;
            if (selectedId != null && location.storageLocationId === selectedId) {
                option.selected = true;
            }
            select.appendChild(option);
        });
    }

    function setupStatusUpdateUI(pkg) {
        if (!pkg) return;

        currentPackage = pkg;
        var updateCard = document.getElementById("status-update-card");
        var statusSelect = document.getElementById("status-select");
        var noteEl = document.getElementById("status-update-note");
        var confirmButton = document.getElementById("confirm-status-update");

        if (!updateCard || !statusSelect) {
            return;
        }

        var current = canonicalStatus(pkg.status);
        var next = current ? NEXT_STATUS[current] : [];

        statusSelect.innerHTML = "";
        next.forEach(function (name) {
            var option = document.createElement("option");
            option.value = name;
            option.textContent = STATUS_LABELS[name];
            statusSelect.appendChild(option);
        });

        var canUpdate = next.length > 0;
        statusSelect.disabled = !canUpdate;
        if (confirmButton) confirmButton.disabled = !canUpdate;

        if (noteEl) {
            noteEl.textContent = !current ? "This package has a status this screen doesn't recognise."
                : current === "ReadyForCollection" ? "Ready for collection. It is handed over on the Collection screen."
                : current === "Collected" ? "This package has already been collected."
                : "";
        }

        updateCard.style.display = "block";
        ensureStorageLocations(function () {
            fillLocationOptions(pkg.storageLocationId);
        });
    }

    var confirmStatusButton = document.getElementById("confirm-status-update");

    if (confirmStatusButton) {
        confirmStatusButton.addEventListener("click", function () {
            if (!currentPackage) {
                CourierApp.toast.error("Scan a package first.");
                return;
            }

            var packageId = currentPackage.f20Identifier || currentPackage.packageId;
            var statusSelect = document.getElementById("status-select");
            var locationSelect = document.getElementById("location-select");
            var newStatus = statusSelect ? statusSelect.value : "";

            if (!newStatus) {
                return;
            }

            var body = { newStatus: newStatus };
            if (locationSelect && locationSelect.value) {
                body.storageLocationId = parseInt(locationSelect.value, 10);
            }

            confirmStatusButton.disabled = true;
            confirmStatusButton.textContent = "Updating...";

            CourierApp.api.post("/packages/" + encodeURIComponent(packageId) + "/status", body).then(function (updated) {
                // The real API returns the whole updated record. Mock mode returns a stub, so fall back to what we know.
                var record = (updated && updated.f20Identifier)
                    ? updated
                    : Object.assign({}, currentPackage, { status: (updated && updated.status) || newStatus });

                CourierApp.toast.success("Status updated to " + statusLabel(record.status) + ".");
                renderPackageUI(record, "Status Update", false);
            }, function (failure) {
                // CourierApp.api has already shown the server's reason (for example why that move isn't allowed).
                // If someone else changed the package first (409), reload it so the options match what is true now.
                if (failure && failure.status === 409) {
                    lookup(packageId, "Reload");
                }
            }).then(function () {
                confirmStatusButton.textContent = "Confirm Update";
                confirmStatusButton.disabled = !statusSelect || statusSelect.options.length === 0;
            });
        });
    }

    function stopScanning() {
        if (!html5QrCode) {
            return;
        }
        html5QrCode.stop().then(function () {
            html5QrCode.clear();
            html5QrCode = null;
            if (startBtn) startBtn.disabled = false;
            if (mobileStartBtn) mobileStartBtn.disabled = false;
            if (stopBtn) stopBtn.disabled = true;
        });
    }

    if (stopBtn) {
        stopBtn.addEventListener("click", stopScanning);
    }
})();