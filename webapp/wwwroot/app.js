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

const state = {
    bootstrap: null,
    filterConfig: null,
    draftFilterConfig: null,
    activeConfigSectionKey: null,
    activeDetail: null,
};

const elements = {
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
    filterConfigSectionList: document.getElementById("filterConfigSectionList"),
    filterConfigOptionList: document.getElementById("filterConfigOptionList"),
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
    parentLinks: document.getElementById("parentLinks"),
    relatedLinks: document.getElementById("relatedLinks"),
    enchantmentLinks: document.getElementById("enchantmentLinks"),
    tagList: document.getElementById("tagList"),
};

let copyToastTimer = null;

void initialize();

async function initialize() {
    bindEvents();

    try {
        state.bootstrap = await fetchJson("/api/bootstrap");
        state.filterConfig = await fetchJson("/api/filter-bar-config");
        state.draftFilterConfig = cloneFilterConfig(state.filterConfig);
    } catch (error) {
        showFatalError(error);
        return;
    }

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
            state.draftFilterConfig = await fetchJson("/api/filter-bar-config/default");
            ensureActiveConfigSection();
            renderFilterConfigEditor();
        } catch (error) {
            showCopyToast(normalizeErrorMessage(error), elements.resetFilterConfigButton);
        }
    });

    elements.saveFilterConfigButton.addEventListener("click", async () => {
        try {
            await saveFilterConfig();
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

    wrapper.classList.toggle("is-hidden", !isVisible);
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

    wrapper.classList.toggle("is-hidden", !isVisible);
    if (!isVisible) {
        select.value = "";
        return;
    }

    populateSelect(
        select,
        visibleOptions,
        FILTER_FIELD_LABELS[key],
        select.value);
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
    ensureActiveConfigSection();
    renderFilterConfigEditor();
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
        count.textContent = `${section.options.filter((item) => item.visible).length} / ${section.options.length}`;

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
        const visibleValues = new Set(
            section.options
                .filter((item) => item.visible)
                .map((item) => item.value));

        const baseOptions = getCurrentMode() === "standard"
            ? state.bootstrap.standardSets
            : state.bootstrap.wildSets;

        return baseOptions.filter((item) => visibleValues.has(item.value));
    }

    return section.options.filter((item) => item.visible);
}

function getCurrentMode() {
    return elements.modeSelect.value || "wild";
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

async function saveFilterConfig() {
    const saved = await fetchJson("/api/filter-bar-config", {
        method: "PUT",
        headers: {
            "Content-Type": "application/json",
        },
        body: JSON.stringify(state.draftFilterConfig),
    });

    state.filterConfig = saved;
    state.draftFilterConfig = cloneFilterConfig(saved);
    renderConfiguredFilters();
    resetFilters();
    await searchCards();
    closeFilterConfigModal();
    showCopyToast("筛选栏设置已保存", elements.saveFilterConfigButton);
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
        const line = document.createElement("div");
        line.className = "tag-line";
        line.textContent = `${tag.displayName} = ${tag.value}`;
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
