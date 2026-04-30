
using BepInEx;
using BepInEx.Configuration;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace HsCardImageExporter;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class Plugin : BaseUnityPlugin
{
    private const string PluginGuid = "hs.card.image.exporter";
    private const string PluginName = "HsCardImageExporter";
    private const string PluginVersion = "0.1.0";
    private const int ExportLayer = 31;
    private const int MinCaptureWarmupFrames = 2;
    private const int MaxCaptureWarmupFrames = 8;
    private const float UnifiedCardSlotOrthographicSizeMultiplier = 1.08f;
    private const float UnifiedCardSlotDistanceMultiplier = 1.05f;

    private ConfigEntry<bool> _enableExport = null!;
    private ConfigEntry<string> _outputDir = null!;
    private ConfigEntry<int> _maxCount = null!;
    private ConfigEntry<int> _renderWidth = null!;
    private ConfigEntry<int> _renderHeight = null!;
    private ConfigEntry<int> _thumbWidth = null!;
    private ConfigEntry<int> _thumbHeight = null!;
    private ConfigEntry<bool> _exportDetail = null!;
    private ConfigEntry<int> _detailWidth = null!;
    private ConfigEntry<int> _detailHeight = null!;
    private ConfigEntry<bool> _skipExisting = null!;
    private ConfigEntry<bool> _autoDisableWhenDone = null!;
    private ConfigEntry<float> _boundsPaddingRatio = null!;
    private ConfigEntry<int> _trimPadding = null!;
    private ConfigEntry<bool> _previewMode = null!;
    private ConfigEntry<int> _previewPerStrategy = null!;
    private ConfigEntry<string> _cardSetFilter = null!;
    private ConfigEntry<string> _cardIdFilter = null!;
    private ConfigEntry<string> _excludedCardSets = null!;
    private ConfigEntry<string> _excludedCardTypes = null!;
    private ConfigEntry<string> _excludedSpellSchools = null!;
    private ConfigEntry<string> _cardClassContext = null!;

    private Camera _exportCamera = null!;
    private RenderTexture _renderTexture = null!;
    private Transform _exportRoot = null!;

    private void Awake()
    {
        _enableExport = Config.Bind("General", "EnableExport", true, "是否在启动后执行卡图导出。");
        _outputDir = Config.Bind("General", "OutputDir", Path.Combine(BepInEx.Paths.BepInExRootPath, "HsCardExport", "cards"), "完整卡牌 PNG 输出目录。");
        _maxCount = Config.Bind("General", "MaxCount", 0, "本次最多导出的卡牌数量。0 表示全部。");
        _renderWidth = Config.Bind("General", "RenderWidth", 1536, "内部渲染宽度。");
        _renderHeight = Config.Bind("General", "RenderHeight", 2304, "内部渲染高度。");
        _thumbWidth = Config.Bind("General", "ThumbWidth", 512, "列表图宽度。");
        _thumbHeight = Config.Bind("General", "ThumbHeight", 768, "列表图高度。");
        _exportDetail = Config.Bind("General", "ExportDetail", true, "是否导出 detail 详情图。");
        _detailWidth = Config.Bind("General", "DetailWidth", 1024, "详情图宽度。");
        _detailHeight = Config.Bind("General", "DetailHeight", 1536, "详情图高度。");
        _skipExisting = Config.Bind("General", "SkipExisting", true, "是否跳过已存在的图片文件。");
        _autoDisableWhenDone = Config.Bind("General", "AutoDisableWhenDone", true, "导出完成后是否自动关闭导出开关。");
        _boundsPaddingRatio = Config.Bind("General", "BoundsPaddingRatio", 0.08f, "相机取景边界额外保留的比例留白。");
        _trimPadding = Config.Bind("General", "TrimPadding", 6, "自动裁透明边后保留的安全边距像素。");
        _previewMode = Config.Bind("General", "PreviewMode", true, "是否先按渲染类别导出少量样本图。");
        _previewPerStrategy = Config.Bind("General", "PreviewPerStrategy", 4, "预览模式下每个渲染类别导出的样本数量。");
        _cardSetFilter = Config.Bind("General", "CardSetFilter", "", "按卡包筛选导出范围。支持目录名、枚举名或显示名，多个值可用逗号分隔。");
        _cardIdFilter = Config.Bind("General", "CardIdFilter", "", "按卡牌 ID 筛选导出范围。支持完整 ID 或片段，多个值可用逗号分隔。");
        _excludedCardSets = Config.Bind("General", "ExcludedCardSets", "", "排除指定的 CardSet。支持目录名、枚举名、显示名或数值，多个值可用逗号分隔。");
        _excludedCardTypes = Config.Bind("General", "ExcludedCardTypes", "", "排除指定的 CardType。支持数值或枚举名，多个值可用逗号分隔。");
        _excludedSpellSchools = Config.Bind("General", "ExcludedSpellSchools", "", "排除指定的 SpellSchool 值。支持数值或枚举名，多个值可用逗号分隔。");
        _cardClassContext = Config.Bind("General", "CardClassContext", "", "按职业补足多职业卡的导出上下文。支持职业枚举名、数值或当前语言显示名。留空时自动尝试读取当前对局/收藏页职业上下文。");
    }

    private void Start()
    {
        Logger.LogInfo($"{PluginName} loaded.");
        if (_enableExport.Value)
            StartCoroutine(WaitAndExport());
    }

    private System.Collections.IEnumerator WaitAndExport()
    {
        while (AssetLoader.Get() == null ||
               GameDbf.Card == null ||
               GameDbf.Card.GetRecords().Count == 0 ||
               DefLoader.Get() == null ||
               !DefLoader.Get().HasLoadedEntityDefs())
            yield return new WaitForSeconds(1f);

        EnsureExportRuntime();
        Directory.CreateDirectory(_outputDir.Value);
        Logger.LogInfo("Card image export prerequisites are ready.");
        yield return ExportCards();

        if (_autoDisableWhenDone.Value)
        {
            _enableExport.Value = false;
            Config.Save();
        }
    }

    private void EnsureExportRuntime()
    {
        if (_exportRoot != null && _exportCamera != null && _renderTexture != null)
            return;

        var rootObject = new GameObject("HsCardImageExporter_Runtime");
        DontDestroyOnLoad(rootObject);
        _exportRoot = rootObject.transform;

        var cameraObject = new GameObject("ExportCamera");
        cameraObject.transform.SetParent(_exportRoot, false);
        _exportCamera = cameraObject.AddComponent<Camera>();
        _exportCamera.enabled = false;
        _exportCamera.clearFlags = CameraClearFlags.SolidColor;
        _exportCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        _exportCamera.orthographic = true;
        _exportCamera.cullingMask = 1 << ExportLayer;
        _exportCamera.allowHDR = false;
        _exportCamera.allowMSAA = false;

        _renderTexture = new RenderTexture(_renderWidth.Value, _renderHeight.Value, 24, RenderTextureFormat.ARGB32);
        _renderTexture.name = "HsCardImageExporter_RT";
        _renderTexture.Create();
        _exportCamera.targetTexture = _renderTexture;
    }

    private IEnumerator ExportCards()
    {
        var cardIds = GetTargetCardIds();
        Logger.LogInfo($"Card export queue size: {cardIds.Count}");

        for (var i = 0; i < cardIds.Count; i++)
        {
            yield return ExportSingleCard(cardIds[i], i + 1, cardIds.Count);
        }

        Logger.LogInfo("Card image export finished.");
    }

    private List<string> GetTargetCardIds()
    {
        var cardSetFilters = SplitFilterTokens(_cardSetFilter.Value);
        var cardIdFilters = SplitFilterTokens(_cardIdFilter.Value);
        var excludedCardSetFilters = SplitFilterTokens(_excludedCardSets.Value);
        var excludedCardTypes = ParseCardTypeValues(_excludedCardTypes.Value);
        var excludedSpellSchools = ParseSpellSchoolValues(_excludedSpellSchools.Value);
        var ids = GameUtils.GetAllCardIds()
            .Where(cardId => !string.IsNullOrWhiteSpace(cardId))
            .Distinct()
            .OrderBy(cardId => cardId, System.StringComparer.Ordinal)
            .ToList();

        if (cardSetFilters.Count > 0 || cardIdFilters.Count > 0 || excludedCardSetFilters.Count > 0 || excludedCardTypes.Count > 0 || excludedSpellSchools.Count > 0)
        {
            ids = ids
                .Where(cardId =>
                {
                    var entityDef = DefLoader.Get().GetEntityDef(cardId);
                    return entityDef != null && IsCardMatchedByFilters(cardId, entityDef, cardSetFilters, cardIdFilters, excludedCardSetFilters, excludedCardTypes, excludedSpellSchools);
                })
                .ToList();

            Logger.LogInfo($"Card export filters applied. CardSetFilter='{_cardSetFilter.Value}', CardIdFilter='{_cardIdFilter.Value}', ExcludedCardSets='{_excludedCardSets.Value}', ExcludedCardTypes='{_excludedCardTypes.Value}', ExcludedSpellSchools='{_excludedSpellSchools.Value}', Matched={ids.Count}");
        }
        else if (ShouldUsePreviewSampling())
        {
            ids = BuildPreviewCardIds(ids);
        }

        if (_maxCount.Value > 0)
            ids = ids.Take(_maxCount.Value).ToList();

        if (_skipExisting.Value)
        {
            ids = ids
                .Where(cardId =>
                {
                    var entityDef = DefLoader.Get().GetEntityDef(cardId);
                    var cardSet = entityDef != null ? entityDef.GetCardSet() : TAG_CARD_SET.INVALID;
                    if (!File.Exists(GetOutputPath(cardId, cardSet, "thumb")))
                        return true;

                    return _exportDetail.Value &&
                           !File.Exists(GetOutputPath(cardId, cardSet, "detail"));
                })
                .ToList();
        }

        return ids;
    }

    private bool ShouldUsePreviewSampling()
    {
        return _previewMode.Value && !HasExplicitFilter();
    }

    private bool HasExplicitFilter()
    {
        return !string.IsNullOrWhiteSpace(_cardSetFilter.Value) ||
               !string.IsNullOrWhiteSpace(_cardIdFilter.Value) ||
               !string.IsNullOrWhiteSpace(_excludedCardSets.Value) ||
               !string.IsNullOrWhiteSpace(_excludedCardTypes.Value) ||
               !string.IsNullOrWhiteSpace(_excludedSpellSchools.Value);
    }

    private static List<string> SplitFilterTokens(string rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
            return new List<string>();

        return rawValue
            .Split(new[] { ',', ';', '|', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(token => token.Trim())
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .Distinct(System.StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsCardMatchedByFilters(
        string cardId,
        EntityDef entityDef,
        IReadOnlyCollection<string> cardSetFilters,
        IReadOnlyCollection<string> cardIdFilters,
        IReadOnlyCollection<string> excludedCardSetFilters,
        ISet<int> excludedCardTypes,
        ISet<int> excludedSpellSchools)
    {
        if (cardIdFilters.Count > 0 && !cardIdFilters.Any(filter => ContainsIgnoreCase(cardId, filter)))
            return false;

        if (excludedCardSetFilters.Count > 0 &&
            IsCardSetMatched(entityDef.GetCardSet(), excludedCardSetFilters))
            return false;

        if (excludedCardTypes.Count > 0 &&
            excludedCardTypes.Contains(entityDef.GetTag(GAME_TAG.CARDTYPE)))
            return false;

        if (excludedSpellSchools.Count > 0 &&
            excludedSpellSchools.Contains(entityDef.GetTag(GAME_TAG.SPELL_SCHOOL)))
            return false;

        if (cardSetFilters.Count == 0)
            return true;

        return IsCardSetMatched(entityDef.GetCardSet(), cardSetFilters);
    }

    private HashSet<int> ParseSpellSchoolValues(string rawValue)
    {
        var result = new HashSet<int>();

        foreach (var token in SplitFilterTokens(rawValue))
        {
            if (TryParseSpellSchoolValue(token, out var value))
            {
                result.Add(value);
                continue;
            }

            Logger.LogWarning($"Ignore invalid spell school filter token: {token}");
        }

        return result;
    }

    private HashSet<int> ParseCardTypeValues(string rawValue)
    {
        var result = new HashSet<int>();

        foreach (var token in SplitFilterTokens(rawValue))
        {
            if (TryParseCardTypeValue(token, out var value))
            {
                result.Add(value);
                continue;
            }

            Logger.LogWarning($"Ignore invalid card type filter token: {token}");
        }

        return result;
    }

    private static bool TryParseSpellSchoolValue(string rawValue, out int value)
    {
        if (int.TryParse(rawValue, out value))
            return true;

        if (Enum.TryParse(rawValue, true, out TAG_SPELL_SCHOOL spellSchool))
        {
            value = (int)spellSchool;
            return true;
        }

        value = 0;
        return false;
    }

    private static bool TryParseCardTypeValue(string rawValue, out int value)
    {
        if (int.TryParse(rawValue, out value))
            return true;

        if (Enum.TryParse(rawValue, true, out TAG_CARDTYPE cardType))
        {
            value = (int)cardType;
            return true;
        }

        value = 0;
        return false;
    }

    private static bool IsCardSetMatched(TAG_CARD_SET cardSet, IReadOnlyCollection<string> cardSetFilters)
    {
        var displayName = cardSet == TAG_CARD_SET.INVALID ? "UNKNOWN" : GameStrings.GetCardSetName(cardSet);
        var enumName = cardSet.ToString();
        var cardSetDir = GetCardSetDirectoryName(cardSet);
        var cardSetValue = ((int)cardSet).ToString();

        return cardSetFilters.Any(filter =>
            ContainsIgnoreCase(cardSetDir, filter) ||
            ContainsIgnoreCase(enumName, filter) ||
            ContainsIgnoreCase(displayName, filter) ||
            ContainsIgnoreCase(cardSetValue, filter));
    }

    private static bool ContainsIgnoreCase(string source, string value)
    {
        return !string.IsNullOrWhiteSpace(source) &&
               !string.IsNullOrWhiteSpace(value) &&
               source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private List<string> BuildPreviewCardIds(List<string> allIds)
    {
        var result = new List<string>();
        var counts = new Dictionary<ExportRenderStrategyKind, int>();

        foreach (var cardId in allIds)
        {
            var entityDef = DefLoader.Get().GetEntityDef(cardId);
            if (entityDef == null)
                continue;

            var kind = CreateRenderStrategy(entityDef).Kind;
            var current = counts.TryGetValue(kind, out var value) ? value : 0;
            if (current >= _previewPerStrategy.Value)
                continue;

            counts[kind] = current + 1;
            result.Add(cardId);
        }

        Logger.LogInfo("Preview mode category counts: " + string.Join(", ", counts.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}={pair.Value}")));
        return result;
    }

    private IEnumerator ExportSingleCard(string cardId, int index, int total)
    {
        Logger.LogInfo($"[{index}/{total}] Exporting {cardId}");
        GameObject actorObject = null;
        GameObject renderRootObject = null;
        DefLoader.DisposableFullDef fullDef = null;

        try
        {
            fullDef = DefLoader.Get().GetFullDef(cardId, CardPortraitQuality.GetDefault());
            if (fullDef?.EntityDef == null)
            {
                Logger.LogWarning($"Skip {cardId}: full def unavailable.");
                yield break;
            }

            var preparedEntityDef = PrepareEntityDefForExport(fullDef.EntityDef);
            var exportEntityDef = preparedEntityDef.EntityDef;
            var strategy = CreateRenderStrategy(exportEntityDef);
            var shouldUseUnifiedCardSlotFraming = ShouldUseUnifiedCardSlotFraming(strategy);
            actorObject = AssetLoader.Get().InstantiatePrefab(strategy.ActorPath, AssetLoadingOptions.IgnorePrefabPosition);
            if (actorObject == null)
            {
                Logger.LogWarning($"Skip {cardId}: actor prefab unavailable.");
                yield break;
            }

            actorObject.transform.SetParent(_exportRoot, false);
            SetLayerRecursively(actorObject.transform, ExportLayer);
            actorObject.transform.localPosition = Vector3.zero;

            var actor = actorObject.GetComponent<Actor>();
            if (actor == null)
            {
                Logger.LogWarning($"Skip {cardId}: actor component missing.");
                yield break;
            }

            InitializeActorForExport(actorObject, actor, exportEntityDef, strategy, shouldUseUnifiedCardSlotFraming);

            renderRootObject = WrapActorWithCollectionVisualIfAvailable(actor, exportEntityDef);
            if (renderRootObject == null && shouldUseUnifiedCardSlotFraming)
                FinalizeActorPresentationForExport(actorObject, actor, strategy);

            yield return WaitForActorReadyForCapture(actorObject, actor, preparedEntityDef.ForceMulticlassRibbon);

            var boundsTarget = renderRootObject ?? actorObject;
            SetLayerRecursively(boundsTarget.transform, ExportLayer);
            var useUnifiedCardSlotFraming = renderRootObject != null && shouldUseUnifiedCardSlotFraming;

            Bounds bounds;
            if (useUnifiedCardSlotFraming)
            {
                if (!TryGetUnifiedSlotFramingBounds(renderRootObject, actor, out bounds))
                {
                    Logger.LogWarning($"Skip {cardId}: unified card slot bounds unavailable.");
                    yield break;
                }
            }
            else if (!TryGetRenderBounds(boundsTarget, actor, out bounds))
            {
                Logger.LogWarning($"Skip {cardId}: no renderers found.");
                yield break;
            }

            bounds = ExpandBounds(bounds, _boundsPaddingRatio.Value);

            if (!useUnifiedCardSlotFraming)
            {
                var alignmentTransform = renderRootObject != null ? renderRootObject.transform : actorObject.transform;
                var offset = -bounds.center;
                alignmentTransform.position += offset;
                bounds.center += offset;
            }

            var cameraFrameTransform = useUnifiedCardSlotFraming ? renderRootObject.transform : actorObject.transform;
            ConfigureCamera(bounds, cameraFrameTransform, strategy, useUnifiedCardSlotFraming);
            SaveCardPng(cardId, fullDef.EntityDef.GetCardSet(), strategy);

            if (index % 50 == 0)
                yield return Resources.UnloadUnusedAssets();
        }
        finally
        {
            if (renderRootObject != null)
            {
                Destroy(renderRootObject);
            }
            else if (actorObject != null)
            {
                Destroy(actorObject);
            }

            fullDef?.Dispose();
        }
    }

    private static void InitializeActorForExport(
        GameObject actorObject,
        Actor actor,
        EntityDef entityDef,
        ExportRenderStrategy strategy,
        bool deferActorShowUntilCardSlot)
    {
        actor.SetPremium(TAG_PREMIUM.NORMAL);
        actor.SetEntityDef(entityDef);

        if (strategy.CreateBannedRibbon)
            actor.CreateBannedRibbon();

        strategy.CustomInitialize?.Invoke(actorObject, actor, entityDef);

        var rootObject = actor.GetRootObject();
        if (strategy.ActivateRootObjectBeforeShow && rootObject != null && !rootObject.activeSelf)
            rootObject.SetActive(true);

        if (deferActorShowUntilCardSlot)
        {
            actor.UpdateAllComponents(strategy.UpdateAllComponentsIgnoreSpells);
        }
        else
        {
            FinalizeActorPresentationForExport(actorObject, actor, strategy);
        }

        SetLayerRecursively(actorObject.transform, ExportLayer);
    }

    private static void FinalizeActorPresentationForExport(GameObject actorObject, Actor actor, ExportRenderStrategy strategy)
    {
        if (strategy.UpdateComponentsAfterShow)
        {
            actor.Show();
            actor.UpdateAllComponents(strategy.UpdateAllComponentsIgnoreSpells);
        }
        else
        {
            actor.UpdateAllComponents(strategy.UpdateAllComponentsIgnoreSpells);
            actor.Show();
        }

        SetLayerRecursively(actorObject.transform, ExportLayer);
    }

    private static void ApplyPostInitializeOverrides(Actor actor, bool forceMulticlassRibbon)
    {
        if (forceMulticlassRibbon && actor.m_multiclassRibbon != null)
            actor.m_multiclassRibbon.SetActive(true);
    }

    private static IEnumerator WaitForActorReadyForCapture(GameObject actorObject, Actor actor, bool forceMulticlassRibbon)
    {
        var previousChildCount = -1;
        var previousRendererCount = -1;
        var consecutiveStableFrames = 0;

        for (var frame = 0; frame < MaxCaptureWarmupFrames; frame++)
        {
            yield return new WaitForEndOfFrame();

            if (actorObject == null)
                yield break;

            SetLayerRecursively(actorObject.transform, ExportLayer);
            ApplyPostInitializeOverrides(actor, forceMulticlassRibbon);

            var childCount = actorObject.GetComponentsInChildren<Transform>(true).Length;
            var rendererCount = actorObject.GetComponentsInChildren<Renderer>(true).Length;
            var isWaitingOnAssets = IsActorWaitingOnAssets(actorObject);
            var hasPendingDecor = HasPendingExportDecor(actor);

            if (!isWaitingOnAssets &&
                !hasPendingDecor &&
                childCount == previousChildCount &&
                rendererCount == previousRendererCount)
            {
                consecutiveStableFrames++;
            }
            else
            {
                consecutiveStableFrames = 0;
            }

            previousChildCount = childCount;
            previousRendererCount = rendererCount;

            if (frame + 1 >= MinCaptureWarmupFrames && consecutiveStableFrames >= 1)
                yield break;
        }
    }

    private static bool IsActorWaitingOnAssets(GameObject actorObject)
    {
        var assetLoader = AssetLoader.Get();
        if (assetLoader == null)
            return false;

        foreach (var transform in actorObject.GetComponentsInChildren<Transform>(true))
        {
            if (assetLoader.IsWaitingOnObject(transform.gameObject))
                return true;
        }

        return false;
    }

    private static bool HasPendingExportDecor(Actor actor)
    {
        if (actor == null)
            return false;

        return IsPendingNestedPrefab(actor.m_hearthstoneFactionBannerContainer) ||
               IsPendingNestedPrefab(actor.m_tradeableBannerContainer) ||
               IsPendingNestedPrefab(actor.m_forgeBannerContainer) ||
               IsPendingNestedPrefab(actor.m_bannedRibbonContainer);
    }

    private static bool IsPendingNestedPrefab(NestedPrefab nestedPrefab)
    {
        return nestedPrefab != null &&
               nestedPrefab.gameObject.activeSelf &&
               !nestedPrefab.PrefabIsLoaded();
    }

    private static void SetLayerRecursively(Transform root, int layer)
    {
        root.gameObject.layer = layer;
        foreach (Transform child in root)
            SetLayerRecursively(child, layer);
    }

    private static bool TryGetRenderBounds(GameObject actorObject, Actor actor, out Bounds bounds)
    {
        if (TryGetStableSlotBounds(actorObject, out bounds))
            return true;

        if (TryGetCuratedActorBounds(actor, out bounds))
            return true;

        return TryGetFallbackRendererBounds(actorObject, out bounds);
    }

    private static bool TryGetUnifiedSlotFramingBounds(GameObject slotObject, Actor actor, out Bounds bounds)
    {
        if (!TryGetStableSlotBounds(slotObject, out var slotBounds))
        {
            bounds = default;
            return false;
        }

        if (!TryGetUnifiedCardFrameBounds(actor, out var actorBounds) &&
            !TryGetCuratedActorBounds(actor, out actorBounds))
        {
            bounds = slotBounds;
            return true;
        }

        var center = slotBounds.center;
        var min = actorBounds.min;
        var max = actorBounds.max;
        var extents = slotBounds.extents;
        extents.x = Mathf.Max(extents.x, Mathf.Abs(min.x - center.x), Mathf.Abs(max.x - center.x));
        extents.y = Mathf.Max(extents.y, Mathf.Abs(min.y - center.y), Mathf.Abs(max.y - center.y));
        extents.z = Mathf.Max(extents.z, Mathf.Abs(min.z - center.z), Mathf.Abs(max.z - center.z));
        bounds = new Bounds(center, extents * 2f);
        return true;
    }

    private static bool TryGetUnifiedCardFrameBounds(Actor actor, out Bounds bounds)
    {
        if (actor == null)
        {
            bounds = default;
            return false;
        }

        var frameRenderer = actor.m_cardMesh != null
            ? actor.m_cardMesh.GetComponent<Renderer>()
            : actor.GetMeshRenderer(false);
        if (frameRenderer == null)
        {
            bounds = default;
            return false;
        }

        bounds = frameRenderer.bounds;

        EncapsulateRendererBounds(actor.m_manaObject, ref bounds);
        EncapsulateRendererBounds(actor.m_rarityGemMesh, ref bounds);
        EncapsulateRendererBounds(actor.m_rarityNoGemMesh, ref bounds);
        EncapsulateRendererBounds(actor.m_attackObject, ref bounds);
        EncapsulateRendererBounds(actor.m_healthObject, ref bounds);
        EncapsulateRendererBounds(actor.m_armorObject, ref bounds);
        return true;
    }

    private static void EncapsulateRendererBounds(GameObject targetObject, ref Bounds bounds)
    {
        if (targetObject == null)
            return;

        var renderers = targetObject.GetComponentsInChildren<Renderer>(true);
        for (var i = 0; i < renderers.Length; i++)
        {
            var renderer = renderers[i];
            if (renderer == null ||
                !renderer.enabled ||
                !renderer.gameObject.activeInHierarchy)
            {
                continue;
            }

            bounds.Encapsulate(renderer.bounds);
        }
    }

    private static bool TryGetStableSlotBounds(GameObject actorObject, out Bounds bounds)
    {
        if (actorObject != null)
        {
            var boxCollider = actorObject.GetComponent<BoxCollider>();
            if (boxCollider != null)
            {
                bounds = boxCollider.bounds;
                return true;
            }
        }

        bounds = default;
        return false;
    }

    private static bool TryGetCuratedActorBounds(Actor actor, out Bounds bounds)
    {
        if (actor == null)
        {
            bounds = default;
            return false;
        }

        var primaryRenderer = actor.GetMeshRenderer(false);
        if (primaryRenderer == null)
        {
            bounds = default;
            return false;
        }

        bounds = primaryRenderer.bounds;

        if (actor.m_meshesThatAffectBoundsCalculations != null)
        {
            foreach (var meshRenderer in actor.m_meshesThatAffectBoundsCalculations)
            {
                if (meshRenderer != null)
                    bounds.Encapsulate(meshRenderer.bounds);
            }
        }

        if (RequiresManaGemBounds(actor) &&
            TryFindRendererIgnoreCase(actor.GetRootObject() ?? actor.gameObject, "gem_mana", out var gemRenderer))
        {
            bounds.Encapsulate(gemRenderer.bounds);
        }

        return true;
    }

    private static bool TryGetFallbackRendererBounds(GameObject actorObject, out Bounds bounds)
    {
        var renderers = actorObject.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            bounds = default;
            return false;
        }

        bounds = renderers[0].bounds;
        for (var i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        return true;
    }

    private static bool RequiresManaGemBounds(Actor actor)
    {
        var entity = actor.GetEntity();
        if (entity != null)
            return entity.IsSideQuest() || entity.IsSigil() || entity.IsObjective();

        var entityDef = actor.GetEntityDef();
        return entityDef != null &&
               (entityDef.IsSideQuest() || entityDef.IsSigil() || entityDef.IsObjective());
    }

    private static bool TryFindRendererIgnoreCase(GameObject rootObject, string objectName, out MeshRenderer renderer)
    {
        if (rootObject != null)
        {
            foreach (var meshRenderer in rootObject.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (meshRenderer.gameObject.name.Equals(objectName, StringComparison.InvariantCultureIgnoreCase))
                {
                    renderer = meshRenderer;
                    return true;
                }
            }
        }

        renderer = null;
        return false;
    }

    private static Bounds ExpandBounds(Bounds bounds, float paddingRatio)
    {
        if (paddingRatio <= 0f)
            return bounds;

        var scale = 1f + paddingRatio * 2f;
        bounds.extents = bounds.extents * scale;
        return bounds;
    }

    private GameObject WrapActorWithCollectionVisualIfAvailable(Actor actor, EntityDef entityDef)
    {
        if (actor == null)
            return null;

        var collectionManager = CollectionManager.Get();
        var collectibleDisplay = collectionManager != null ? collectionManager.GetCollectibleDisplay() : null;
        var cardVisualPrefab = collectibleDisplay != null ? collectibleDisplay.GetCardVisualPrefab() : null;
        if (cardVisualPrefab == null)
            return null;

        var hostObject = Instantiate(cardVisualPrefab.gameObject);
        if (hostObject == null)
            return null;

        hostObject.transform.SetParent(_exportRoot, false);
        hostObject.transform.localPosition = Vector3.zero;
        hostObject.transform.localRotation = Quaternion.identity;
        hostObject.transform.localScale = Vector3.one;

        var cardVisual = hostObject.GetComponent<CollectionCardVisual>();
        if (cardVisual == null)
        {
            Destroy(hostObject);
            return null;
        }

        cardVisual.SetActors(new CollectionCardActors(actor), CollectionUtils.ViewMode.CARDS);
        if (entityDef != null && entityDef.IsPet())
            cardVisual.SetPetBoxCollider();
        else if (entityDef != null && entityDef.IsHeroSkin())
            cardVisual.SetHeroSkinBoxCollider();
        else
            cardVisual.SetDefaultBoxCollider();

        cardVisual.UpdateSpecialCaseTransform();
        cardVisual.Show();
        return hostObject;
    }

    private static bool ShouldUseUnifiedCardSlotFraming(ExportRenderStrategy strategy)
    {
        return strategy.Kind != ExportRenderStrategyKind.HeroSkin &&
               strategy.Kind != ExportRenderStrategyKind.Pet;
    }

    private void ConfigureCamera(Bounds bounds, Transform frameTransform, ExportRenderStrategy strategy, bool useUnifiedCardSlotFraming)
    {
        var aspect = (float)_renderWidth.Value / _renderHeight.Value;
        var faceNormal = frameTransform.up.sqrMagnitude > 0.001f ? frameTransform.up.normalized : Vector3.up;
        var imageUp = frameTransform.forward.sqrMagnitude > 0.001f ? frameTransform.forward.normalized : Vector3.forward;
        var orthographicSizeMultiplier = useUnifiedCardSlotFraming
            ? UnifiedCardSlotOrthographicSizeMultiplier
            : strategy.OrthographicSizeMultiplier;
        var distanceMultiplier = useUnifiedCardSlotFraming
            ? UnifiedCardSlotDistanceMultiplier
            : strategy.DistanceMultiplier;
        var distance = Mathf.Max(4f, bounds.extents.z + 5f) * distanceMultiplier;

        _exportCamera.orthographicSize = Mathf.Max(bounds.extents.y, bounds.extents.x / aspect) * orthographicSizeMultiplier;
        _exportCamera.nearClipPlane = 0.01f;
        _exportCamera.farClipPlane = distance * 3f;
        _exportCamera.transform.position = bounds.center + faceNormal * distance + strategy.CameraOffset;
        _exportCamera.transform.rotation = Quaternion.LookRotation(-faceNormal, imageUp);
    }

    private void SaveCardPng(string cardId, TAG_CARD_SET cardSet, ExportRenderStrategy strategy)
    {
        var previous = RenderTexture.active;
        var texture = strategy.UseDualBackgroundAlphaCapture
            ? CaptureTextureWithAccurateAlpha()
            : CaptureTextureWithCameraAlpha();
        Texture2D thumbTexture = null;
        Texture2D detailTexture = null;

        try
        {
            thumbTexture = ResizeTextureToCanvas(texture, _thumbWidth.Value, _thumbHeight.Value, _trimPadding.Value);

            var thumbPath = GetOutputPath(cardId, cardSet, "thumb");
            Directory.CreateDirectory(Path.GetDirectoryName(thumbPath)!);
            File.WriteAllBytes(thumbPath, thumbTexture.EncodeToPNG());

            if (_exportDetail.Value)
            {
                detailTexture = ResizeTextureToCanvas(texture, _detailWidth.Value, _detailHeight.Value, _trimPadding.Value);
                var detailPath = GetOutputPath(cardId, cardSet, "detail");
                Directory.CreateDirectory(Path.GetDirectoryName(detailPath)!);
                File.WriteAllBytes(detailPath, detailTexture.EncodeToPNG());
            }
        }
        finally
        {
            RenderTexture.active = previous;
            if (thumbTexture != null && thumbTexture != texture)
                Destroy(thumbTexture);
            if (detailTexture != null && detailTexture != texture)
                Destroy(detailTexture);
            Destroy(texture);
        }
    }

    private Texture2D CaptureTextureWithCameraAlpha()
    {
        var texture = new Texture2D(_renderWidth.Value, _renderHeight.Value, TextureFormat.RGBA32, false);
        CaptureCameraToTexture(new Color(0f, 0f, 0f, 0f), texture);
        return texture;
    }

    private Texture2D CaptureTextureWithAccurateAlpha()
    {
        var blackTexture = new Texture2D(_renderWidth.Value, _renderHeight.Value, TextureFormat.RGBA32, false);
        var whiteTexture = new Texture2D(_renderWidth.Value, _renderHeight.Value, TextureFormat.RGBA32, false);

        try
        {
            CaptureCameraToTexture(new Color(0f, 0f, 0f, 0f), blackTexture);
            CaptureCameraToTexture(new Color(1f, 1f, 1f, 1f), whiteTexture);
            return RebuildTextureAlpha(blackTexture, whiteTexture);
        }
        finally
        {
            Destroy(blackTexture);
            Destroy(whiteTexture);
        }
    }

    private void CaptureCameraToTexture(Color backgroundColor, Texture2D targetTexture)
    {
        var previous = RenderTexture.active;
        var previousBackgroundColor = _exportCamera.backgroundColor;

        try
        {
            _exportCamera.backgroundColor = backgroundColor;
            _exportCamera.Render();
            RenderTexture.active = _renderTexture;
            targetTexture.ReadPixels(new Rect(0f, 0f, _renderWidth.Value, _renderHeight.Value), 0, 0, false);
            targetTexture.Apply(false, false);
        }
        finally
        {
            _exportCamera.backgroundColor = previousBackgroundColor;
            RenderTexture.active = previous;
        }
    }

    private static Texture2D RebuildTextureAlpha(Texture2D blackTexture, Texture2D whiteTexture)
    {
        var blackPixels = blackTexture.GetPixels32();
        var whitePixels = whiteTexture.GetPixels32();
        var mergedPixels = new Color32[blackPixels.Length];

        for (var i = 0; i < mergedPixels.Length; i++)
        {
            var blackPixel = blackPixels[i];
            var whitePixel = whitePixels[i];

            var blackR = blackPixel.r / 255f;
            var blackG = blackPixel.g / 255f;
            var blackB = blackPixel.b / 255f;
            var whiteR = whitePixel.r / 255f;
            var whiteG = whitePixel.g / 255f;
            var whiteB = whitePixel.b / 255f;

            var alphaR = Mathf.Clamp01(1f - (whiteR - blackR));
            var alphaG = Mathf.Clamp01(1f - (whiteG - blackG));
            var alphaB = Mathf.Clamp01(1f - (whiteB - blackB));
            var alpha = Mathf.Clamp01((alphaR + alphaG + alphaB) / 3f);

            if (alpha <= 1f / 255f)
            {
                mergedPixels[i] = new Color32(0, 0, 0, 0);
                continue;
            }

            var inverseAlpha = 1f / alpha;
            mergedPixels[i] = new Color32(
                (byte)Mathf.Clamp(Mathf.RoundToInt(blackR * inverseAlpha * 255f), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(blackG * inverseAlpha * 255f), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(blackB * inverseAlpha * 255f), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(alpha * 255f), 0, 255));
        }

        var mergedTexture = new Texture2D(blackTexture.width, blackTexture.height, TextureFormat.RGBA32, false);
        mergedTexture.SetPixels32(mergedPixels);
        mergedTexture.Apply(false, false);
        return mergedTexture;
    }

    private static Texture2D ResizeTextureToCanvas(Texture2D source, int canvasWidth, int canvasHeight, int padding)
    {
        var width = source.width;
        var height = source.height;
        var canvas = new Texture2D(canvasWidth, canvasHeight, TextureFormat.RGBA32, false);
        var clearPixels = Enumerable.Repeat(new Color(0f, 0f, 0f, 0f), canvasWidth * canvasHeight).ToArray();
        canvas.SetPixels(clearPixels);
        var targetWidth = Mathf.Max(1, canvasWidth - padding * 2);
        var targetHeight = Mathf.Max(1, canvasHeight - padding * 2);
        var scale = Mathf.Min((float)targetWidth / width, (float)targetHeight / height);
        var scaledWidth = Mathf.Max(1, Mathf.RoundToInt(width * scale));
        var scaledHeight = Mathf.Max(1, Mathf.RoundToInt(height * scale));
        var offsetX = Mathf.Max(0, (canvasWidth - scaledWidth) / 2);
        var offsetY = Mathf.Max(0, (canvasHeight - scaledHeight) / 2);
        var scaledPixels = new Color[scaledWidth * scaledHeight];

        for (var y = 0; y < scaledHeight; y++)
        {
            for (var x = 0; x < scaledWidth; x++)
            {
                var u = scaledWidth <= 1 ? 0f : (float)x / (scaledWidth - 1);
                var v = scaledHeight <= 1 ? 0f : (float)y / (scaledHeight - 1);
                scaledPixels[y * scaledWidth + x] = source.GetPixelBilinear(
                    u,
                    v);
            }
        }

        canvas.SetPixels(offsetX, offsetY, scaledWidth, scaledHeight, scaledPixels);
        canvas.Apply(false, false);
        return canvas;
    }

    private string GetOutputPath(string cardId, TAG_CARD_SET cardSet, string variant)
    {
        var cardSetDir = GetCardSetDirectoryName(cardSet);
        var root = ShouldUsePreviewSampling()
            ? Path.Combine(_outputDir.Value, "preview")
            : _outputDir.Value;
        return Path.Combine(root, variant, cardSetDir, $"{cardId}.png");
    }

    private static string GetCardSetDirectoryName(TAG_CARD_SET cardSet)
    {
        var displayName = cardSet == TAG_CARD_SET.INVALID ? "UNKNOWN" : GameStrings.GetCardSetName(cardSet);
        var safeDisplayName = SanitizePathSegment(displayName);
        var enumName = cardSet.ToString();
        return $"{(int)cardSet:D4}_{enumName}_{safeDisplayName}";
    }

    private static string SanitizePathSegment(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "UNKNOWN";

        var invalidChars = Path.GetInvalidFileNameChars();
        var chars = value
            .Select(ch => invalidChars.Contains(ch) ? '_' : ch)
            .ToArray();

        var result = new string(chars).Trim();
        return string.IsNullOrWhiteSpace(result) ? "UNKNOWN" : result;
    }

    private (EntityDef EntityDef, bool ForceMulticlassRibbon) PrepareEntityDefForExport(EntityDef sourceEntityDef)
    {
        if (sourceEntityDef == null)
            return (sourceEntityDef, false);

        var cardClasses = new List<TAG_CLASS>();
        sourceEntityDef.GetClasses(cardClasses);
        if (cardClasses.Count <= 2)
            return (sourceEntityDef, false);

        var resolvedClassContext = ResolveExportClassContext(cardClasses);
        if (resolvedClassContext == TAG_CLASS.INVALID)
            return (sourceEntityDef, false);

        var clonedEntityDef = sourceEntityDef.Clone();
        clonedEntityDef.SetTag(GAME_TAG.CLASS, (int)resolvedClassContext);
        clonedEntityDef.SetTag(GAME_TAG.MULTIPLE_CLASSES, 0);
        return (clonedEntityDef, true);
    }

    private TAG_CLASS ResolveExportClassContext(IReadOnlyCollection<TAG_CLASS> cardClasses)
    {
        var configuredClassContext = ParseCardClassContext(_cardClassContext.Value);
        if (cardClasses.Contains(configuredClassContext))
            return configuredClassContext;

        foreach (var runtimeClassContext in GetRuntimeClassContexts())
        {
            if (cardClasses.Contains(runtimeClassContext))
                return runtimeClassContext;
        }

        return TAG_CLASS.INVALID;
    }

    private static List<TAG_CLASS> GetRuntimeClassContexts()
    {
        var contexts = new List<TAG_CLASS>();
        AddFriendlyHeroClassContext(contexts);

        foreach (var collectionClass in CollectionManager.GetCollectionManagerClasses())
            AddDistinctClassContext(contexts, collectionClass);

        return contexts;
    }

    private static void AddFriendlyHeroClassContext(List<TAG_CLASS> contexts)
    {
        var gameState = GameState.Get();
        var friendlyPlayer = gameState?.GetFriendlySidePlayer();
        var hero = friendlyPlayer?.GetHero();
        if (hero == null)
            return;

        var heroClasses = new List<TAG_CLASS>();
        hero.GetClasses(heroClasses);
        foreach (var heroClass in heroClasses)
            AddDistinctClassContext(contexts, heroClass);
    }

    private static void AddDistinctClassContext(List<TAG_CLASS> contexts, TAG_CLASS tagClass)
    {
        if (tagClass == TAG_CLASS.INVALID || contexts.Contains(tagClass))
            return;

        contexts.Add(tagClass);
    }

    private static TAG_CLASS ParseCardClassContext(string rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
            return TAG_CLASS.INVALID;

        var trimmedValue = rawValue.Trim();
        if (int.TryParse(trimmedValue, out var numericValue) &&
            Enum.IsDefined(typeof(TAG_CLASS), numericValue))
        {
            return (TAG_CLASS)numericValue;
        }

        foreach (TAG_CLASS tagClass in Enum.GetValues(typeof(TAG_CLASS)))
        {
            if (tagClass == TAG_CLASS.INVALID)
                continue;

            if (trimmedValue.Equals(tagClass.ToString(), StringComparison.OrdinalIgnoreCase) ||
                trimmedValue.Equals(GameStrings.GetClassName(tagClass), StringComparison.OrdinalIgnoreCase))
            {
                return tagClass;
            }
        }

        return TAG_CLASS.INVALID;
    }

    private static ExportRenderStrategy CreateRenderStrategy(EntityDef entityDef)
    {
        if (entityDef == null)
        {
            return CreateUnifiedCardStrategy(
                ActorNames.GetHandActor(TAG_CARDTYPE.MINION, TAG_PREMIUM.NORMAL, null),
                ExportRenderStrategyKind.Default);
        }

        ExportRenderStrategy strategy;
        if (entityDef.IsHeroSkin())
        {
            strategy = new ExportRenderStrategy(
                ActorNames.GetHeroSkinOrHandActor(entityDef, TAG_PREMIUM.NORMAL),
                ExportRenderStrategyKind.HeroSkin,
                createBannedRibbon: false,
                customInitialize: static (actorObject, actor, def) =>
                {
                    var heroSkin = actorObject.GetComponent<CollectionHeroSkin>();
                    if (heroSkin != null)
                        heroSkin.SetClass(def);
                },
                orthographicSizeMultiplier: 1.12f,
                distanceMultiplier: 1.05f);
            return ApplyAutomaticAlphaCapture(entityDef, strategy);
        }

        if (entityDef.IsPet())
        {
            strategy = new ExportRenderStrategy(
                "Card_Pet_Skin.prefab:7d865418b931b41468d56109238ce3a5",
                ExportRenderStrategyKind.Pet,
                createBannedRibbon: false,
                customInitialize: static (actorObject, actor, def) =>
                {
                    var petSkin = actorObject.GetComponent<CollectionPetSkin>();
                    if (petSkin != null)
                    {
                        petSkin.SetParentPet(true, true);
                        petSkin.HideText();
                        petSkin.DisableFavoriteBanner(true);
                    }

                    var petControllerUi = actorObject.GetComponentInChildren<PetControllerUI>(true);
                    if (petControllerUi != null)
                        petControllerUi.SetPetMaterialData(PetDrawType.Popup, 0);
                },
                activateRootObjectBeforeShow: true,
                updateComponentsAfterShow: true,
                updateAllComponentsIgnoreSpells: true,
                orthographicSizeMultiplier: 1.18f,
                distanceMultiplier: 1.1f,
                cameraOffset: new Vector3(0f, -0.05f, 0f));
            return ApplyAutomaticAlphaCapture(entityDef, strategy);
        }

        switch (entityDef.GetCardType())
        {
            case TAG_CARDTYPE.LOCATION:
                strategy = CreateUnifiedCardStrategy(
                    ActorNames.GetHandActor(entityDef, TAG_PREMIUM.NORMAL),
                    ExportRenderStrategyKind.Location,
                    useDualBackgroundAlphaCapture: true,
                    cameraOffset: Vector3.zero);
                break;

            case TAG_CARDTYPE.HERO_POWER:
                strategy = CreateUnifiedCardStrategy(
                    ActorNames.GetHandActor(entityDef, TAG_PREMIUM.NORMAL),
                    ExportRenderStrategyKind.HeroPower);
                break;

            case TAG_CARDTYPE.BATTLEGROUND_HERO_BUDDY:
                strategy = CreateUnifiedCardStrategy(
                    ActorNames.GetHandActor(entityDef, TAG_PREMIUM.NORMAL),
                    ExportRenderStrategyKind.BattlegroundHeroBuddy);
                break;

            case TAG_CARDTYPE.BATTLEGROUND_QUEST_REWARD:
                strategy = CreateUnifiedCardStrategy(
                    ActorNames.GetHandActor(entityDef, TAG_PREMIUM.NORMAL),
                    ExportRenderStrategyKind.BattlegroundQuestReward);
                break;

            case TAG_CARDTYPE.BATTLEGROUND_SPELL:
                strategy = CreateUnifiedCardStrategy(
                    ActorNames.GetHandActor(entityDef, TAG_PREMIUM.NORMAL),
                    ExportRenderStrategyKind.BattlegroundSpell);
                break;

            case TAG_CARDTYPE.BATTLEGROUND_ANOMALY:
                strategy = CreateUnifiedCardStrategy(
                    ActorNames.GetHandActor(entityDef, TAG_PREMIUM.NORMAL),
                    ExportRenderStrategyKind.BattlegroundAnomaly);
                break;

            case TAG_CARDTYPE.BATTLEGROUND_TRINKET:
                strategy = CreateUnifiedCardStrategy(
                    ActorNames.GetHandActor(entityDef, TAG_PREMIUM.NORMAL),
                    ExportRenderStrategyKind.BattlegroundTrinket,
                    useDualBackgroundAlphaCapture: true);
                break;

            default:
                strategy = CreateUnifiedCardStrategy(
                    ActorNames.GetHandActor(entityDef, TAG_PREMIUM.NORMAL),
                    ExportRenderStrategyKind.Default);
                break;
        }

        return ApplyAutomaticAlphaCapture(entityDef, strategy);
    }

    private static ExportRenderStrategy CreateUnifiedCardStrategy(
        string actorPath,
        ExportRenderStrategyKind kind,
        bool useDualBackgroundAlphaCapture = false,
        Vector3? cameraOffset = null)
    {
        return new ExportRenderStrategy(
            actorPath,
            kind,
            useDualBackgroundAlphaCapture: useDualBackgroundAlphaCapture,
            orthographicSizeMultiplier: UnifiedCardSlotOrthographicSizeMultiplier,
            distanceMultiplier: UnifiedCardSlotDistanceMultiplier,
            cameraOffset: cameraOffset ?? Vector3.zero);
    }

    private static ExportRenderStrategy ApplyAutomaticAlphaCapture(EntityDef entityDef, ExportRenderStrategy strategy)
    {
        if (entityDef == null || strategy.UseDualBackgroundAlphaCapture || !RequiresAccurateAlphaCapture(entityDef))
            return strategy;

        return new ExportRenderStrategy(
            strategy.ActorPath,
            strategy.Kind,
            strategy.CreateBannedRibbon,
            strategy.CustomInitialize,
            useDualBackgroundAlphaCapture: true,
            activateRootObjectBeforeShow: strategy.ActivateRootObjectBeforeShow,
            updateComponentsAfterShow: strategy.UpdateComponentsAfterShow,
            updateAllComponentsIgnoreSpells: strategy.UpdateAllComponentsIgnoreSpells,
            orthographicSizeMultiplier: strategy.OrthographicSizeMultiplier,
            distanceMultiplier: strategy.DistanceMultiplier,
            cameraOffset: strategy.CameraOffset);
    }

    private static bool RequiresAccurateAlphaCapture(EntityDef entityDef)
    {
        if (entityDef.HasRuneCost || entityDef.HasDeckAction() || entityDef.IsElite())
            return true;

        var classes = new List<TAG_CLASS>();
        entityDef.GetClasses(classes);
        return classes.Count > 2;
    }
}


internal enum ExportRenderStrategyKind
{
    Default,
    HeroSkin,
    Pet,
    Location,
    HeroPower,
    BattlegroundHeroBuddy,
    BattlegroundQuestReward,
    BattlegroundSpell,
    BattlegroundAnomaly,
    BattlegroundTrinket
}

internal sealed class ExportRenderStrategy
{
    public ExportRenderStrategy(
        string actorPath,
        ExportRenderStrategyKind kind,
        bool createBannedRibbon = true,
        Action<GameObject, Actor, EntityDef> customInitialize = null,
        bool useDualBackgroundAlphaCapture = false,
        bool activateRootObjectBeforeShow = false,
        bool updateComponentsAfterShow = false,
        bool updateAllComponentsIgnoreSpells = false,
        float orthographicSizeMultiplier = 1.08f,
        float distanceMultiplier = 1f,
        Vector3? cameraOffset = null)
    {
        ActorPath = actorPath;
        Kind = kind;
        CreateBannedRibbon = createBannedRibbon;
        CustomInitialize = customInitialize;
        UseDualBackgroundAlphaCapture = useDualBackgroundAlphaCapture;
        ActivateRootObjectBeforeShow = activateRootObjectBeforeShow;
        UpdateComponentsAfterShow = updateComponentsAfterShow;
        UpdateAllComponentsIgnoreSpells = updateAllComponentsIgnoreSpells;
        OrthographicSizeMultiplier = orthographicSizeMultiplier;
        DistanceMultiplier = distanceMultiplier;
        CameraOffset = cameraOffset ?? Vector3.zero;
    }

    public string ActorPath { get; }

    public ExportRenderStrategyKind Kind { get; }

    public bool CreateBannedRibbon { get; }

    public Action<GameObject, Actor, EntityDef> CustomInitialize { get; }

    public bool UseDualBackgroundAlphaCapture { get; }

    public bool ActivateRootObjectBeforeShow { get; }

    public bool UpdateComponentsAfterShow { get; }

    public bool UpdateAllComponentsIgnoreSpells { get; }

    public float OrthographicSizeMultiplier { get; }

    public float DistanceMultiplier { get; }

    public Vector3 CameraOffset { get; }
}



