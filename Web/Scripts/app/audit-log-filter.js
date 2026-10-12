document.addEventListener("DOMContentLoaded", function () {
    const startInput = document.getElementById("startDate");
    const endInput = document.getElementById("endDate");
    const errorDiv = document.getElementById("dateRangeError");
    const filterForm = document.getElementById("auditFilterForm");
    const searchInput = document.getElementById("searchTerm");
    const dropdown = document.getElementById("customSuggestionsDropdown");
    const dataContainer = document.getElementById("searchSuggestionsData");

    let allUserIds = [];
    let allEntityTypes = [];
    let allEntityIds = [];

    if (dataContainer) {
        try {
            allUserIds = JSON.parse(dataContainer.getAttribute("data-users") || "[]");
            allEntityTypes = JSON.parse(dataContainer.getAttribute("data-types") || "[]");
            allEntityIds = JSON.parse(dataContainer.getAttribute("data-ids") || "[]");
        } catch (e) {
            console.error("Failed to parse suggestion attributes", e);
        }
    }

    if (searchInput && dropdown) {
        searchInput.addEventListener("input", function () {
            const query = this.value.trim().toLowerCase();

            if (query.length === 0) {
                dropdown.style.display = "none";
                dropdown.replaceChildren();
                return;
            }

            const matchedUsers = allUserIds.filter(item => item && item.toLowerCase().includes(query));
            const matchedTypes = allEntityTypes.filter(item => item && item.toLowerCase().includes(query));
            const matchedIds = allEntityIds.filter(item => item && item.toLowerCase().includes(query));

            if (matchedUsers.length === 0 && matchedTypes.length === 0 && matchedIds.length === 0) {
                dropdown.style.display = "none";
                dropdown.replaceChildren();
                return;
            }

            dropdown.replaceChildren();
            function addGroup(title, items) {
                if (!items.length) return;
                const heading = document.createElement("div");
                heading.className = "suggestion-heading";
                heading.textContent = title;
                dropdown.appendChild(heading);
                items.forEach(item => {
                    const option = document.createElement("div");
                    option.className = "suggestion-item";
                    option.textContent = item;
                    option.dataset.value = item;
                    dropdown.appendChild(option);
                });
            }
            addGroup("USER IDS", matchedUsers);
            addGroup("ENTITY TYPES", matchedTypes);
            addGroup("ENTITY IDS", matchedIds);
            dropdown.style.display = "block";
        });

        dropdown.addEventListener("click", function (e) {
            const target = e.target.closest(".suggestion-item");
            if (target) {
                searchInput.value = target.dataset.value;
                dropdown.style.display = "none";
            }
        });

        document.addEventListener("click", function (e) {
            if (!searchInput.contains(e.target) && !dropdown.contains(e.target)) {
                dropdown.style.display = "none";
            }
        });
    }

    // Clear inputs on fresh hard refresh
    if (!window.location.search && performance.navigation.type === 1) {
        if (filterForm) filterForm.reset();
    }

    function validateDates() {
        startInput.style.borderColor = "";
        startInput.style.boxShadow = "";
        endInput.style.borderColor = "";
        endInput.style.boxShadow = "";
        errorDiv.style.display = "none";
        errorDiv.innerHTML = "";

        if (startInput.value && endInput.value) {
            const startDate = new Date(startInput.value);
            const endDate = new Date(endInput.value);

            if (startDate > endDate) {
                startInput.style.borderColor = "#c0392b";
                endInput.style.borderColor = "#c0392b";

                errorDiv.style.display = "flex";
                errorDiv.innerHTML = `
                    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="#c0392b" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" style="flex-shrink: 0;">
                        <circle cx="12" cy="12" r="10"></circle>
                        <line x1="12" y1="8" x2="12" y2="12"></line>
                        <line x1="12" y1="16" x2="12.01" y2="16"></line>
                    </svg>
                    <span>Start date cannot be after the end date.</span>
                `;
                return false;
            } else {
                startInput.style.borderColor = "#5c2d91";
                startInput.style.boxShadow = "0 0 0 3px rgba(92, 45, 145, 0.18)";
                endInput.style.borderColor = "#5c2d91";
                endInput.style.boxShadow = "0 0 0 3px rgba(92, 45, 145, 0.18)";
            }
        }
        return true;
    }

    flatpickr(".flatpickr-date", {
        dateFormat: "Y-m-d",
        allowInput: false,
        clickOpens: true,
        disableMobile: true,
        onChange: function () {
            validateDates();
        }
    });

    if (startInput) startInput.addEventListener("change", validateDates);
    if (endInput) endInput.addEventListener("change", validateDates);

    if (filterForm) {
        filterForm.addEventListener("submit", function (e) {
            if (!validateDates()) {
                e.preventDefault();
            }
        });
    }
});