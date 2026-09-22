/*
 * T10 spike: USB HID-mode scan and phone-camera QR scan, both calling
 * GET /api/packages/scan/{f20Identifier} (docs/API_CONTRACT.md) via CourierApp.api.
 * Findings: docs/SCANNING_SPIKE.md.
 */
(function () {
    "use strict";

    var resultEl = document.getElementById("scan-result");
    var html5QrCode = null;

    function lookup(f20Identifier, source) {
        resultEl.textContent = "Looking up " + f20Identifier + " (via " + source + ")...";

        CourierApp.api.get("/packages/scan/" + encodeURIComponent(f20Identifier)).then(function (pkg) {
            resultEl.textContent = source + " -> " + JSON.stringify(pkg, null, 2);
            CourierApp.toast.success("Found " + f20Identifier + " (" + source + ")");
        }, function () {
            resultEl.textContent = source + " -> lookup failed for \"" + f20Identifier + "\" (see toast).";
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

    function stopScanning() {
        if (!html5QrCode) {
            return;
        }
        html5QrCode.stop().then(function () {
            html5QrCode.clear();
            html5QrCode = null;
            startBtn.disabled = false;
            stopBtn.disabled = true;
        });
    }

    stopBtn.addEventListener("click", stopScanning);
})();
