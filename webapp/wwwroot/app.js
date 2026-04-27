const FILTER_FIELD_ORDER = [
    "mode",
    "set",
    "cost",
    "class",
    "rarity",
    "cardType",
    "race",
    "school",
    "keyword",
    "collectible",
];

const FILTER_FIELD_LABELS = {
    mode: "模式",
    set: "扩展包",
    cost: "法力值",
    class: "职业",
    rarity: "稀有度",
    cardType: "卡片类型",
    race: "随从种族",
    school: "法术派系",
    keyword: "关键词",
    collectible: "是否可收藏",
};

const CARD_DATA_MAP_KEYS = [
    "unknownEnumMap",
    "tagLabels",
    "classMap",
    "rarityMap",
    "raceMap",
    "schoolMap",
    "keywordMap",
    "setMap",
];

const SETTINGS_VIEW_FILTER = "filter";
const SETTINGS_VIEW_MAPS = "maps";

const state = {
    bootstrap: null,
    filterConfig: null,
    draftFilterConfig: null,
    activeConfigSectionKey: null,
    cardDataMaps: null,
    draftCardDataMaps: null,
    activeCardDataMapKey: null,
    activeSettingsView: SETTINGS_VIEW_FILTER,
    activeDetail: null,
};

const elements = {
    pageTitle: document.querySelector(".hero-copy h1"),
    queryInput: document.getElementById("queryInput"),
    modeSelect: document.getElementById("modeSelect"),
    setPicker: document.getElementById("setPicker"),
    setPickerButton: document.getElementById("setPickerButton"),
    setPickerPanel: document.getElementById("setPickerPanel"),
    costSelect: document.getElementById("costSelect"),
    classSelect: document.getElementById("classSelect"),
    raritySelect: document.getElementById("raritySelect"),
    cardTypeSelect: document.getElementById("cardTypeSelect"),
    raceSelect: document.getElementById("raceSelect"),
    schoolSelect: document.getElementById("schoolSelect"),
    keywordSelect: document.getElementById("keywordSelect"),
    collectibleSelect: document.getElementById("collectibleSelect"),
    searchButton: document.getElementById("searchButton"),
    resetButton: document.getElementById("resetButton"),
    statusText: document.getElementById("statusText"),
    results: document.getElementById("results"),
    filterConfigButton: document.getElementById("filterConfigButton"),
    filterConfigModal: document.getElementById("filterConfigModal"),
    filterConfigModalPanel: document.getElementById("filterConfigModalPanel"),
    closeFilterConfigButton: document.getElementById("closeFilterConfigButton"),
    settingsHeaderBadge: document.querySelector("#filterConfigModalPanel .settings-modal-header .detail-badge"),
    settingsTitle: document.getElementById("filterConfigTitle"),
    settingsDescription: document.querySelector("#filterConfigModalPanel .settings-modal-description"),
    filterConfigTabButton: document.getElementById("filterConfigTabButton"),
    cardDataMapTabButton: document.getElementById("cardDataMapTabButton"),
    filterConfigView: document.getElementById("filterConfigView"),
    cardDataMapView: document.getElementById("cardDataMapView"),
    filterConfigSectionList: document.getElementById("filterConfigSectionList"),
    filterConfigOptionList: document.getElementById("filterConfigOptionList"),
    cardDataMapLibraryList: document.getElementById("cardDataMapLibraryList"),
    cardDataMapEditor: document.getElementById("cardDataMapEditor"),
    resetFilterConfigButton: document.getElementById("resetFilterConfigButton"),
    saveFilterConfigButton: document.getElementById("saveFilterConfigButton"),
    backToTopButton: document.getElementById("backToTopButton"),
    copyToast: document.getElementById("copyToast"),
    detailModal: document.getElementById("detailModal"),
    detailModalPanel: document.getElementById("detailModalPanel"),
    closeModalButton: document.getElementById("closeModalButton"),
    detailBadge: document.getElementById("detailBadge"),
    copyNameButton: document.getElementById("copyNameButton"),
    copyCardIdButton: document.getElementById("copyCardIdButton"),
    copyDbfIdButton: document.getElementById("copyDbfIdButton"),
    detailDescription: document.getElementById("detailDescription"),
    detailVisual: document.getElementById("detailVisual"),
    parentSection: document.getElementById("parentSection"),
    relatedSection: document.getElementById("relatedSection"),
    enchantmentSection: document.getElementById("enchantmentSection"),
    parentSectionTitle: document.querySelector("#parentSection .section-title"),
    relatedSectionTitle: document.querySelector("#relatedSection .section-title"),
    enchantmentSectionTitle: document.querySelector("#enchantmentSection .section-title"),
    tagSectionTitle: document.querySelector(".tag-section .section-title"),
    parentLinks: document.getElementById("parentLinks"),
    relatedLinks: document.getElementById("relatedLinks"),
    enchantmentLinks: document.getElementById("enchantmentLinks"),
    tagList: document.getElementById("tagList"),
};

let copyToastTimer = null;

void initialize();

async function initialize() {
    injectRuntimeStyles();
    synchronizeStaticText();
    bindEvents();

    try {
        state.bootstrap = await fetchJson("/api/bootstrap");
        state.filterConfig = await fetchJson("/api/filter-bar-config");
        state.draftFilterConfig = cloneFilterConfig(state.filterConfig);
        state.cardDataMaps = hydrateCardDataMapConfig(await fetchJson("/api/card-data-maps"));
        state.draftCardDataMaps = cloneCardDataMapConfig(state.cardDataMaps);
    } catch (error) {
        showFatalError(error);
        return;
    }

    synchronizeStaticText();
    initializeStaticControls();
    renderConfiguredFilters();
    await searchCards();
}

