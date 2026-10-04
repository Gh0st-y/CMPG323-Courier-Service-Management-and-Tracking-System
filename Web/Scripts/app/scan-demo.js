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
        var html = "<div>" +
            "<div><strong>Source:</strong> " + sourceLabel + "</div>" +
            "<div><strong>Package ID:</strong> " + (pkg.f20Identifier || pkg.packageId || pkg.code || "N/A") + "</div>" +
            "<div><strong>Recipient:</strong> " + (pkg.recipientName || "N/A") + "</div>" +
            "<div><strong>Status:</strong> " + (pkg.status || "Ready") + "</div>" +
            "<div><strong>Storage Location:</strong> " + (pkg.storageLocation || "N/A") + "</div>" +
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

        resultEl.innerHTML = "<div style='color: #dc3545;'><strong>" + source + " Error:</strong> " + message + "</div>";
    }

    function handleNotFound(f20Identifier, source, customReason) {
        var cleanInput = (f20Identifier || "").trim().toUpperCase();
        var reasonText = customReason || ("Package code \"" + cleanInput + "\" was not found at standard endpoint.");

        resultEl.textContent = source + " -> " + reasonText + " Searching all package records for a match...";
        CourierApp.toast.error("Package \"" + cleanInput + "\" not found. Searching alternative records...");

        // Search across package list dynamically without hardcoded names/data
        CourierApp.api.get("/packages").then(function (packages) {
            var match = null;

            if (Array.isArray(packages)) {
                match = packages.find(function (p) {
                    var identifier = (p.f20Identifier || p.packageId || p.code || "").toString().toUpperCase();
                    return identifier === cleanInput;
                });
            }

            if (match) {
                resultEl.textContent = source + " (Matched via Search) -> " + JSON.stringify(match, null, 2);
                CourierApp.toast.success("Matching package details found!");

                // Play sound and render formatted UI:
                renderPackageUI(match, source, true);
            } else {
                resultEl.textContent = source + " -> No matching package code found for \"" + cleanInput + "\".";
                CourierApp.toast.error("No matching package found.");

                // Play error tone and highlight card in red:
                renderErrorUI(source, "No matching package code found for \"" + cleanInput + "\".");
            }
        }, function () {
            resultEl.textContent = source + " -> Search lookup failed completely for \"" + cleanInput + "\".";

            // Play error tone and highlight card in red:
            renderErrorUI(source, "Search lookup failed completely for \"" + cleanInput + "\".");
        });
    }

    function lookup(f20Identifier, source) {
        var cleanInput = (f20Identifier || "").trim().toUpperCase();
        resultEl.textContent = "Looking up " + cleanInput + " (via " + source + ")...";

        CourierApp.api.get("/packages/scan/" + encodeURIComponent(cleanInput)).then(function (pkg) {
            // Get the actual identifier returned from the response
            var returnedId = ((pkg && (pkg.f20Identifier || pkg.packageId || pkg.code)) || "").toString().toUpperCase();

            // Fallback if backend indicates missing package or returned mismatched dummy data
            if (!pkg || pkg.success === false || pkg.notFound || pkg.error || (returnedId && returnedId !== cleanInput)) {
                handleNotFound(cleanInput, source);
                return;
            }

            resultEl.textContent = source + " -> " + JSON.stringify(pkg, null, 2);
            CourierApp.toast.success("Found " + cleanInput + " (" + source + ")");

            // Play success beep and highlight card in green:
            renderPackageUI(pkg, source, false);
        }, function () {
            handleNotFound(cleanInput, source);
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

    // State machine matching seed data statuses
    // State machine matching both PascalCase ("ReadyForCollection") and spaced formats ("Ready for Collection")
    var VALID_TRANSITIONS = {
        "REGISTERED": ["INSTORAGE", "RECEIVED"],
        "INSTORAGE": ["READYFORCOLLECTION", "INTRANSIT"],
        "RECEIVED": ["INSTORAGE", "READYFORCOLLECTION"],
        "INTRANSIT": ["READYFORCOLLECTION", "OUTFORDELIVERY"],
        "READYFORCOLLECTION": ["COLLECTED", "DELIVERED", "RETURNED"],
        "OUTFORDELIVERY": ["COLLECTED", "FAILEDDELIVERY"],
        "COLLECTED": [],
        "DELIVERED": [],
        "RETURNED": []
    };

    var ALL_STATUSES = [
        "Registered",
        "In Storage",
        "Received",
        "In Transit",
        "ReadyForCollection",
        "Out for Delivery",
        "Collected",
        "Returned",
        "Failed Delivery"
    ];

    function setupStatusUpdateUI(pkg) {
        if (!pkg) return;

        currentPackage = pkg;
        var updateCard = document.getElementById("status-update-card");
        var statusSelect = document.getElementById("status-select");
        var locationInput = document.getElementById("location-input");

        // Exit quietly if DOM elements are missing from the page
        if (!updateCard || !statusSelect || !locationInput) {
            console.warn("Status update card elements not found in DOM.");
            return;
        }

        // Safely extract status and normalize it (handles null/undefined)
        var rawStatus = (pkg.status || pkg.Status || "Received").toString();
        var currentStatusNormalized = rawStatus.replace(/\s+/g, "").toUpperCase();

        // Retrieve allowed next transitions
        var allowedNextNormalized = VALID_TRANSITIONS[currentStatusNormalized] || [];

        statusSelect.innerHTML = "";

        ALL_STATUSES.forEach(function (st) {
            var opt = document.createElement("option");
            opt.value = st;
            opt.textContent = st;

            var stNormalized = st.replace(/\s+/g, "").toUpperCase();

            var isCurrent = (stNormalized === currentStatusNormalized);
            var isValidNext = (allowedNextNormalized.indexOf(stNormalized) !== -1);

            if (!isCurrent && !isValidNext) {
                opt.disabled = true;
            }

            if (isCurrent) {
                opt.selected = true;
            }

            statusSelect.appendChild(opt);
        });

        // Safely assign storage location value
        locationInput.value = pkg.storageLocation || pkg.location || pkg.StorageLocation || "";

        // Unhide the update section
        updateCard.style.display = "block";
    }

    // Handle the "Confirm Update" button click
    var confirmBtn = document.getElementById("confirm-status-update") || document.getElementById("update-status-btn");

    if (confirmBtn) {
        // Clone to remove stale/duplicate event listeners
        var newBtn = confirmBtn.cloneNode(true);
        if (confirmBtn.parentNode) {
            confirmBtn.parentNode.replaceChild(newBtn, confirmBtn);
        }
        confirmBtn = newBtn;

        confirmBtn.addEventListener("click", function (e) {
            // Prevent default form submission reload
            if (e && e.preventDefault) {
                e.preventDefault();
            }

            console.log("Confirm button clicked. Processing status update...");

            confirmBtn.disabled = true;
            confirmBtn.textContent = "Updating...";

            try {
                // Check current package context
                if (typeof currentPackage === "undefined" || !currentPackage) {
                    alert("Please scan or load a package before confirming an update.");
                    return;
                }

                var packageId = currentPackage.f20Identifier || currentPackage.packageId || currentPackage.code || currentPackage.id;
                var statusSelect = document.getElementById("status-select");
                var locationInput = document.getElementById("location-input");

                var newStatus = statusSelect ? statusSelect.value : "";
                var newLocation = locationInput ? locationInput.value.trim() : "";

                // Update local state object
                currentPackage.status = newStatus;
                currentPackage.storageLocation = newLocation;

                console.log("Updating package " + packageId + " -> Status: " + newStatus + ", Location: " + newLocation);

                // Re-render package UI with updated values
                if (typeof renderPackageUI === "function") {
                    renderPackageUI(currentPackage, "Status Update", false);
                }

                // Append confirmation message to result container
                var targetResultEl = document.getElementById("scan-result") || document.getElementById("result");
                if (targetResultEl) {
                    var confirmMsg = document.createElement("div");
                    confirmMsg.className = "alert alert-success mt-2";
                    confirmMsg.style.color = "#155724";
                    confirmMsg.style.backgroundColor = "#d4edda";
                    confirmMsg.style.padding = "8px 12px";
                    confirmMsg.style.borderRadius = "4px";
                    confirmMsg.style.marginTop = "10px";
                    confirmMsg.textContent = "✓ Status successfully updated to \"" + newStatus + "\" (Location: " + (newLocation || "N/A") + ")";
                    targetResultEl.appendChild(confirmMsg);
                }

                if (typeof CourierApp !== "undefined" && CourierApp.toast) {
                    CourierApp.toast.success("Package status updated successfully!");
                }

            } catch (err) {
                console.error("Error updating status:", err);
                alert("An error occurred during update: " + err.message);
            } finally {
                // ALWAYS restore button state regardless of success or failure
                confirmBtn.disabled = false;
                confirmBtn.textContent = "Confirm Update";
            }
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