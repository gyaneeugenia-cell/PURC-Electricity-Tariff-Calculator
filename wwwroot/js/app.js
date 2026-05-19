(function () {
    const yearSelect = document.getElementById("calendarYearSelect");
    const periodSelect = document.getElementById("periodSelect");
    const yearIdInput = document.getElementById("selectedYearId");
    const categorySelect = document.getElementById("categorySelect");
    const demandField = document.getElementById("demandField");
    const demandInput = document.getElementById("DemandKva");
    const consumptionField = document.getElementById("consumptionField");
    const totalBillField = document.getElementById("totalBillField");
    const modeInputs = document.querySelectorAll('input[name="Mode"]');
    const showTrendTools = document.getElementById("ShowTrendTools");
    const trendToolsPanel = document.getElementById("trendToolsPanel");
    const closeTrendToolsButton = document.getElementById("closeTrendToolsButton");
    const toggleRatesButton = document.getElementById("toggleRatesButton");
    const tariffRatesPanel = document.getElementById("tariffRatesPanel");
    const toggleDatabaseUpdateButton = document.getElementById("toggleDatabaseUpdateButton");
    const databaseUpdatePanel = document.getElementById("databaseUpdatePanel");
    const trendCategorySelect = document.getElementById("TrendCategory");
    const trendYearsSelect = document.getElementById("TrendYearsSelect");
    const trendPeriodSelects = document.querySelectorAll(".trend-period-select");
    const showTrendTableInput = document.getElementById("ShowTrendTable");
    const trendConsumptionInput = document.getElementById("TrendConsumptionKwh");
    const chartExportButtons = document.querySelectorAll("[data-chart-export]");
    const tariffRecordKindSelect = document.getElementById("tariffRecordKind");
    const componentEditorSection = document.querySelector('[data-tariff-editor-section="component"]');
    const chargeEditorSection = document.querySelector('[data-tariff-editor-section="charge"]');
    const confirmForms = document.querySelectorAll("form[data-confirm-message]");
    const tiltCards = document.querySelectorAll("[data-tilt-card]");

    function getSelectedMode() {
        const checked = Array.from(modeInputs).find(function (input) {
            return input.checked;
        });

        return checked ? checked.value : "Consumption";
    }

    function updatePreferenceCardState() {
        document.querySelectorAll(".preference-card").forEach(function (card) {
            const input = card.querySelector('input[type="radio"]');
            card.classList.toggle("is-selected", !!input && input.checked);
        });
    }

    function updateModeVisibility() {
        const mode = getSelectedMode();

        if (consumptionField) {
            consumptionField.style.display = mode === "Consumption" ? "flex" : "none";
        }

        if (totalBillField) {
            totalBillField.style.display = mode === "TotalBill" ? "flex" : "none";
        }
    }

    function extractYear(period) {
        const match = String(period || "").match(/(19|20)\d{2}/);
        return match ? Number(match[0]) : 0;
    }

    function requiresDemand(category, period) {
        if (!String(category || "").startsWith("SLT")) {
            return false;
        }

        const year = extractYear(period);
        return !((year === 2019 && String(period).includes("Q3 2019")) || year >= 2020);
    }

    function syncSelectedYearId() {
        if (!periodSelect || !yearIdInput) {
            return;
        }

        const selectedOption = periodSelect.options[periodSelect.selectedIndex];
        yearIdInput.value = selectedOption ? (selectedOption.dataset.yearId || "0") : "0";
    }

    function updateDemandVisibility() {
        if (!demandField || !periodSelect || !categorySelect) {
            return;
        }

        const shouldShow = requiresDemand(categorySelect.value, periodSelect.value);
        demandField.style.display = shouldShow ? "flex" : "none";

        if (!shouldShow && demandInput) {
            demandInput.value = "0";
        }
    }

    function toggleTrendPanel(forceOpen) {
        if (!showTrendTools || !trendToolsPanel) {
            return;
        }

        const shouldOpen = typeof forceOpen === "boolean" ? forceOpen : showTrendTools.checked;
        showTrendTools.checked = shouldOpen;
        trendToolsPanel.classList.toggle("is-hidden", !shouldOpen);

        if (shouldOpen) {
            trendToolsPanel.scrollIntoView({ behavior: "smooth", block: "start" });
        }
    }

    function toggleRatesPanel() {
        if (!tariffRatesPanel || !toggleRatesButton) {
            return;
        }

        const isHidden = tariffRatesPanel.classList.toggle("is-hidden");
        toggleRatesButton.textContent = isHidden ? "Show Tariff Rates" : "Hide Tariff Rates";
    }

    function toggleDatabaseUpdatePanel() {
        if (!databaseUpdatePanel || !toggleDatabaseUpdateButton) {
            return;
        }

        const isHidden = databaseUpdatePanel.classList.toggle("is-hidden");
        toggleDatabaseUpdateButton.textContent = isHidden ? "Update Database" : "Hide Database Update";
    }

    function appendMainState(url, resetDependentSelections) {
        if (!yearSelect) {
            return url;
        }

        syncSelectedYearId();
        url.searchParams.set("SelectedCalendarYear", yearSelect.value);

        if (!resetDependentSelections) {
            if (periodSelect && periodSelect.value) {
                url.searchParams.set("SelectedPeriod", periodSelect.value);
            }

            if (categorySelect && categorySelect.value) {
                url.searchParams.set("SelectedCategory", categorySelect.value);
            }
        }

        const mode = getSelectedMode();
        if (mode) {
            url.searchParams.set("Mode", mode);
        }

        document.querySelectorAll('input[data-sanitize-number="integer"], input[data-sanitize-number="decimal"]').forEach(function (input) {
            if (!input.id || !input.value) {
                return;
            }

            if (input.id === "TrendConsumptionKwh") {
                return;
            }

            if (input.id.startsWith("trend-demand-")) {
                return;
            }

            url.searchParams.set(input.name || input.id, input.value);
        });

        if (showTrendTools && showTrendTools.checked) {
            url.searchParams.set("ShowTrendTools", "true");
        }

        return url;
    }

    function appendTrendState(url) {
        if (!trendYearsSelect || !trendCategorySelect) {
            return url;
        }

        url.searchParams.set("ShowTrendTools", "true");
        url.searchParams.set("TrendCategory", trendCategorySelect.value || "");

        if (trendConsumptionInput && trendConsumptionInput.value) {
            url.searchParams.set("TrendConsumptionKwh", trendConsumptionInput.value);
        }

        if (showTrendTableInput) {
            url.searchParams.set("ShowTrendTable", showTrendTableInput.checked ? "true" : "false");
        }

        Array.from(trendYearsSelect.selectedOptions).forEach(function (option) {
            url.searchParams.append("TrendYears", option.value);
        });

        trendPeriodSelects.forEach(function (select) {
            if (select.name && select.value) {
                url.searchParams.set(select.name, select.value);
            }
        });

        document.querySelectorAll('input[id^="trend-demand-"]').forEach(function (input) {
            if (input.name && input.value) {
                url.searchParams.set(input.name, input.value);
            }
        });

        return url;
    }

    function buildRefreshUrl(resetDependentSelections) {
        const url = new URL("/", window.location.origin);
        appendMainState(url, resetDependentSelections);

        if (showTrendTools && showTrendTools.checked) {
            appendTrendState(url);
        }

        return url.toString();
    }

    function buildTrendRefreshUrl() {
        const url = new URL("/", window.location.origin);
        appendMainState(url, false);
        appendTrendState(url);
        return url.toString();
    }

    function setSectionVisibility(section, shouldShow, displayMode) {
        if (!section) {
            return;
        }

        section.style.display = shouldShow ? displayMode : "none";
        section.querySelectorAll("input, select, textarea, button").forEach(function (element) {
            if (element.type === "hidden") {
                return;
            }

            element.disabled = !shouldShow;
        });
    }

    function updateTariffEditorSections() {
        if (!tariffRecordKindSelect) {
            return;
        }

        const recordKind = tariffRecordKindSelect.value;
        const showComponentSection = recordKind === "TariffComponent";

        setSectionVisibility(componentEditorSection, showComponentSection, "grid");
        setSectionVisibility(chargeEditorSection, !showComponentSection, "grid");
    }

    function sanitizeNumericInputs() {
        document.querySelectorAll("[data-sanitize-number]").forEach(function (input) {
        const mode = input.dataset.sanitizeNumber;

        input.addEventListener("keydown", function (event) {
            const blockedKeys = mode === "integer" ? ["e", "E", "+", "-", ".", ","] : ["e", "E", "+", "-"];

            if (blockedKeys.includes(event.key)) {
                event.preventDefault();
                return;
            }

            if (mode === "decimal" && (event.key === "." || event.key === ",") && input.value.includes(".")) {
                event.preventDefault();
            }
        });

        input.addEventListener("input", function () {
            const originalValue = input.value;
            const originalCursor = input.selectionStart ?? originalValue.length;
            let nextValue = originalValue;

            if (mode === "integer") {
                nextValue = nextValue.replace(/[^\d]/g, "");
            } else {
                nextValue = nextValue.replace(/,/g, ".").replace(/[^0-9.]/g, "");
                const firstDotIndex = nextValue.indexOf(".");
                if (firstDotIndex >= 0) {
                    nextValue =
                        nextValue.substring(0, firstDotIndex + 1) +
                            nextValue.substring(firstDotIndex + 1).replace(/\./g, "");
                }
            }

            if (input.value !== nextValue) {
                const removedCharacters = originalValue.length - nextValue.length;
                const nextCursor = Math.max(0, originalCursor - Math.max(0, removedCharacters));

                input.value = nextValue;

                if (typeof input.setSelectionRange === "function") {
                    input.setSelectionRange(nextCursor, nextCursor);
                }
            }
        });
    });
}

    function wirePasswordToggles() {
        document.querySelectorAll("[data-password-toggle]").forEach(function (button) {
            button.addEventListener("click", function () {
                const inputId = button.dataset.passwordToggle;
                if (!inputId) {
                    return;
                }

                const input = document.getElementById(inputId);
                if (!input) {
                    return;
                }

                const shouldShow = input.type === "password";
                input.type = shouldShow ? "text" : "password";
                button.textContent = shouldShow ? "Hide" : "Show";
            });
        });
    }

    function wireConfirmForms() {
        confirmForms.forEach(function (form) {
            form.addEventListener("submit", function (event) {
                const message = form.dataset.confirmMessage;
                if (message && !window.confirm(message)) {
                    event.preventDefault();
                }
            });
        });
    }

    function wireMotionCards() {
        tiltCards.forEach(function (card) {
            function resetTilt() {
                card.style.setProperty("--card-tilt-x", "0deg");
                card.style.setProperty("--card-tilt-y", "0deg");
            }

            card.addEventListener("pointermove", function (event) {
                const rect = card.getBoundingClientRect();
                if (!rect.width || !rect.height) {
                    return;
                }

                const xRatio = (event.clientX - rect.left) / rect.width - 0.5;
                const yRatio = (event.clientY - rect.top) / rect.height - 0.5;

                card.style.setProperty("--card-tilt-y", `${xRatio * 18}deg`);
                card.style.setProperty("--card-tilt-x", `${yRatio * -14}deg`);
            });

            card.addEventListener("pointerleave", resetTilt);
            card.addEventListener("pointerup", resetTilt);
            card.addEventListener("pointercancel", resetTilt);
        });
    }

    function downloadTrendChart(format) {
        const svg = document.querySelector(".trend-svg");
        if (!svg) {
            return;
        }

        const serializer = new XMLSerializer();
        const svgMarkup = serializer.serializeToString(svg);
        const svgBlob = new Blob([svgMarkup], { type: "image/svg+xml;charset=utf-8" });
        const svgUrl = URL.createObjectURL(svgBlob);
        const image = new Image();

        image.onload = function () {
            const viewBox = svg.viewBox.baseVal;
            const width = viewBox && viewBox.width ? viewBox.width : svg.clientWidth || 800;
            const height = viewBox && viewBox.height ? viewBox.height : svg.clientHeight || 420;
            const canvas = document.createElement("canvas");
            const context = canvas.getContext("2d");

            canvas.width = width;
            canvas.height = height;

            if (!context) {
                URL.revokeObjectURL(svgUrl);
                return;
            }

            context.fillStyle = "#ffffff";
            context.fillRect(0, 0, width, height);
            context.drawImage(image, 0, 0, width, height);

            const mimeType = format === "jpeg" ? "image/jpeg" : "image/png";
            const extension = format === "jpeg" ? "jpg" : "png";
            const chartTitle = (document.querySelector(".chart-card h3")?.textContent || "bill-trend")
                .trim()
                .toLowerCase()
                .replace(/[^a-z0-9]+/g, "-")
                .replace(/^-+|-+$/g, "");
            const downloadLink = document.createElement("a");

            downloadLink.href = canvas.toDataURL(mimeType, 0.95);
            downloadLink.download = `${chartTitle || "bill-trend"}.${extension}`;
            downloadLink.click();

            URL.revokeObjectURL(svgUrl);
        };

        image.onerror = function () {
            URL.revokeObjectURL(svgUrl);
        };

        image.src = svgUrl;
    }

    if (yearSelect) {
        yearSelect.addEventListener("change", function () {
            window.location.assign(buildRefreshUrl(true));
        });
    }

    if (periodSelect) {
        periodSelect.addEventListener("change", function () {
            syncSelectedYearId();
            updateDemandVisibility();
            window.location.assign(buildRefreshUrl(false));
        });
    }

    if (categorySelect) {
        categorySelect.addEventListener("change", function () {
            updateDemandVisibility();
            window.location.assign(buildRefreshUrl(false));
        });
    }

    modeInputs.forEach(function (input) {
        input.addEventListener("change", function () {
            updatePreferenceCardState();
            updateModeVisibility();
        });
    });

    if (showTrendTools) {
        showTrendTools.addEventListener("change", function () {
            toggleTrendPanel(showTrendTools.checked);
        });
    }

    if (closeTrendToolsButton) {
        closeTrendToolsButton.addEventListener("click", function () {
            toggleTrendPanel(false);
        });
    }

    if (toggleRatesButton) {
        toggleRatesButton.addEventListener("click", toggleRatesPanel);
    }

    if (toggleDatabaseUpdateButton) {
        toggleDatabaseUpdateButton.addEventListener("click", toggleDatabaseUpdatePanel);
    }

    if (trendCategorySelect) {
        trendCategorySelect.addEventListener("change", function () {
            window.location.assign(buildTrendRefreshUrl());
        });
    }

    if (trendYearsSelect) {
        trendYearsSelect.addEventListener("change", function () {
            window.location.assign(buildTrendRefreshUrl());
        });
    }

    if (tariffRecordKindSelect) {
        tariffRecordKindSelect.addEventListener("change", updateTariffEditorSections);
    }

    trendPeriodSelects.forEach(function (select) {
        select.addEventListener("change", function () {
            window.location.assign(buildTrendRefreshUrl());
        });
    });

    chartExportButtons.forEach(function (button) {
        button.addEventListener("click", function () {
            downloadTrendChart(button.dataset.chartExport || "png");
        });
    });

    updatePreferenceCardState();
    updateModeVisibility();
    syncSelectedYearId();
    updateDemandVisibility();
    updateTariffEditorSections();
    sanitizeNumericInputs();
    wirePasswordToggles();
    wireConfirmForms();
    wireMotionCards();

    if (toggleRatesButton && tariffRatesPanel) {
        toggleRatesButton.textContent = tariffRatesPanel.classList.contains("is-hidden")
            ? "Show Tariff Rates"
            : "Hide Tariff Rates";
    }

    if (toggleDatabaseUpdateButton && databaseUpdatePanel) {
        toggleDatabaseUpdateButton.textContent = databaseUpdatePanel.classList.contains("is-hidden")
            ? "Update Database"
            : "Hide Database Update";
    }

})();