function bindEvents() {
    elements.searchButton.addEventListener("click", () => {
        void searchCards();
    });

    elements.resetButton.addEventListener("click", () => {
        resetFilters();
        void searchCards();
    });

    elements.modeSelect.addEventListener("change", () => {
        refreshSetOptions();
    });

    elements.setPickerButton.addEventListener("click", () => {
        const shouldOpen = elements.setPickerPanel.classList.contains("is-hidden");
        elements.setPickerPanel.classList.toggle("is-hidden", !shouldOpen);
        elements.setPickerButton.setAttribute("aria-expanded", String(shouldOpen));
    });

    document.addEventListener("click", (event) => {
        if (!elements.setPicker.contains(event.target)) {
            closeSetPicker();
        }
    });

    const submitOnEnterElements = [
        elements.queryInput,
        elements.modeSelect,
        elements.costSelect,
        elements.classSelect,
        elements.raritySelect,
        elements.cardTypeSelect,
        elements.raceSelect,
        elements.schoolSelect,
        elements.keywordSelect,
        elements.collectibleSelect,
    ];

    for (const control of submitOnEnterElements) {
        control.addEventListener("keydown", (event) => {
            if (event.key !== "Enter") {
                return;
            }

            event.preventDefault();
            void searchCards();
        });
    }

    elements.filterConfigButton.addEventListener("click", openFilterConfigModal);
    elements.closeFilterConfigButton.addEventListener("click", closeFilterConfigModal);

    if (elements.filterConfigTabButton) {
        elements.filterConfigTabButton.addEventListener("click", () => {
            state.activeSettingsView = SETTINGS_VIEW_FILTER;
            renderSettingsModal();
        });
    }

    if (elements.cardDataMapTabButton) {
        elements.cardDataMapTabButton.addEventListener("click", () => {
            state.activeSettingsView = SETTINGS_VIEW_MAPS;
            renderSettingsModal();
        });
    }

    elements.filterConfigModal.addEventListener("click", (event) => {
        const target = event.target;
        if (!(target instanceof HTMLElement)) {
            return;
        }

        if (target.dataset.closeFilterConfig === "true") {
            closeFilterConfigModal();
        }
    });

    elements.resetFilterConfigButton.addEventListener("click", async () => {
        try {
            await resetSettingsDraft();
        } catch (error) {
            showCopyToast(normalizeErrorMessage(error), elements.resetFilterConfigButton);
        }
    });

    elements.saveFilterConfigButton.addEventListener("click", async () => {
        try {
            await saveSettings();
        } catch (error) {
            showCopyToast(normalizeErrorMessage(error), elements.saveFilterConfigButton);
        }
    });

    elements.closeModalButton.addEventListener("click", closeModal);

    elements.detailModal.addEventListener("click", (event) => {
        const target = event.target;
        if (!(target instanceof HTMLElement)) {
            return;
        }

        if (target.dataset.closeModal === "true") {
            closeModal();
        }
    });

    document.addEventListener("keydown", (event) => {
        if (event.key === "Escape" && !elements.filterConfigModal.classList.contains("is-hidden")) {
            closeFilterConfigModal();
            return;
        }

        if (event.key === "Escape" && !elements.detailModal.classList.contains("is-hidden")) {
            closeModal();
        }
    });

    window.addEventListener("scroll", () => {
        elements.backToTopButton.classList.toggle("is-hidden", window.scrollY <= 320);
    });

    elements.backToTopButton.addEventListener("click", () => {
        window.scrollTo({ top: 0, behavior: "smooth" });
    });

    elements.copyNameButton.addEventListener("click", (event) => {
        if (state.activeDetail) {
            void copyText(state.activeDetail.name, `已复制到剪贴板：${state.activeDetail.name}`, event.currentTarget);
        }
    });

    elements.copyCardIdButton.addEventListener("click", (event) => {
        if (state.activeDetail) {
            void copyText(state.activeDetail.cardId, `已复制到剪贴板：${state.activeDetail.cardId}`, event.currentTarget);
        }
    });

    elements.copyDbfIdButton.addEventListener("click", (event) => {
        if (state.activeDetail) {
            void copyText(String(state.activeDetail.dbfId), `已复制到剪贴板：${state.activeDetail.dbfId}`, event.currentTarget);
        }
    });
}

function synchronizeStaticText() {
    const appName = state.bootstrap?.appName || "炉石卡牌检索器";
    document.title = appName;

    if (elements.pageTitle) {
        elements.pageTitle.textContent = appName;
    }

    elements.statusText.textContent = state.bootstrap
        ? elements.statusText.textContent
        : "正在加载卡牌数据…";

    elements.queryInput.placeholder = "中文名 / 英文名 / CardID / DbfId / 标签:值 / EnumID:值";
    elements.searchButton.textContent = "筛选 / 搜索";
    elements.resetButton.textContent = "重置";
    elements.setPickerButton.textContent = elements.setPickerButton.dataset.value || FILTER_FIELD_LABELS.set;
    elements.setPickerButton.title = elements.setPickerButton.dataset.value || FILTER_FIELD_LABELS.set;
    elements.filterConfigButton.textContent = "⚙";
    elements.filterConfigButton.setAttribute("aria-label", "打开设置中心");
    elements.filterConfigButton.title = "设置中心";

    elements.closeModalButton.textContent = "×";
    elements.closeModalButton.setAttribute("aria-label", "关闭详情");
    elements.closeFilterConfigButton.textContent = "×";
    elements.closeFilterConfigButton.setAttribute("aria-label", "关闭设置中心");

    if (elements.parentSectionTitle) {
        elements.parentSectionTitle.textContent = "【衍生自 / 主卡牌】";
    }

    if (elements.relatedSectionTitle) {
        elements.relatedSectionTitle.textContent = "【衍生 / 相关牌】";
    }

    if (elements.enchantmentSectionTitle) {
        elements.enchantmentSectionTitle.textContent = "【附魔】";
    }

    if (elements.tagSectionTitle) {
        elements.tagSectionTitle.textContent = "完整标签";
    }

    if (elements.filterConfigTabButton) {
        elements.filterConfigTabButton.textContent = "筛选栏";
    }

    if (elements.cardDataMapTabButton) {
        elements.cardDataMapTabButton.textContent = "映射库";
    }

    elements.backToTopButton.textContent = "置顶";

    syncSettingsHeader();
}

function injectRuntimeStyles() {
    if (document.getElementById("appRuntimeStyles")) {
        return;
    }

    const style = document.createElement("style");
    style.id = "appRuntimeStyles";
    style.textContent = `
        .settings-tabs {
            display: flex;
            flex-wrap: wrap;
            gap: 10px;
        }

        .settings-tab-button {
            min-height: 40px;
            padding: 0 16px;
            border: 1px solid rgba(127, 95, 55, 0.18);
            border-radius: 999px;
            background: rgba(255, 252, 246, 0.88);
            color: var(--ink);
            cursor: pointer;
            font-weight: 700;
            transition: transform 160ms ease, box-shadow 160ms ease, background-color 160ms ease;
        }

        .settings-tab-button.is-active {
            background: rgba(198, 118, 45, 0.16);
            border-color: rgba(198, 118, 45, 0.36);
            color: var(--accent);
            box-shadow: 0 10px 24px rgba(141, 79, 24, 0.08);
        }

        .settings-tab-button:hover {
            transform: translateY(-1px);
        }

        .settings-view.is-hidden {
            display: none;
        }

        .settings-library-card {
            width: 100%;
            border: 0;
            text-align: left;
            cursor: pointer;
            font: inherit;
        }

        .settings-text-hint {
            padding: 12px 14px;
            border-radius: 14px;
            background: rgba(255, 252, 246, 0.88);
            color: var(--muted);
            line-height: 1.7;
        }

        .settings-map-stats {
            display: flex;
            flex-wrap: wrap;
            gap: 10px;
        }

        .settings-stat-chip {
            padding: 8px 12px;
            border-radius: 999px;
            background: rgba(255, 243, 222, 0.92);
            color: var(--accent);
            font-size: 13px;
            font-weight: 700;
        }

        .settings-map-status {
            padding: 12px 14px;
            border-radius: 14px;
            background: rgba(251, 244, 232, 0.82);
            color: var(--muted);
            line-height: 1.7;
        }

        .settings-map-status.is-success {
            background: rgba(224, 241, 220, 0.92);
            color: #2d5a28;
        }

        .settings-map-status.is-warning {
            background: rgba(255, 239, 214, 0.94);
            color: #8d4f18;
        }

        .settings-map-textarea {
            width: 100%;
            min-height: 320px;
            padding: 14px;
            border: 1px solid rgba(127, 95, 55, 0.2);
            border-radius: 16px;
            background: rgba(255, 252, 246, 0.96);
            color: var(--ink);
            font: 14px/1.65 Consolas, "SFMono-Regular", "Cascadia Mono", monospace;
            resize: vertical;
        }

        .settings-set-mode-grid {
            display: flex;
            flex-direction: column;
            gap: 10px;
        }

        .settings-set-mode-head,
        .settings-set-mode-item {
            display: grid;
            grid-template-columns: minmax(0, 1fr) 96px 96px;
            gap: 10px;
            align-items: center;
        }

        .settings-set-mode-head {
            padding: 0 8px;
            color: var(--muted);
            font-size: 13px;
            font-weight: 700;
        }

        .settings-set-mode-item {
            padding: 12px 14px;
            border: 1px solid rgba(127, 95, 55, 0.14);
            border-radius: 14px;
            background: rgba(255, 252, 246, 0.9);
        }

        .settings-set-mode-name {
            line-height: 1.6;
            word-break: break-word;
        }

        .settings-set-mode-toggle {
            display: inline-flex;
            align-items: center;
            justify-content: center;
            gap: 8px;
            min-height: 40px;
            padding: 0 10px;
            border-radius: 12px;
            background: rgba(255, 249, 239, 0.94);
            cursor: pointer;
            font-weight: 700;
        }

        @media (max-width: 720px) {
            .settings-set-mode-head,
            .settings-set-mode-item {
                grid-template-columns: 1fr;
            }

            .settings-set-mode-head {
                display: none;
            }

            .settings-set-mode-toggle {
                justify-content: flex-start;
            }
        }
    `;

    document.head.append(style);
}

