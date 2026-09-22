# Scanning spike (T10)

Demo page: `/Scan` (`Web/Controllers/ScanController.cs`, `Web/Views/Scan/Index.cshtml`,
`Web/Scripts/app/scan-demo.js`). Both scan methods below call the exact same endpoint —
`GET /api/packages/scan/{f20Identifier}` (`docs/API_CONTRACT.md`) — through
`CourierApp.api`, so mock mode (see README's frontend style guide) works for both without
a backend.

## (a) USB scanner, keyboard/HID mode

**What it does:** a text input, auto-focused on page load. HID-mode barcode scanners act
as a keyboard — they "type" the decoded value character-by-character and then send a
terminator key (Enter, by default on most scanners). The page listens for `keydown` on
that input, and on Enter takes the accumulated value, clears the field, and calls the
lookup — then refocuses the input so the next scan doesn't need a click first.

**Verified:** typing an identifier and pressing Enter triggers the lookup, displays the
result, clears and refocuses the input — confirmed in a real browser session (Chrome).

**Not verified — needs a physical scanner:** this sandbox has no USB HID hardware
attached, so the *actual scanner device* was never plugged in and tested. Typing +
Enter is a faithful simulation of HID-mode output, but a teammate with a real scanner
should still confirm:
- their scanner's terminator key matches Enter (most do; some default to Tab — if so,
  the `keydown` check in `scan-demo.js` needs a second case for `"Tab"`)
- scan speed doesn't cause characters to arrive faster than the browser processes them
  (unlikely at typical scanner rates, but worth a real check)

## (b) Phone camera, QR via html5-qrcode

**What it does:** loads `html5-qrcode` from a CDN (`cdn.jsdelivr.net` — this project has
no JS build step, so a CDN `<script>` tag is the pragmatic choice; vendor it locally
later if the team wants the demo to work fully offline), and on "Start camera scan"
opens the device's rear camera (`facingMode: "environment"`) and calls the same lookup
on a successful decode.

**HTTPS requirement — this was the main point of the spike:**
Camera access (`getUserMedia`) only works in a "secure context" — HTTPS, or `http://`
on `localhost`/`127.0.0.1`. A phone reaching a dev machine over the LAN by IP address
(e.g. `http://192.168.x.x:44444`) does **not** get the localhost exception, so the
camera will silently refuse to start unless the connection is HTTPS.

The page checks `window.isSecureContext` before attempting to start the camera. If it's
`false`, it shows a visible warning banner and disables the "Start camera scan" button
instead of letting the camera call fail silently — that was an explicit requirement for
this spike (don't skip the check silently).

**Verified:**
- On `http://localhost:44444/Scan`, `window.isSecureContext` is `true` (Chrome's
  localhost exception), the warning banner stays hidden, and clicking "Start camera
  scan" does reach `Html5Qrcode.start()` and genuinely calls `getUserMedia` — the
  sandboxed browser used for this spike has no camera device attached, so the call
  rejects, but the rejection is caught and surfaced as an error toast rather than
  hanging or crashing. (The CDN script itself loaded fine — `Html5Qrcode` is defined
  and callable.)

**Not verified — needs the team network / a real device:**
- The actual "blocked because not secure" path. I tried reaching the dev machine from
  the same browser via its LAN IP (`http://192.168.8.95:44444/Scan`) to force a
  non-localhost HTTP origin, but IIS Express's default binding (`iisexpress
  /path:... /port:44444`) only listens on `localhost` and returned `400 Bad Request`
  for the IP — reaching it from another device on the network needs either an IIS
  Express binding to the machine's IP (and a Windows Firewall allow rule) or a tunnel
  (e.g. `ngrok`), plus a certificate the phone's browser will accept for the HTTPS
  case. None of that was set up in this spike.
- Real QR decode against a physical camera and a printed/displayed QR code.
- Whether the team's actual network (NWU wifi / whatever the demo room uses) allows
  a phone and the demo laptop to reach each other at all — worth checking before the
  M1 demo, not just before this task's deadline.

## Recommendation for later tasks

- For the demo (4 Oct 2026), either run the Web app under HTTPS (IIS Express can do
  this with a trusted dev cert when launched via Visual Studio, or use `dotnet dev-certs`
  equivalents / a reverse proxy) or accept that the phone-camera path only gets
  demonstrated from a laptop's own `localhost`, not a separate phone.
- If a real scanner's terminator key turns out to be Tab instead of Enter, that's a
  one-line change in `scan-demo.js`, not a redesign.
