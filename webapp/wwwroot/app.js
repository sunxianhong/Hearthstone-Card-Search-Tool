const state = {
    bootstrap: null,
    activeDetail: null,
};

const elements = {
    totalCardsValue: document.getElementById("totalCardsValue"),
    maxDisplayValue: document.getElementById("maxDisplayValue"),
    queryInput: document.getElementById("queryInput"),
    modeSelect: document.getElementById("modeSelect"),
    setSelect: document.getElementById("setSelect"),
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
    hintText: document.getElementById("hintText"),
    searchModeBadge: document.getElementById("searchModeBadge"),
    results: document.getElementById("results"),
    detailModal: document.getElementById("detailModal"),
    closeModalButton: document.getElementById("closeModalButton"),
    detailBadge: document.getElementById("detailBadge"),
    detailName: document.getElementById("detailName"),
    detailDescription: document.getElementById("detailDescription"),
    detailCardId: document.getElementById("detailCardId"),
    detailDbfId: document.getElementById("detailDbfId"),
    detailVisual: document.getElementById("detailVisual"),
    parentSection: document.getElementById("parentSection"),
    relatedSection: document.getElementById("relatedSection"),
    enchantmentSection: document.getElementById("enchantmentSection"),
    parentLinks: document.getElementById("parentLinks"),
    relatedLinks: document.getElementById("relatedLinks"),
    enchantmentLinks: document.getElementById("enchantmentLinks"),
    tagTableBody: document.getElementById("tagTableBody"),
    copyCardIdButton: document.getElementById("copyCardIdButton"),
    copyDbfIdButton: document.getElementById("copyDbfIdButton"),
};

void initialize();

