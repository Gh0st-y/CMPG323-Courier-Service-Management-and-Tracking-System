/*
 * T07 shared frontend scaffold: CourierApp.api / .toast / .confirm / .loading.
 * Vanilla JS only (DECISIONS.md — no SPA framework, no jQuery). Load this on every page via
 * the master layout; see README.md "Frontend style guide" for usage examples.
 */
(function (window, document) {
    "use strict";

    var STORAGE_KEY_MOCK_MODE = "courier.mockMode";

    var CourierApp = window.CourierApp || {};

    // ---------------------------------------------------------------------
    // config
    // ---------------------------------------------------------------------

    CourierApp.config = {
        apiBase: "/api",
        mockFixturesUrl: "/mock/fixtures",

        get mockMode() {
            return window.localStorage.getItem(STORAGE_KEY_MOCK_MODE) === "true";
        },

        setMockMode: function (enabled) {
            window.localStorage.setItem(STORAGE_KEY_MOCK_MODE, enabled ? "true" : "false");
        }
    };

    // ---------------------------------------------------------------------
    // DOM scaffolding (idempotent — safe to call more than once)
    // ---------------------------------------------------------------------

    var toastContainerEl = null;
    var loadingBarEl = null;
    var loadingCount = 0;

    function ensureScaffold() {
        if (!toastContainerEl) {
            toastContainerEl = document.createElement("div");
            toastContainerEl.className = "toast-container";
            toastContainerEl.setAttribute("aria-live", "polite");
            document.body.appendChild(toastContainerEl);
        }

        if (!loadingBarEl) {
            loadingBarEl = document.createElement("div");
            loadingBarEl.className = "app-loading-bar";
            document.body.appendChild(loadingBarEl);
        }
    }

    // ---------------------------------------------------------------------
    // loading
    // ---------------------------------------------------------------------

    CourierApp.loading = {
        /** Increments the active-request count and shows the top loading bar. */
        show: function () {
            ensureScaffold();
            loadingCount += 1;
            loadingBarEl.classList.add("is-active");
        },

        /** Decrements the active-request count; hides the bar once nothing is pending. */
        hide: function () {
            ensureScaffold();
            loadingCount = Math.max(0, loadingCount - 1);
            if (loadingCount === 0) {
                loadingBarEl.classList.remove("is-active");
            }
        },

        /** Wraps a promise so the loading bar shows for its duration, success or failure. */
        wrap: function (promise) {
            CourierApp.loading.show();
            return promise.finally(function () {
                CourierApp.loading.hide();
            });
        }
    };

    // ---------------------------------------------------------------------
    // toast
    // ---------------------------------------------------------------------

    CourierApp.toast = {
        /** type: "info" (default) | "success" | "error" */
        show: function (message, type, durationMs) {
            ensureScaffold();
            type = type || "info";
            durationMs = durationMs || (type === "error" ? 6000 : 3500);

            var toastEl = document.createElement("div");
            toastEl.className = "toast toast--" + type;
            toastEl.setAttribute("role", type === "error" ? "alert" : "status");
            toastEl.textContent = message;

            toastContainerEl.appendChild(toastEl);

            window.setTimeout(function () {
                if (toastEl.parentNode) {
                    toastEl.parentNode.removeChild(toastEl);
                }
            }, durationMs);
        },

        success: function (message) {
            CourierApp.toast.show(message, "success");
        },

        error: function (message) {
            CourierApp.toast.show(message, "error");
        },

        info: function (message) {
            CourierApp.toast.show(message, "info");
        }
    };

    // ---------------------------------------------------------------------
    // confirm dialog — CourierApp.confirm(message).then(function (confirmed) { ... })
    // ---------------------------------------------------------------------

    CourierApp.confirm = function (message, options) {
        options = options || {};
        var confirmLabel = options.confirmLabel || "Confirm";
        var cancelLabel = options.cancelLabel || "Cancel";

        return new Promise(function (resolve) {
            var overlay = document.createElement("div");
            overlay.className = "modal-overlay";

            var dialog = document.createElement("div");
            dialog.className = "modal-dialog";
            dialog.setAttribute("role", "alertdialog");
            dialog.setAttribute("aria-modal", "true");

            var messageEl = document.createElement("p");
            messageEl.className = "modal-dialog__message";
            messageEl.textContent = message;

            var actionsEl = document.createElement("div");
            actionsEl.className = "modal-dialog__actions";

            var cancelBtn = document.createElement("button");
            cancelBtn.type = "button";
            cancelBtn.className = "btn btn-secondary";
            cancelBtn.textContent = cancelLabel;

            var confirmBtn = document.createElement("button");
            confirmBtn.type = "button";
            confirmBtn.className = "btn btn-primary";
            confirmBtn.textContent = confirmLabel;

            function close(result) {
                document.body.removeChild(overlay);
                document.removeEventListener("keydown", onKeyDown);
                resolve(result);
            }

            function onKeyDown(event) {
                if (event.key === "Escape") {
                    close(false);
                }
            }

            cancelBtn.addEventListener("click", function () {
                close(false);
            });
            confirmBtn.addEventListener("click", function () {
                close(true);
            });
            overlay.addEventListener("click", function (event) {
                if (event.target === overlay) {
                    close(false);
                }
            });
            document.addEventListener("keydown", onKeyDown);

            actionsEl.appendChild(cancelBtn);
            actionsEl.appendChild(confirmBtn);
            dialog.appendChild(messageEl);
            dialog.appendChild(actionsEl);
            overlay.appendChild(dialog);
            document.body.appendChild(overlay);

            confirmBtn.focus();
        });
    };

    // ---------------------------------------------------------------------
    // api — CourierApp.api.get/post/patch("/packages/...", body)
    // ---------------------------------------------------------------------

    var fixturesPromise = null;

    function loadFixtures() {
        if (!fixturesPromise) {
            fixturesPromise = fetch(CourierApp.config.mockFixturesUrl)
                .then(function (response) {
                    if (!response.ok) {
                        throw new Error("Could not load mock fixtures (" + response.status + ")");
                    }
                    return response.json();
                });
        }
        return fixturesPromise;
    }

    /** Matches "METHOD /api/packages/F20-0001/detail" against a fixture key that may contain {placeholders}. */
    function fixtureKeyMatches(key, method, path) {
        var spaceIndex = key.indexOf(" ");
        if (spaceIndex === -1) {
            return false;
        }
        var keyMethod = key.substring(0, spaceIndex);
        var keyPath = key.substring(spaceIndex + 1);

        if (keyMethod !== method) {
            return false;
        }
        if (keyPath === path) {
            return true;
        }

        var pattern = "^" + keyPath
            .replace(/[.+?^$()|[\]\\]/g, "\\$&")
            .replace(/\{[^}/]+\}/g, "[^/]+") + "(\\?.*)?$";
        return new RegExp(pattern).test(path);
    }

    function requestMock(method, path) {
        return loadFixtures().then(function (fixtures) {
            var matchKey = Object.keys(fixtures).filter(function (key) {
                return key !== "_readme" && key !== "error_example";
            }).find(function (key) {
                return fixtureKeyMatches(key, method, path);
            });

            if (!matchKey) {
                return Promise.reject({
                    status: 404,
                    error: { code: "MockNotFound", message: "No mock fixture for " + method + " " + path }
                });
            }

            return fixtures[matchKey];
        });
    }

    function requestReal(method, path, body) {
        var fetchOptions = {
            method: method,
            credentials: "same-origin",
            headers: {}
        };

        if (body !== undefined) {
            fetchOptions.headers["Content-Type"] = "application/json";
            fetchOptions.body = JSON.stringify(body);
        }

        return fetch(CourierApp.config.apiBase + path, fetchOptions).then(function (response) {
            if (response.status === 204) {
                return null;
            }

            return response.json().catch(function () {
                return null;
            }).then(function (data) {
                if (!response.ok) {
                    var apiError = (data && data.error) || { code: "UnknownError", message: "Something went wrong. Please try again." };
                    return Promise.reject({ status: response.status, error: apiError });
                }
                return data;
            });
        }, function () {
            return Promise.reject({ status: 0, error: { code: "NetworkError", message: "Could not reach the server. Check your connection and try again." } });
        });
    }

    /**
     * Core request function. Shows the loading bar for the duration of the call and shows an
     * error toast automatically unless options.silent is true. Resolves with the parsed body
     * (or null for 204s); rejects with { status, error: { code, message } } on failure.
     */
    function request(method, path, body, options) {
        options = options || {};

        var resultPromise = CourierApp.config.mockMode
            ? requestMock(method, CourierApp.config.apiBase + path)
            : requestReal(method, path, body);

        resultPromise = CourierApp.loading.wrap(resultPromise);

        if (!options.silent) {
            resultPromise = resultPromise.catch(function (failure) {
                var message = (failure && failure.error && failure.error.message) || "Something went wrong. Please try again.";
                CourierApp.toast.error(message);
                throw failure;
            });
        }

        return resultPromise;
    }

    CourierApp.api = {
        get: function (path, options) {
            return request("GET", path, undefined, options);
        },
        post: function (path, body, options) {
            return request("POST", path, body, options);
        },
        patch: function (path, body, options) {
            return request("PATCH", path, body, options);
        },
        request: request
    };

    window.CourierApp = CourierApp;
})(window, document);