function initializeStaticControls() {
    const bootstrap = state.bootstrap;
    if (!bootstrap) {
        return;
    }

    populateSelect(elements.modeSelect, bootstrap.modes, null, "wild");
    populateSelect(elements.costSelect, bootstrap.costs);
    populateSelect(elements.collectibleSelect, bootstrap.collectibleOptions);
    populateSelect(elements.keywordSelect, bootstrap.keywordOptions);
    populateSelect(elements.classSelect, bootstrap.classes, "职业");
    populateSelect(elements.raritySelect, bootstrap.rarities, "稀有度");
    populateSelect(elements.cardTypeSelect, bootstrap.cardTypes, "卡片类型");
    populateSelect(elements.raceSelect, bootstrap.races, "随从种族");
    populateSelect(elements.schoolSelect, bootstrap.schools, "法术派系");
}

function populateSelect(select, items, placeholder = null, preferredValue = "") {
    const currentValue = preferredValue ?? select.value ?? "";
    const fragment = document.createDocumentFragment();

    if (placeholder !== null) {
        fragment.append(createOption("", placeholder));
    }

    for (const item of items) {
        fragment.append(createOption(item.value, item.label));
    }

    select.replaceChildren(fragment);

    const hasPreferredValue = Array.from(select.options).some((option) => option.value === currentValue);
    select.value = hasPreferredValue ? currentValue : "";
}

function createOption(value, label) {
    const option = document.createElement("option");
    option.value = value;
    option.textContent = label;
    return option;
}

function appendIfPresent(params, key, value) {
    if (!value) {
        return;
    }

    params.set(key, value);
}

async function fetchJson(url, options = {}) {
    const response = await fetch(url, {
        ...options,
        headers: {
            Accept: "application/json",
            ...(options.headers ?? {}),
        },
    });

    if (!response.ok) {
        const errorText = await response.text();
        throw new Error(errorText || `请求失败：${response.status}`);
    }

    return response.json();
}

function setSearchBusy(isBusy, statusText = null) {
    elements.searchButton.disabled = isBusy;
    elements.resetButton.disabled = isBusy;

    if (statusText) {
        elements.statusText.textContent = statusText;
    }
}

function showFatalError(error) {
    const message = normalizeErrorMessage(error);
    elements.statusText.textContent = "初始化失败";
    renderEmptyState("应用未能启动", message);
}

function normalizeErrorMessage(error) {
    if (error instanceof Error && error.message) {
        return error.message;
    }

    return "发生了未预期的错误，请检查容器日志。";
}

function formatNumber(value) {
    return new Intl.NumberFormat("zh-CN").format(value);
}

function truncateText(value, maxLength) {
    if (!value || value.length <= maxLength) {
        return value || "";
    }

    return `${value.slice(0, maxLength).trim()}…`;
}

function cloneFilterConfig(config) {
    return config
        ? JSON.parse(JSON.stringify(config))
        : { sections: [] };
}

function hydrateCardDataMapConfig(config) {
    if (!config || !Array.isArray(config.libraries)) {
        return { libraries: [] };
    }

    return {
        libraries: config.libraries.map(normalizeCardDataMapLibrary),
    };
}

function cloneCardDataMapConfig(config) {
    return config
        ? hydrateCardDataMapConfig(JSON.parse(JSON.stringify(config)))
        : { libraries: [] };
}

function normalizeCardDataMapLibrary(library) {
    const overrides = { ...(library.overrides ?? {}) };
    const defaultEntries = { ...(library.defaultEntries ?? {}) };
    const effectiveEntries = mergeMapEntries(defaultEntries, overrides);

    return {
        ...library,
        overrides,
        defaultEntries,
        effectiveEntries,
        defaultCount: Object.keys(defaultEntries).length,
        overrideCount: Object.keys(overrides).length,
        effectiveCount: Object.keys(effectiveEntries).length,
        rawText: typeof library.rawText === "string" ? library.rawText : formatMapOverrides(overrides),
        parseErrors: Array.isArray(library.parseErrors) ? [...library.parseErrors] : [],
        parseWarnings: Array.isArray(library.parseWarnings) ? [...library.parseWarnings] : [],
    };
}

function renderConfiguredFilters() {
    applyModeFilterOptions();
    applySelectFilterOptions("cost", elements.costSelect);
    applySelectFilterOptions("class", elements.classSelect);
    applySelectFilterOptions("rarity", elements.raritySelect);
    applySelectFilterOptions("cardType", elements.cardTypeSelect);
    applySelectFilterOptions("race", elements.raceSelect);
    applySelectFilterOptions("school", elements.schoolSelect);
    applySelectFilterOptions("keyword", elements.keywordSelect);
    applySelectFilterOptions("collectible", elements.collectibleSelect);
    refreshSetOptions();
}

function applyModeFilterOptions() {
    const wrapper = elements.modeSelect.closest(".field");
    const visibleOptions = getVisibleSectionOptions("mode");
    const section = getConfigSection("mode");
    const isVisible = Boolean(section?.enabled) && visibleOptions.length > 0;

    wrapper?.classList.toggle("is-hidden", !isVisible);
    if (!isVisible) {
        elements.modeSelect.value = "";
        return;
    }

    const preferredValue = visibleOptions.some((item) => item.value === elements.modeSelect.value)
        ? elements.modeSelect.value
        : visibleOptions.find((item) => item.value === "wild")?.value ?? visibleOptions[0]?.value ?? "";

    populateSelect(elements.modeSelect, visibleOptions, null, preferredValue);
}

function applySelectFilterOptions(key, select) {
    const wrapper = select.closest(".field");
    const section = getConfigSection(key);
    const visibleOptions = getVisibleSectionOptions(key);
    const isVisible = Boolean(section?.enabled) && visibleOptions.length > 0;

    wrapper?.classList.toggle("is-hidden", !isVisible);
    if (!isVisible) {
        select.value = "";
        return;
    }

    populateSelect(select, visibleOptions, FILTER_FIELD_LABELS[key], select.value);
}

function refreshSetOptions(preferredValue = null) {
    const section = getConfigSection("set");
    const visibleOptions = getVisibleSectionOptions("set");
    const isVisible = Boolean(section?.enabled) && visibleOptions.length > 0;

    elements.setPicker.classList.toggle("is-hidden", !isVisible);
    if (!isVisible) {
        elements.setPickerButton.dataset.value = "";
        elements.setPickerButton.textContent = FILTER_FIELD_LABELS.set;
        elements.setPickerButton.title = FILTER_FIELD_LABELS.set;
        closeSetPicker();
        return;
    }

    renderSetPicker(visibleOptions, preferredValue ?? elements.setPickerButton.dataset.value ?? "");
}