async function initialize() {
    bindEvents();

    try {
        const bootstrap = await fetchJson("/api/bootstrap");
        state.bootstrap = bootstrap;

        elements.totalCardsValue.textContent = formatNumber(bootstrap.totalCards);
        elements.maxDisplayValue.textContent = String(bootstrap.maxDisplay);

        populateSelect(elements.modeSelect, bootstrap.modes, null, "wild");
        populateSelect(elements.costSelect, bootstrap.costs);
        populateSelect(elements.collectibleSelect, bootstrap.collectibleOptions);
        populateSelect(elements.keywordSelect, bootstrap.keywordOptions);
        populateSelect(elements.classSelect, bootstrap.classes, "全部职业");
        populateSelect(elements.raritySelect, bootstrap.rarities, "全部稀有度");
        populateSelect(elements.cardTypeSelect, bootstrap.cardTypes, "全部卡牌类型");
        populateSelect(elements.raceSelect, bootstrap.races, "全部随从种族");
        populateSelect(elements.schoolSelect, bootstrap.schools, "全部法术派系");

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

    elements.queryInput.addEventListener("keydown", (event) => {
        if (event.key !== "Enter") {
            return;
        }

        event.preventDefault();
        void searchCards();
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
        if (event.key === "Escape" && !elements.detailModal.classList.contains("is-hidden")) {
            closeModal();
        }
    });

    elements.copyCardIdButton.addEventListener("click", () => {
        if (!state.activeDetail) {
            return;
        }

        void copyText(state.activeDetail.cardId, "CardID 已复制");
    });

    elements.copyDbfIdButton.addEventListener("click", () => {
        if (!state.activeDetail) {
            return;
        }

        void copyText(String(state.activeDetail.dbfId), "DbfId 已复制");
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

    populateSelect(elements.setSelect, sets, "全部扩展包", preferredValue ?? elements.setSelect.value);
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
        appendIfPresent(params, "set", elements.setSelect.value);
        appendIfPresent(params, "rarity", elements.raritySelect.value);
        appendIfPresent(params, "cardType", elements.cardTypeSelect.value);
        appendIfPresent(params, "race", elements.raceSelect.value);
        appendIfPresent(params, "school", elements.schoolSelect.value);
        appendIfPresent(params, "collectible", elements.collectibleSelect.value);
        appendIfPresent(params, "keyword", elements.keywordSelect.value);
        params.set("limit", String(state.bootstrap.maxDisplay));

        const response = await fetchJson(`/api/cards?${params.toString()}`);

        elements.searchModeBadge.textContent = response.searchMode;
        elements.statusText.textContent = `卡牌库共 ${formatNumber(response.totalCards)} 张，当前显示 ${formatNumber(response.displayedCount)} 张。`;

        if (response.displayedCount === 0) {
            elements.hintText.textContent = "没有找到符合条件的卡牌。你可以试试中文名、英文名、CardID，或者使用 `标签:值` 和 `EnumID:值` 搜索。";
        } else if (response.displayedCount >= response.maxDisplay) {
            elements.hintText.textContent = `结果较多，当前仅显示前 ${formatNumber(response.maxDisplay)} 张。你可以继续加筛选条件缩小范围。`;
        } else {
            elements.hintText.textContent = "点击任意卡牌即可打开详情弹层，查看关联卡牌、附魔卡牌和完整标签。";
        }

        renderResults(response.items);
    } catch (error) {
        elements.statusText.textContent = "搜索失败";
        elements.hintText.textContent = normalizeErrorMessage(error);
        renderEmptyState("搜索失败", normalizeErrorMessage(error));
    } finally {
        setSearchBusy(false);
    }
}

function renderResults(items) {
    elements.results.replaceChildren();

    if (!items || items.length === 0) {
        renderEmptyState("没有符合条件的卡牌", "可以试试放宽筛选条件，或者直接输入卡牌中文名、英文名、CardID。");
        return;
    }

    for (const item of items) {
        const tile = document.createElement("article");
        tile.className = "card-tile";

        const button = document.createElement("button");
        button.className = "card-button";
        button.type = "button";
        button.addEventListener("click", () => {
            void openDetail(item.cardId);
        });

        const visual = document.createElement("div");
        visual.className = "card-visual";
        if (item.hasImage && item.imageUrl) {
            visual.append(createImageElement(item.imageUrl, item.nameZh, item.nameZh));
        } else {
            visual.append(createPlaceholder(item.nameZh, item.textZh));
        }

        const body = document.createElement("div");
        body.className = "card-body";

        const subtitle = document.createElement("div");
        subtitle.className = "card-subtitle";
        subtitle.textContent = item.subtitle || `CardID: ${item.cardId}`;

        const title = document.createElement("h3");
        title.className = "card-title";
        title.textContent = item.nameZh || item.nameEn || item.cardId;

        const description = document.createElement("div");
        description.className = "card-text";
        description.textContent = truncateText(item.textZh || item.nameEn || item.cardId, 110);

        body.append(subtitle, title, description);
        button.append(visual, body);
        tile.append(button);
        elements.results.append(tile);
    }
}

async function openDetail(cardId) {
    elements.detailModal.classList.remove("is-hidden");
    elements.detailBadge.textContent = "正在加载详情";
    elements.detailName.textContent = "读取中…";
    elements.detailDescription.textContent = "请稍候，正在从服务器读取这张卡牌的详细信息。";
    elements.detailCardId.textContent = cardId;
    elements.detailDbfId.textContent = "-";
    elements.detailVisual.replaceChildren(createPlaceholder("加载中", "正在读取卡牌图片和关联信息。"));

    try {
        const detail = await fetchJson(`/api/cards/${encodeURIComponent(cardId)}`);
        state.activeDetail = detail;
        renderDetail(detail);
    } catch (error) {
        state.activeDetail = null;
        elements.detailBadge.textContent = "加载失败";
        elements.detailName.textContent = "无法打开详情";
        elements.detailDescription.textContent = normalizeErrorMessage(error);
        elements.detailVisual.replaceChildren(createPlaceholder("加载失败", normalizeErrorMessage(error)));
        elements.parentSection.classList.add("is-hidden");
        elements.relatedSection.classList.add("is-hidden");
        elements.enchantmentSection.classList.add("is-hidden");
        elements.tagTableBody.replaceChildren();
    }
}

function renderDetail(detail) {
    elements.detailBadge.textContent = detail.isEnchantment ? "附魔卡牌" : "卡牌详情";
    elements.detailName.textContent = detail.name;
    elements.detailDescription.textContent = detail.text || "（无描述）";
    elements.detailCardId.textContent = detail.cardId;
    elements.detailDbfId.textContent = String(detail.dbfId);

    elements.detailVisual.replaceChildren(
        detail.hasImage && detail.imageUrl
            ? createImageElement(detail.imageUrl, detail.name, detail.name)
            : createPlaceholder(detail.name, detail.text || "暂无图片索引")
    );

    renderLinkSection(elements.parentSection, elements.parentLinks, detail.parentCards);
    renderLinkSection(elements.relatedSection, elements.relatedLinks, detail.relatedCards);
    renderLinkSection(elements.enchantmentSection, elements.enchantmentLinks, detail.enchantmentCards);
    renderTags(detail.tags);
}

function renderLinkSection(section, container, items) {
    container.replaceChildren();

    if (!items || items.length === 0) {
        section.classList.add("is-hidden");
        return;
    }

    section.classList.remove("is-hidden");

    for (const item of items) {
        const button = document.createElement("button");
        button.className = "related-link-button";
        button.type = "button";
        button.textContent = `${item.name} · ${item.reason}`;
        button.title = `CardID: ${item.cardId}\nDbfId: ${item.dbfId}`;
        button.addEventListener("click", () => {
            void openDetail(item.cardId);
        });
        container.append(button);
    }
}

function renderTags(tags) {
    elements.tagTableBody.replaceChildren();

    if (!tags || tags.length === 0) {
        const row = document.createElement("tr");
        row.append(
            createTextCell("暂无标签"),
            createTextCell("-"),
            createTextCell("-"),
        );
        elements.tagTableBody.append(row);
        return;
    }

    for (const tag of tags) {
        const row = document.createElement("tr");
        row.append(createTextCell(tag.displayName), createTextCell(tag.value));

        const actionCell = document.createElement("td");
        if (tag.targetCardId) {
            const button = document.createElement("button");
            button.className = "tag-link-button";
            button.type = "button";
            button.textContent = `查看 ${tag.targetCardId}`;
            button.addEventListener("click", () => {
                void openDetail(tag.targetCardId);
            });
            actionCell.append(button);
        } else {
            actionCell.textContent = "-";
        }

        row.append(actionCell);
        elements.tagTableBody.append(row);
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
    block.className = "card-placeholder";
    block.textContent = `${title || "暂无图片"}\n\n${truncateText(description || "暂无描述", 160)}`;
    return block;
}

function createTextCell(value) {
    const cell = document.createElement("td");
    cell.textContent = value;
    return cell;
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

    if (hintText) {
        elements.hintText.textContent = hintText;
    }
}

function closeModal() {
    elements.detailModal.classList.add("is-hidden");
}

async function copyText(value, successMessage) {
    try {
        await navigator.clipboard.writeText(value);
        elements.hintText.textContent = successMessage;
    } catch {
        elements.hintText.textContent = `复制失败，请手动复制：${value}`;
    }
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
    elements.hintText.textContent = message;
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
