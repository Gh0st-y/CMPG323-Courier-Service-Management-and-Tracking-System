/* Scatters mail-themed icons over .login-doodles. Include on the login page only. */
(function () {
    'use strict';

    var host = document.querySelector('.login-doodles');
    if (!host) return;

    var ICONS = [
        'box-seam', 'postage', 'postcard', 'mailbox-flag', 'mailbox', 'envelope',
        'envelope-open', 'envelope-paper', 'truck', 'box2', 'qr-code-scan',
        'upc-scan', 'paperclip', 'clipboard2-data'
    ];

    var CELL = 120;          // bigger = fewer doodles
    var SKIP_CHANCE = 0.25;  // empty cells for breathing room

    // seeded random so the layout is the same on every load
    function rng(seed) {
        return function () {
            seed |= 0; seed = seed + 0x6D2B79F5 | 0;
            var t = Math.imul(seed ^ seed >>> 15, 1 | seed);
            t = t + Math.imul(t ^ t >>> 7, 61 | t) ^ t;
            return ((t ^ t >>> 14) >>> 0) / 4294967296;
        };
    }

    function build() {
        host.innerHTML = '';
        var rand = rng(20261012);
        var cols = Math.ceil(window.innerWidth / CELL) + 1;
        var rows = Math.ceil(window.innerHeight / CELL) + 1;
        var n = 0;

        for (var r = 0; r < rows; r++) {
            for (var c = 0; c < cols; c++) {
                if (rand() < SKIP_CHANCE) continue;

                var el = document.createElement('i');
                var rot = Math.round(rand() * 60 - 30);

                el.className = 'bi bi-' + ICONS[n++ % ICONS.length] + (rand() < 0.3 ? ' float' : '');
                el.style.left = (c * CELL + rand() * 50 - 10) + 'px';
                el.style.top = (r * CELL + rand() * 50 - 10) + 'px';
                el.style.fontSize = Math.round(28 + rand() * 24) + 'px';
                el.style.opacity = (0.07 + rand() * 0.07).toFixed(2);
                el.style.setProperty('--r', rot + 'deg');
                el.style.transform = 'rotate(' + rot + 'deg)';
                host.appendChild(el);
            }
        }
    }

    build();

    var t;
    window.addEventListener('resize', function () {
        clearTimeout(t);
        t = setTimeout(build, 200);
    });
})();