function renderSetPicker(items, preferredValue) {
    const currentValue = preferredValue ?? "";
    const selected = items.find((item) => item.value === currentValue) ?? null;

    elements.setPickerButton.dataset.value = selected?.value ?? "";
    elements.setPickerButton.textContent = selected?.label ?? FILTER_FIELD_LABELS.set;
    elements.setPickerButton.title = selected?.label ?? FILTER_FIELD_LABELS.set;
    elements.setPickerButton.setAttribute("aria-expanded", "false");

    const grid = document.createElement("div");
    grid.className = "set-picker-grid";

    const allButton = document.createElement("button");
    allButton.type = "button";
    allButton.className = `set-picker-option${selected ? "" : " is-selected"}`;
    allButton.textContent = FILTER_FIELD_LABELS.set;
    allButton.addEventListener("click", () => {
        elements.setPickerButton.dataset.value = "";
        elements.setPickerButton.textContent = FILTER_FIELD_LABELS.set;
        elements.setPickerButton.title = FILTER_FIELD_LABELS.set;
        closeSetPicker();
    });
    grid.append(allButton);

    for (const item of items) {
        const option = document.createElement("button");
        option.type = "button";
        option.className = `set-picker-option${item.value === selected?.value ? " is-selected" : ""}`;
        option.textContent = item.label;
        option.title = item.label;
        option.addEventListener("click", () => {
            elements.setPickerButton.dataset.value = item.value;
            elements.setPickerButton.textContent = item.label;
            elements.setPickerButton.title = item.label;
            closeSetPicker();
        });
        grid.append(option);
    }

    elements.setPickerPanel.replaceChildren(grid);
}

function closeSetPicker() {
    elements.setPickerPanel.classList.add("is-hidden");
    elements.setPickerButton.setAttribute("aria-expanded", "false");
}

function openFilterConfigModal() {
    state.draftFilterConfig = cloneFilterConfig(state.filterConfig);
    state.draftCardDataMaps = cloneCardDataMapConfig(state.cardDataMaps);
    ensureActiveConfigSection();
    ensureActiveCardDataMapLibrary();
    renderSettingsModal();
    elements.filterConfigModalPanel.scrollTop = 0;
    elements.filterConfigModal.classList.remove("is-hidden");
    closeSetPicker();
}

function closeFilterConfigModal() {
    elements.filterConfigModal.classList.add("is-hidden");
}

function ensureActiveConfigSection() {
    const sections = state.draftFilterConfig?.sections ?? [];
    if (sections.length === 0) {
        state.activeConfigSectionKey = null;
        return;
    }

    const exists = sections.some((section) => section.key === state.activeConfigSectionKey);
    if (!exists) {
        state.activeConfigSectionKey = sections[0].key;
    }
}

function ensureActiveCardDataMapLibrary() {
    const libraries = state.draftCardDataMaps?.libraries ?? [];
    if (libraries.length === 0) {
        state.activeCardDataMapKey = null;
        return;
    }

    const exists = libraries.some((library) => library.key === state.activeCardDataMapKey);
    if (!exists) {
        state.activeCardDataMapKey = libraries[0].key;
    }
}

function renderSettingsModal() {
    const showFilter = state.activeSettingsView !== SETTINGS_VIEW_MAPS;

    if (elements.filterConfigTabButton) {
        elements.filterConfigTabButton.classList.toggle("is-active", showFilter);
        elements.filterConfigTabButton.setAttribute("aria-selected", String(showFilter));
    }

    if (elements.cardDataMapTabButton) {
        elements.cardDataMapTabButton.classList.toggle("is-active", !showFilter);
        elements.cardDataMapTabButton.setAttribute("aria-selected", String(!showFilter));
    }

    if (elements.filterConfigView) {
        elements.filterConfigView.classList.toggle("is-hidden", !showFilter);
    }

    if (elements.cardDataMapView) {
        elements.cardDataMapView.classList.toggle("is-hidden", showFilter);
    }

    syncSettingsHeader();
    renderFilterConfigEditor();
    renderCardDataMapLibraries();
    renderCardDataMapEditor();
}

function syncSettingsHeader() {
    if (!elements.settingsTitle || !elements.settingsDescription || !elements.settingsHeaderBadge) {
        return;
    }

    if (state.activeSettingsView === SETTINGS_VIEW_MAPS) {
        elements.settingsHeaderBadge.textContent = "映射库";
        elements.settingsTitle.textContent = "在线维护职业、稀有度、种族与扩展包映射";
        elements.settingsDescription.textContent = "这里保存的是覆盖项，不用再改源码。新增职业、种族、法术派系、扩展包代码时，直接在网页里写 key=value 并保存即可。";
        elements.resetFilterConfigButton.textContent = "清空全部覆盖";
    } else {
        elements.settingsHeaderBadge.textContent = "设置中心";
        elements.settingsTitle.textContent = "筛选栏与映射库配置";
        elements.settingsDescription.textContent = "可以控制筛选栏显示内容，也可以直接在网页里维护职业、稀有度、种族、法术派系、扩展包等映射库。";
        elements.resetFilterConfigButton.textContent = "恢复筛选默认";
    }

    elements.saveFilterConfigButton.textContent = "保存全部设置";
}

function renderFilterConfigEditor() {
    renderFilterConfigSections();
    renderFilterConfigOptions();
}

function renderFilterConfigSections() {
    const sections = state.draftFilterConfig?.sections ?? [];
    if (sections.length === 0) {
        elements.filterConfigSectionList.classList.add("settings-placeholder");
        elements.filterConfigSectionList.textContent = "没有可配置的筛选项。";
        return;
    }

    elements.filterConfigSectionList.classList.remove("settings-placeholder");

    const fragment = document.createDocumentFragment();
    for (const section of sections) {
        const card = document.createElement("div");
        card.className = `settings-section-card${section.key === state.activeConfigSectionKey ? " is-active" : ""}`;

        const toggleLabel = document.createElement("label");
        toggleLabel.className = "settings-toggle-label";

        const checkbox = document.createElement("input");
        checkbox.type = "checkbox";
        checkbox.checked = section.enabled;
        checkbox.addEventListener("change", () => {
            section.enabled = checkbox.checked;
            renderFilterConfigSections();
            renderFilterConfigOptions();
        });

        const textWrap = document.createElement("div");
        textWrap.className = "settings-section-meta";

        const title = document.createElement("div");
        title.className = "settings-section-name";
        title.textContent = section.label;

        const count = document.createElement("div");
        count.className = "settings-section-count";
        count.textContent = section.key === "set"
            ? `标准 ${countSetOptionsForMode(section, "standard")} / 狂野 ${countSetOptionsForMode(section, "wild")}`
            : `${section.options.filter((item) => item.visible).length} / ${section.options.length}`;

        textWrap.append(title, count);
        toggleLabel.append(checkbox, textWrap);

        const chooseButton = document.createElement("button");
        chooseButton.type = "button";
        chooseButton.className = "settings-section-open";
        chooseButton.textContent = "编辑";
        chooseButton.addEventListener("click", () => {
            state.activeConfigSectionKey = section.key;
            renderFilterConfigEditor();
        });

        card.append(toggleLabel, chooseButton);
        fragment.append(card);
    }

    elements.filterConfigSectionList.replaceChildren(fragment);
}

