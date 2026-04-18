const state = {
    bootstrap: null,
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
    collectibleSelect: document.getElementById("collectibleSelect"),
    keywordSelect: document.getElementById("keywordSelect"),
    searchButton: document.getElementById("searchButton"),
    resetButton: document.getElementById("resetButton"),
    statusText: document.getElementById("statusText"),
    results: document.getElementById("results"),
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
        const bootstrap = await fetchJson("/api/bootstrap");
        state.bootstrap = bootstrap;

        populateSelect(elements.modeSelect, bootstrap.modes, null, "wild");
        populateSelect(elements.costSelect, bootstrap.costs);
        populateSelect(elements.collectibleSelect, bootstrap.collectibleOptions);
        populateSelect(elements.keywordSelect, bootstrap.keywordOptions);
        populateSelect(elements.classSelect, bootstrap.classes, "职业");
        populateSelect(elements.raritySelect, bootstrap.rarities, "稀有度");
        populateSelect(elements.cardTypeSelect, bootstrap.cardTypes, "卡牌类型");
        populateSelect(elements.raceSelect, bootstrap.races, "随从种族");
        populateSelect(elements.schoolSelect, bootstrap.schools, "法术派系");

        refreshSetOptions();
        await searchCards();
    } catch (error) {
        showFatalError(error);
    }
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
        if (!state.activeDetail) {
            return;
        }

        void copyText(state.activeDetail.name, `已复制到剪贴板: ${state.activeDetail.name}`, event.currentTarget);
    });

    elements.copyCardIdButton.addEventListener("click", (event) => {
        if (!state.activeDetail) {
            return;
        }

        void copyText(state.activeDetail.cardId, `已复制到剪贴板: ${state.activeDetail.cardId}`, event.currentTarget);
    });

    elements.copyDbfIdButton.addEventListener("click", (event) => {
        if (!state.activeDetail) {
            return;
        }

        void copyText(String(state.activeDetail.dbfId), `已复制到剪贴板: ${state.activeDetail.dbfId}`, event.currentTarget);
    });
}

function resetFilters() {
    elements.queryInput.value = "";
    elements.modeSelect.value = "wild";
    elements.costSelect.value = "";
    elements.classSelect.value = "";
    elements.raritySelect.value = "";
    elements.cardTypeSelect.value = "";
    elements.raceSelect.value = "";
    elements.schoolSelect.value = "";
    elements.collectibleSelect.value = "";
    elements.keywordSelect.value = "";
    refreshSetOptions("");
}

function refreshSetOptions(preferredValue = null) {
    if (!state.bootstrap) {
        return;
    }

    const sets = elements.modeSelect.value === "standard"
        ? state.bootstrap.standardSets
        : state.bootstrap.wildSets;

    renderSetPicker(sets, preferredValue ?? elements.setPickerButton.dataset.value ?? "");
}

async function searchCards() {
    if (!state.bootstrap) {
        return;
    }

    setSearchBusy(true, "正在筛选卡牌…", "服务器正在读取搜索结果，请稍候。");

    try {
        const params = new URLSearchParams();

        appendIfPresent(params, "query", elements.queryInput.value);
        appendIfPresent(params, "mode", elements.modeSelect.value || "wild");
        appendIfPresent(params, "cost", elements.costSelect.value);
        appendIfPresent(params, "class", elements.classSelect.value);
        appendIfPresent(params, "set", elements.setPickerButton.dataset.value);
        appendIfPresent(params, "rarity", elements.raritySelect.value);
        appendIfPresent(params, "cardType", elements.cardTypeSelect.value);
        appendIfPresent(params, "race", elements.raceSelect.value);
        appendIfPresent(params, "school", elements.schoolSelect.value);
        appendIfPresent(params, "collectible", elements.collectibleSelect.value);
        appendIfPresent(params, "keyword", elements.keywordSelect.value);
        params.set("limit", String(state.bootstrap.maxDisplay));

        const response = await fetchJson(`/api/cards?${params.toString()}`);
        if (response.displayedCount === 0) {
            elements.statusText.textContent = `卡牌库共 ${formatNumber(response.totalCards)} 张。未找到符合条件的卡牌。`;
        } else {
            elements.statusText.textContent = `卡牌库共 ${formatNumber(response.totalCards)} 张，当前显示 ${formatNumber(response.displayedCount)} 张卡牌。`;
        }

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
        elements.detailModalPanel.scrollTop = 0;
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
            : createPlaceholder(detail.name, detail.text || "暂无图片索引")
    );

    renderLinkSection(elements.parentSection, elements.parentLinks, detail.parentCards, true);
    renderLinkSection(elements.relatedSection, elements.relatedLinks, detail.relatedCards, true);
    renderLinkSection(elements.enchantmentSection, elements.enchantmentLinks, detail.enchantmentCards, false);
    renderTags(detail.tags);
}

function renderSetPicker(items, preferredValue) {
    const currentValue = preferredValue ?? "";
    const selected = items.find((item) => item.value === currentValue) ?? null;
    elements.setPickerButton.dataset.value = selected?.value ?? "";
    elements.setPickerButton.textContent = selected?.label ?? "扩展包";
    elements.setPickerButton.title = selected?.label ?? "扩展包";
    elements.setPickerButton.setAttribute("aria-expanded", "false");

    const grid = document.createElement("div");
    grid.className = "set-picker-grid";

    const allButton = document.createElement("button");
    allButton.type = "button";
    allButton.className = `set-picker-option${selected ? "" : " is-selected"}`;
    allButton.textContent = "扩展包";
    allButton.addEventListener("click", () => {
        elements.setPickerButton.dataset.value = "";
        elements.setPickerButton.textContent = "扩展包";
        elements.setPickerButton.title = "扩展包";
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
            const image = createImageElement(item.imageUrl, item.name, item.name);
            preview.append(image);
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

function createImageElement(url, alt, fallbackTitle) {
    const image = document.createElement("img");
    image.loading = "lazy";
    image.src = url;
    image.alt = alt;
    image.addEventListener("error", () => {
        image.replaceWith(createPlaceholder(fallbackTitle, "图片加载失败，改为显示文字占位。"));
    });
    return image;
}

function createPlaceholder(title, description) {
    const block = document.createElement("div");
    block.className = "detail-placeholder";
    block.textContent = `${title || "暂无图片"}\n\n${truncateText(description || "暂无描述", 160)}`;
    return block;
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

function setSearchBusy(isBusy, statusText = null, hintText = null) {
    elements.searchButton.disabled = isBusy;
    elements.resetButton.disabled = isBusy;

    if (statusText) {
        elements.statusText.textContent = statusText;
    }
}

function closeModal() {
    elements.detailModal.classList.add("is-hidden");
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

    const fallbackLeft = Math.max(12, window.innerWidth - 260);
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

async function fetchJson(url) {
    const response = await fetch(url, {
        headers: {
            Accept: "application/json",
        },
    });

    if (!response.ok) {
        const errorText = await response.text();
        throw new Error(errorText || `请求失败：${response.status}`);
    }

    return response.json();
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

function truncateText(value, maxLength) {
    if (!value || value.length <= maxLength) {
        return value || "";
    }

    return `${value.slice(0, maxLength).trim()}…`;
}

function formatNumber(value) {
    return new Intl.NumberFormat("zh-CN").format(value);
}