function renderFilterConfigOptions() {
    const section = getConfigSection(state.activeConfigSectionKey, state.draftFilterConfig);
    if (!section) {
        elements.filterConfigOptionList.classList.add("settings-placeholder");
        elements.filterConfigOptionList.textContent = "请选择左侧要配置的筛选项。";
        return;
    }

    if (section.key === "set") {
        renderSetFilterConfigOptions(section);
        return;
    }

    elements.filterConfigOptionList.classList.remove("settings-placeholder");

    const wrapper = document.createElement("div");
    wrapper.className = "settings-option-editor";

    const toolbar = document.createElement("div");
    toolbar.className = "settings-option-toolbar";

    const title = document.createElement("div");
    title.className = "settings-option-title";
    title.textContent = section.label;

    const actions = document.createElement("div");
    actions.className = "settings-option-actions";

    const showAllButton = document.createElement("button");
    showAllButton.type = "button";
    showAllButton.className = "secondary-button settings-mini-button";
    showAllButton.textContent = "全部显示";
    showAllButton.addEventListener("click", () => {
        for (const item of section.options) {
            item.visible = true;
        }
        renderFilterConfigEditor();
    });

    const hideAllButton = document.createElement("button");
    hideAllButton.type = "button";
    hideAllButton.className = "secondary-button settings-mini-button";
    hideAllButton.textContent = "全部隐藏";
    hideAllButton.addEventListener("click", () => {
        for (const item of section.options) {
            item.visible = false;
        }
        renderFilterConfigEditor();
    });

    actions.append(showAllButton, hideAllButton);
    toolbar.append(title, actions);

    const grid = document.createElement("div");
    grid.className = "settings-option-grid";

    for (const option of section.options) {
        const label = document.createElement("label");
        label.className = "settings-option-item";

        const checkbox = document.createElement("input");
        checkbox.type = "checkbox";
        checkbox.checked = option.visible;
        checkbox.addEventListener("change", () => {
            option.visible = checkbox.checked;
            renderFilterConfigSections();
        });

        const text = document.createElement("span");
        text.textContent = option.label;

        label.append(checkbox, text);
        grid.append(label);
    }

    wrapper.append(toolbar, grid);
    elements.filterConfigOptionList.replaceChildren(wrapper);
}

function renderSetFilterConfigOptions(section) {
    elements.filterConfigOptionList.classList.remove("settings-placeholder");

    const wrapper = document.createElement("div");
    wrapper.className = "settings-option-editor";

    const toolbar = document.createElement("div");
    toolbar.className = "settings-option-toolbar";

    const title = document.createElement("div");
    title.className = "settings-option-title";
    title.textContent = section.label;

    const actions = document.createElement("div");
    actions.className = "settings-option-actions";

    actions.append(
        createModeBatchButton("标准全选", section, "standard", true),
        createModeBatchButton("标准清空", section, "standard", false),
        createModeBatchButton("狂野全选", section, "wild", true),
        createModeBatchButton("狂野清空", section, "wild", false));

    toolbar.append(title, actions);

    const hint = document.createElement("div");
    hint.className = "settings-text-hint";
    hint.textContent = "这里分别控制标准模式和狂野模式下，扩展包筛选器会显示哪些扩展包；同时也会影响按标准/狂野模式搜索时允许出现哪些扩展包卡牌。";

    const grid = document.createElement("div");
    grid.className = "settings-set-mode-grid";

    const header = document.createElement("div");
    header.className = "settings-set-mode-head";
    header.innerHTML = "<span>扩展包</span><span>标准</span><span>狂野</span>";
    grid.append(header);

    for (const option of section.options) {
        syncSetOptionVisibleFlag(option);

        const row = document.createElement("div");
        row.className = "settings-set-mode-item";

        const name = document.createElement("div");
        name.className = "settings-set-mode-name";
        name.textContent = option.label;

        const standardLabel = document.createElement("label");
        standardLabel.className = "settings-set-mode-toggle";

        const standardCheckbox = document.createElement("input");
        standardCheckbox.type = "checkbox";
        standardCheckbox.checked = Boolean(option.visibleInStandard);
        standardCheckbox.addEventListener("change", () => {
            option.visibleInStandard = standardCheckbox.checked;
            syncSetOptionVisibleFlag(option);
            renderFilterConfigSections();
        });

        const standardText = document.createElement("span");
        standardText.textContent = "标准";
        standardLabel.append(standardCheckbox, standardText);

        const wildLabel = document.createElement("label");
        wildLabel.className = "settings-set-mode-toggle";

        const wildCheckbox = document.createElement("input");
        wildCheckbox.type = "checkbox";
        wildCheckbox.checked = Boolean(option.visibleInWild);
        wildCheckbox.addEventListener("change", () => {
            option.visibleInWild = wildCheckbox.checked;
            syncSetOptionVisibleFlag(option);
            renderFilterConfigSections();
        });

        const wildText = document.createElement("span");
        wildText.textContent = "狂野";
        wildLabel.append(wildCheckbox, wildText);

        row.append(name, standardLabel, wildLabel);
        grid.append(row);
    }

    wrapper.append(toolbar, hint, grid);
    elements.filterConfigOptionList.replaceChildren(wrapper);
}

function createModeBatchButton(label, section, mode, nextValue) {
    const button = document.createElement("button");
    button.type = "button";
    button.className = "secondary-button settings-mini-button";
    button.textContent = label;
    button.addEventListener("click", () => {
        for (const option of section.options) {
            if (mode === "standard") {
                option.visibleInStandard = nextValue;
            } else {
                option.visibleInWild = nextValue;
            }

            syncSetOptionVisibleFlag(option);
        }

        renderFilterConfigEditor();
    });
    return button;
}

function getConfigSection(key, source = state.filterConfig) {
    if (!key) {
        return null;
    }

    return source?.sections?.find((section) => section.key === key) ?? null;
}

function getVisibleSectionOptions(key) {
    const section = getConfigSection(key);
    if (!section) {
        return [];
    }

    if (key === "set") {
        return section.options
            .filter((item) => isSetOptionVisibleForMode(item, getCurrentMode()))
            .map((item) => ({
                value: item.value,
                label: item.label,
            }));
    }

    return section.options.filter((item) => item.visible);
}

function getCurrentMode() {
    return elements.modeSelect.value || "wild";
}

function syncSetOptionVisibleFlag(option) {
    option.visible = Boolean(option.visibleInStandard) || Boolean(option.visibleInWild);
}

function isSetOptionVisibleForMode(option, mode) {
    return mode === "standard"
        ? Boolean(option.visibleInStandard)
        : Boolean(option.visibleInWild);
}

function countSetOptionsForMode(section, mode) {
    return section.options.filter((option) => isSetOptionVisibleForMode(option, mode)).length;
}

function resetFilters() {
    elements.queryInput.value = "";
    elements.costSelect.value = "";
    elements.classSelect.value = "";
    elements.raritySelect.value = "";
    elements.cardTypeSelect.value = "";
    elements.raceSelect.value = "";
    elements.schoolSelect.value = "";
    elements.keywordSelect.value = "";
    elements.collectibleSelect.value = "";

    const visibleModes = getVisibleSectionOptions("mode");
    elements.modeSelect.value = visibleModes.find((item) => item.value === "wild")?.value ?? visibleModes[0]?.value ?? "";
    refreshSetOptions("");
}

function getCardDataMapLibrary(key, source = state.cardDataMaps) {
    if (!key) {
        return null;
    }

    return source?.libraries?.find((library) => library.key === key) ?? null;
}

function renderCardDataMapLibraries() {
    const libraries = state.draftCardDataMaps?.libraries ?? [];
    if (libraries.length === 0) {
        elements.cardDataMapLibraryList.classList.add("settings-placeholder");
        elements.cardDataMapLibraryList.textContent = "没有可编辑的映射库。";
        return;
    }

    elements.cardDataMapLibraryList.classList.remove("settings-placeholder");

    const fragment = document.createDocumentFragment();
    for (const library of libraries) {
        const card = document.createElement("button");
        card.type = "button";
        card.className = `settings-section-card settings-library-card${library.key === state.activeCardDataMapKey ? " is-active" : ""}`;
        card.addEventListener("click", () => {
            state.activeCardDataMapKey = library.key;
            renderSettingsModal();
        });

        const textWrap = document.createElement("div");
        textWrap.className = "settings-section-meta";

        const title = document.createElement("div");
        title.className = "settings-section-name";
        title.textContent = library.label;

        const count = document.createElement("div");
        count.className = "settings-section-count";
        count.textContent = buildCardDataMapLibrarySummary(library);

        textWrap.append(title, count);
        card.append(textWrap);
        fragment.append(card);
    }

    elements.cardDataMapLibraryList.replaceChildren(fragment);
}

function buildCardDataMapLibrarySummary(library) {
    const summary = `默认 ${library.defaultCount} / 覆盖 ${library.overrideCount} / 生效 ${library.effectiveCount}`;
    if (library.parseErrors.length > 0) {
        return `${summary} · 有 ${library.parseErrors.length} 处格式问题`;
    }

    if (library.parseWarnings.length > 0) {
        return `${summary} · ${library.parseWarnings[0]}`;
    }

    return summary;
}

function renderCardDataMapEditor() {
    const library = getCardDataMapLibrary(state.activeCardDataMapKey, state.draftCardDataMaps);
    if (!library) {
        elements.cardDataMapEditor.classList.add("settings-placeholder");
        elements.cardDataMapEditor.textContent = "请选择左侧要编辑的映射库。";
        return;
    }

    elements.cardDataMapEditor.classList.remove("settings-placeholder");

    const wrapper = document.createElement("div");
    wrapper.className = "settings-option-editor";

    const toolbar = document.createElement("div");
    toolbar.className = "settings-option-toolbar";

    const titleWrap = document.createElement("div");

    const title = document.createElement("div");
    title.className = "settings-option-title";
    title.textContent = library.label;

    const description = document.createElement("div");
    description.className = "settings-section-count";
    description.textContent = library.description;

    titleWrap.append(title, description);

    const actions = document.createElement("div");
    actions.className = "settings-option-actions";

    const clearButton = document.createElement("button");
    clearButton.type = "button";
    clearButton.className = "secondary-button settings-mini-button";
    clearButton.textContent = "清空当前库覆盖";
    clearButton.addEventListener("click", () => {
        updateCardDataMapLibraryDraft(library, "");
        renderSettingsModal();
    });

    actions.append(clearButton);
    toolbar.append(titleWrap, actions);

    const hint = document.createElement("div");
    hint.className = "settings-text-hint";
    hint.textContent = "每行一条，格式为 key=value。这里只写新增或覆盖项即可，不需要把默认库整份复制过来。空行和 # 开头的注释行会被忽略。";

    const stats = document.createElement("div");
    stats.className = "settings-map-stats";

    const defaultChip = createMapStatChip();
    const overrideChip = createMapStatChip();
    const effectiveChip = createMapStatChip();
    stats.append(defaultChip, overrideChip, effectiveChip);

    const status = document.createElement("div");
    status.className = "settings-map-status";

    const textarea = document.createElement("textarea");
    textarea.className = "settings-map-textarea";
    textarea.spellcheck = false;
    textarea.value = library.rawText;

    const copyActions = document.createElement("div");
    copyActions.className = "settings-option-actions";

    const copyOverridesButton = createPreviewActionButton("复制当前覆盖", () => {
        void copyText(
            library.rawText.trim() || "当前没有覆盖项。",
            `已复制 ${library.label} 的当前覆盖`,
            copyOverridesButton);
    });

    const copyEffectiveButton = createPreviewActionButton("复制当前生效库", () => {
        void copyText(
            formatMapOverrides(library.effectiveEntries) || "当前没有可显示条目。",
            `已复制 ${library.label} 的当前生效库`,
            copyEffectiveButton);
    });

    const copyDefaultButton = createPreviewActionButton("复制默认库", () => {
        void copyText(
            formatMapOverrides(library.defaultEntries) || "默认库为空。",
            `已复制 ${library.label} 的默认库`,
            copyDefaultButton);
    });

    copyActions.append(copyOverridesButton, copyEffectiveButton, copyDefaultButton);

    const effectivePreview = createMapPreviewBlock();
    const defaultPreview = createMapPreviewBlock();

    function refreshEditorState() {
        defaultChip.textContent = `默认 ${formatNumber(library.defaultCount)} 条`;
        overrideChip.textContent = `覆盖 ${formatNumber(library.overrideCount)} 条`;
        effectiveChip.textContent = `生效 ${formatNumber(library.effectiveCount)} 条`;

        const statusInfo = describeCardDataMapDraftState(library);
        status.className = `settings-map-status${statusInfo.tone ? ` is-${statusInfo.tone}` : ""}`;
        status.textContent = statusInfo.text;

        effectivePreview.summary.textContent = `查看当前生效条目（${formatNumber(library.effectiveCount)}）`;
        effectivePreview.body.textContent = formatMapOverrides(library.effectiveEntries) || "当前没有可显示条目。";

        defaultPreview.summary.textContent = `查看默认条目（${formatNumber(library.defaultCount)}）`;
        defaultPreview.body.textContent = formatMapOverrides(library.defaultEntries) || "默认库为空。";
    }

    textarea.addEventListener("input", () => {
        updateCardDataMapLibraryDraft(library, textarea.value);
        renderCardDataMapLibraries();
        refreshEditorState();
    });

    refreshEditorState();

    wrapper.append(
        toolbar,
        hint,
        stats,
        status,
        textarea,
        copyActions,
        effectivePreview.element,
        defaultPreview.element);

    elements.cardDataMapEditor.replaceChildren(wrapper);
}

function createMapStatChip() {
    const chip = document.createElement("div");
    chip.className = "settings-stat-chip";
    return chip;
}

function createPreviewActionButton(label, onClick) {
    const button = document.createElement("button");
    button.type = "button";
    button.className = "secondary-button settings-mini-button";
    button.textContent = label;
    button.addEventListener("click", onClick);
    return button;
}

function createMapPreviewBlock() {
    const details = document.createElement("details");
    details.className = "settings-text-hint";

    const summary = document.createElement("summary");
    summary.style.cursor = "pointer";
    summary.style.fontWeight = "700";

    const body = document.createElement("pre");
    body.style.margin = "12px 0 0";
    body.style.overflow = "auto";
    body.style.whiteSpace = "pre-wrap";
    body.style.wordBreak = "break-word";
    body.style.font = '13px/1.65 Consolas, "SFMono-Regular", "Cascadia Mono", monospace';

    details.append(summary, body);
    return { element: details, summary, body };
}

function updateCardDataMapLibraryDraft(library, rawText) {
    const parsed = parseMapOverrideText(rawText);
    library.rawText = rawText;
    library.overrides = parsed.overrides;
    library.parseErrors = parsed.errors;
    library.parseWarnings = parsed.warnings;
    library.overrideCount = Object.keys(parsed.overrides).length;
    library.effectiveEntries = mergeMapEntries(library.defaultEntries, parsed.overrides);
    library.effectiveCount = Object.keys(library.effectiveEntries).length;
}

function describeCardDataMapDraftState(library) {
    if (library.parseErrors.length > 0) {
        return {
            tone: "warning",
            text: `当前有 ${library.parseErrors.length} 行格式不正确。${library.parseErrors[0]}。保存前请修正，格式必须是 key=value。`,
        };
    }

    if (library.parseWarnings.length > 0) {
        return {
            tone: "warning",
            text: `已识别 ${library.overrideCount} 条覆盖。${library.parseWarnings[0]}。`,
        };
    }

    if (library.overrideCount === 0) {
        return {
            tone: "",
            text: "当前没有覆盖项。保存后会完全使用内置默认库。",
        };
    }

    return {
        tone: "success",
        text: `已识别 ${library.overrideCount} 条覆盖。保存后会优先使用这里的值覆盖默认库。`,
    };
}

function formatMapOverrides(entries) {
    return Object.entries(entries ?? {})
        .sort((left, right) => left[0].localeCompare(right[0], "zh-CN"))
        .map(([key, value]) => `${key}=${value}`)
        .join("\n");
}

function parseMapOverrideText(text) {
    const overrides = {};
    const errors = [];
    const warnings = [];
    const seenKeys = new Map();
    const lines = (text ?? "").split(/\r?\n/);

    lines.forEach((rawLine, index) => {
        const lineNumber = index + 1;
        const line = rawLine.trim();
        if (!line || line.startsWith("#")) {
            return;
        }

        const separatorIndex = rawLine.indexOf("=");
        if (separatorIndex <= 0) {
            errors.push(`第 ${lineNumber} 行缺少等号`);
            return;
        }

        const key = rawLine.slice(0, separatorIndex).trim();
        const value = rawLine.slice(separatorIndex + 1).trim();

        if (!key) {
            errors.push(`第 ${lineNumber} 行的 key 为空`);
            return;
        }

        if (!value) {
            errors.push(`第 ${lineNumber} 行的 value 为空`);
            return;
        }

        if (seenKeys.has(key)) {
            warnings.push(`键 ${key} 重复出现，已以后面的值为准`);
        }

        seenKeys.set(key, lineNumber);
        overrides[key] = value;
    });

    return { overrides, errors, warnings };
}

function mergeMapEntries(defaultEntries, overrides) {
    return {
        ...(defaultEntries ?? {}),
        ...(overrides ?? {}),
    };
}

async function resetSettingsDraft() {
    if (state.activeSettingsView === SETTINGS_VIEW_MAPS) {
        state.draftCardDataMaps = cloneCardDataMapConfig(state.cardDataMaps);
        for (const library of state.draftCardDataMaps.libraries) {
            updateCardDataMapLibraryDraft(library, "");
        }
        ensureActiveCardDataMapLibrary();
        renderSettingsModal();
        return;
    }

    state.draftFilterConfig = await fetchJson("/api/filter-bar-config/default");
    ensureActiveConfigSection();
    renderSettingsModal();
}

async function saveSettings() {
    validateCardDataMapDraft(state.draftCardDataMaps);

    const mapChanged = hasCardDataMapChanges();
    const filterChanged = hasFilterConfigChanges();

    let savedMaps = state.cardDataMaps;
    let savedFilterConfig = state.filterConfig;

    if (mapChanged) {
        savedMaps = hydrateCardDataMapConfig(await fetchJson("/api/card-data-maps", {
            method: "PUT",
            headers: {
                "Content-Type": "application/json",
            },
            body: JSON.stringify(buildCardDataMapOverridePayload(state.draftCardDataMaps)),
        }));
    }

    if (filterChanged) {
        savedFilterConfig = await fetchJson("/api/filter-bar-config", {
            method: "PUT",
            headers: {
                "Content-Type": "application/json",
            },
            body: JSON.stringify(state.draftFilterConfig),
        });
    } else if (mapChanged) {
        savedFilterConfig = await fetchJson("/api/filter-bar-config");
    }

    if (mapChanged) {
        state.bootstrap = await fetchJson("/api/bootstrap");
    }

    state.cardDataMaps = savedMaps;
    state.draftCardDataMaps = cloneCardDataMapConfig(savedMaps);
    state.filterConfig = savedFilterConfig;
    state.draftFilterConfig = cloneFilterConfig(savedFilterConfig);

    initializeStaticControls();
    renderConfiguredFilters();
    ensureActiveConfigSection();
    ensureActiveCardDataMapLibrary();
    renderSettingsModal();
    await searchCards();

    if (mapChanged && state.activeDetail && !elements.detailModal.classList.contains("is-hidden")) {
        await openDetail(state.activeDetail.cardId);
    }

    closeFilterConfigModal();

    if (mapChanged && filterChanged) {
        showCopyToast("筛选栏设置和映射库已保存", elements.saveFilterConfigButton);
    } else if (mapChanged) {
        showCopyToast("映射库已保存", elements.saveFilterConfigButton);
    } else if (filterChanged) {
        showCopyToast("筛选栏设置已保存", elements.saveFilterConfigButton);
    } else {
        showCopyToast("没有检测到需要保存的变更", elements.saveFilterConfigButton);
    }
}

function validateCardDataMapDraft(config) {
    for (const library of config?.libraries ?? []) {
        if (library.parseErrors.length > 0) {
            throw new Error(`映射库「${library.label}」存在格式问题：${library.parseErrors[0]}`);
        }
    }
}

function hasFilterConfigChanges() {
    return JSON.stringify(state.draftFilterConfig ?? { sections: [] })
        !== JSON.stringify(state.filterConfig ?? { sections: [] });
}

function hasCardDataMapChanges() {
    return JSON.stringify(buildCardDataMapOverridePayload(state.draftCardDataMaps))
        !== JSON.stringify(buildCardDataMapOverridePayload(state.cardDataMaps));
}

function buildCardDataMapOverridePayload(config) {
    const payload = Object.fromEntries(CARD_DATA_MAP_KEYS.map((key) => [key, {}]));

    for (const library of config?.libraries ?? []) {
        if (CARD_DATA_MAP_KEYS.includes(library.key)) {
            payload[library.key] = { ...(library.overrides ?? {}) };
        }
    }

    return payload;
}

async function searchCards() {
    if (!state.bootstrap) {
        return;
    }

    setSearchBusy(true, "正在筛选卡牌…");

    try {
        const params = new URLSearchParams();
        appendIfPresent(params, "query", elements.queryInput.value.trim());
        appendIfPresent(params, "mode", elements.modeSelect.closest(".field")?.classList.contains("is-hidden") ? "" : elements.modeSelect.value);
        appendIfPresent(params, "cost", elements.costSelect.value);
        appendIfPresent(params, "class", elements.classSelect.value);
        appendIfPresent(params, "set", elements.setPicker.classList.contains("is-hidden") ? "" : elements.setPickerButton.dataset.value);
        appendIfPresent(params, "rarity", elements.raritySelect.value);
        appendIfPresent(params, "cardType", elements.cardTypeSelect.value);
        appendIfPresent(params, "race", elements.raceSelect.value);
        appendIfPresent(params, "school", elements.schoolSelect.value);
        appendIfPresent(params, "keyword", elements.keywordSelect.value);
        appendIfPresent(params, "collectible", elements.collectibleSelect.value);
        params.set("limit", String(state.bootstrap.maxDisplay));

        const response = await fetchJson(`/api/cards?${params.toString()}`);
        elements.statusText.textContent = response.displayedCount === 0
            ? `卡牌库共 ${formatNumber(response.totalCards)} 张，未找到符合条件的卡牌。`
            : `卡牌库共 ${formatNumber(response.totalCards)} 张，当前显示 ${formatNumber(response.displayedCount)} 张卡牌。`;

        renderResults(response.items);
    } catch (error) {
        elements.statusText.textContent = "筛选失败";
        renderEmptyState("搜索失败", normalizeErrorMessage(error));
    } finally {
        setSearchBusy(false);
    }
}

function renderResults(items) {
    elements.results.replaceChildren();

    if (!items || items.length === 0) {
        renderEmptyState("没有找到符合条件的卡牌", "可以试试放宽筛选条件，或者直接输入卡牌中文名、英文名、CardID。");
        return;
    }

    for (const item of items) {
        const tile = document.createElement("article");
        tile.className = item.hasImage && item.imageUrl ? "card-image-tile" : "card-fallback";

        const button = document.createElement("button");
        button.className = "card-button";
        button.type = "button";
        button.title = `${item.nameZh}\nCardID: ${item.cardId}\nID: ${item.dbfId}`;
        button.addEventListener("click", () => {
            void openDetail(item.cardId);
        });

        if (item.hasImage && item.imageUrl) {
            const image = createImageElement(item.imageUrl, item.nameZh, item.nameZh);
            image.className = "card-image";
            button.append(image);
        } else {
            const title = document.createElement("h3");
            title.className = "card-fallback-title";
            title.textContent = item.nameZh || item.nameEn || item.cardId;

            const description = document.createElement("div");
            description.className = "card-fallback-text";
            description.textContent = item.textZh || "暂无图片索引";

            button.append(title, description);
        }

        tile.append(button);
        elements.results.append(tile);
    }
}

function renderEmptyState(title, description) {
    const wrapper = document.createElement("article");
    wrapper.className = "empty-state";

    const heading = document.createElement("h3");
    heading.textContent = title;

    const text = document.createElement("p");
    text.textContent = description;

    wrapper.append(heading, text);
    elements.results.replaceChildren(wrapper);
}

async function openDetail(cardId) {
    elements.detailModal.classList.remove("is-hidden");
    elements.detailModalPanel.scrollTop = 0;
    elements.detailBadge.textContent = "正在加载详情";
    elements.copyNameButton.textContent = "读取中…";
    elements.copyCardIdButton.textContent = cardId;
    elements.copyDbfIdButton.textContent = "-";
    elements.detailDescription.textContent = "请稍候，正在从服务器读取这张卡牌的详细信息。";
    elements.detailVisual.replaceChildren(createPlaceholder("加载中", "正在读取卡牌图片和关联信息。"));
    hideSections();

    try {
        const detail = await fetchJson(`/api/cards/${encodeURIComponent(cardId)}`);
        state.activeDetail = detail;
        renderDetail(detail);
    } catch (error) {
        state.activeDetail = null;
        elements.detailBadge.textContent = "加载失败";
        elements.copyNameButton.textContent = "无法打开详情";
        elements.detailDescription.textContent = normalizeErrorMessage(error);
        elements.detailVisual.replaceChildren(createPlaceholder("加载失败", normalizeErrorMessage(error)));
        elements.tagList.replaceChildren();
    }
}

function renderDetail(detail) {
    elements.detailBadge.textContent = detail.isEnchantment ? "附魔" : "卡牌详情";
    elements.copyNameButton.textContent = detail.name;
    elements.copyCardIdButton.textContent = detail.cardId;
    elements.copyDbfIdButton.textContent = String(detail.dbfId);
    elements.detailDescription.textContent = detail.text || "（无描述）";

    elements.detailVisual.replaceChildren(
        detail.hasImage && detail.imageUrl
            ? createImageElement(detail.imageUrl, detail.name, detail.name)
            : createPlaceholder(detail.name, detail.text || "暂无图片索引"));

    renderLinkSection(elements.parentSection, elements.parentLinks, detail.parentCards, true);
    renderLinkSection(elements.relatedSection, elements.relatedLinks, detail.relatedCards, true);
    renderLinkSection(elements.enchantmentSection, elements.enchantmentLinks, detail.enchantmentCards, false);
    renderTags(detail.tags);
}

function renderLinkSection(section, container, items, enablePreview) {
    container.replaceChildren();

    if (!items || items.length === 0) {
        section.classList.add("is-hidden");
        return;
    }

    section.classList.remove("is-hidden");

    for (const item of items) {
        const wrap = document.createElement("div");
        wrap.className = "related-link-wrap";

        const button = document.createElement("button");
        button.className = "related-link-button";
        button.type = "button";
        button.textContent = item.name;
        button.title = item.reason;
        button.addEventListener("click", () => {
            void openDetail(item.cardId);
        });

        wrap.append(button);

        if (enablePreview && item.hasImage && item.imageUrl) {
            const preview = document.createElement("div");
            preview.className = "related-link-preview";
            preview.append(createImageElement(item.imageUrl, item.name, item.name));
            wrap.append(preview);
        }

        container.append(wrap);
    }
}

function renderTags(tags) {
    elements.tagList.replaceChildren();

    if (!tags || tags.length === 0) {
        const line = document.createElement("div");
        line.className = "tag-line";
        line.textContent = "暂无标签";
        elements.tagList.append(line);
        return;
    }

    for (const tag of tags) {
        const text = `${tag.displayName} = ${tag.value}`;
        if (tag.targetCardId) {
            const button = document.createElement("button");
            button.type = "button";
            button.className = "tag-line-button";
            button.textContent = `${text}  →  ${tag.targetCardId}`;
            button.title = `打开关联卡牌 ${tag.targetCardId}`;
            button.addEventListener("click", () => {
                void openDetail(tag.targetCardId);
            });
            elements.tagList.append(button);
            continue;
        }

        const line = document.createElement("div");
        line.className = "tag-line";
        line.textContent = text;
        elements.tagList.append(line);
    }
}

function hideSections() {
    elements.parentSection.classList.add("is-hidden");
    elements.relatedSection.classList.add("is-hidden");
    elements.enchantmentSection.classList.add("is-hidden");
}

function closeModal() {
    elements.detailModal.classList.add("is-hidden");
}

function createImageElement(url, alt, fallbackTitle) {
    const image = document.createElement("img");
    image.loading = "lazy";
    image.src = url;
    image.alt = alt;
    image.addEventListener("error", () => {
        image.replaceWith(createPlaceholder(fallbackTitle, "图片加载失败，已切换为文字占位。"));
    });
    return image;
}

function createPlaceholder(title, description) {
    const block = document.createElement("div");
    block.className = "detail-placeholder";
    block.textContent = `${title || "暂无图片"}\n\n${truncateText(description || "暂无描述", 160)}`;
    return block;
}

async function copyText(value, successMessage, anchorElement) {
    try {
        if (navigator.clipboard?.writeText) {
            await navigator.clipboard.writeText(value);
        } else {
            fallbackCopyText(value);
        }

        showCopyToast(successMessage, anchorElement);
    } catch {
        try {
            fallbackCopyText(value);
            showCopyToast(successMessage, anchorElement);
        } catch {
            showCopyToast(`复制失败，请手动复制：${value}`, anchorElement);
        }
    }
}

function fallbackCopyText(value) {
    const textarea = document.createElement("textarea");
    textarea.value = value;
    textarea.setAttribute("readonly", "true");
    textarea.style.position = "fixed";
    textarea.style.top = "-1000px";
    textarea.style.left = "-1000px";
    document.body.append(textarea);
    textarea.select();
    textarea.setSelectionRange(0, value.length);

    const copied = document.execCommand("copy");
    textarea.remove();

    if (!copied) {
        throw new Error("copy failed");
    }
}

function showCopyToast(message, anchorElement) {
    if (copyToastTimer) {
        window.clearTimeout(copyToastTimer);
    }

    elements.copyToast.textContent = message;
    elements.copyToast.classList.remove("is-hidden");

    const fallbackLeft = Math.max(12, window.innerWidth - 280);
    const fallbackTop = Math.max(12, window.innerHeight - 84);

    if (anchorElement instanceof HTMLElement) {
        const rect = anchorElement.getBoundingClientRect();
        const left = Math.min(Math.max(12, rect.left), window.innerWidth - 280);
        const top = Math.min(window.innerHeight - 54, rect.bottom + 10);
        elements.copyToast.style.left = `${left}px`;
        elements.copyToast.style.top = `${top}px`;
    } else {
        elements.copyToast.style.left = `${fallbackLeft}px`;
        elements.copyToast.style.top = `${fallbackTop}px`;
    }

    copyToastTimer = window.setTimeout(() => {
        elements.copyToast.classList.add("is-hidden");
    }, 1800);
}
