// ============================================================================
// HsCardImageExporter 插件入口
//
// 文件说明：
//   运行在游戏进程内的独立导图插件。
//   目标是批量导出炉石完整卡牌和格式模式 PNG，不接入 Ember 现有主工程链路。
// ============================================================================

using BepInEx;
using BepInEx.Configuration;
using Hearthstone.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace HsCardImageExporter;

/// <summary>
/// 游戏内卡牌图片导出插件入口。
/// </summary>
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
    private const string FormatTypePickerPrefabPath = "FormatTypePickerPopup.prefab:aa88133d144782b40b3fd8818084006c";
    private const string SetRotationIconPrefabPath = "SetRotationIcon.prefab:d9f391fb2af2ba1478cc806fe5c5f014";
    private const int ModeCaptureWarmupFrames = 45;
    private const int MinModeCaptureStableFrames = 3;

    private ConfigEntry<bool> _enableExport = null!;
    private ConfigEntry<bool> _exportCards = null!;
    private ConfigEntry<bool> _exportFormatModes = null!;
    private ConfigEntry<string> _outputDir = null!;
    private ConfigEntry<string> _modeOutputDir = null!;
    private ConfigEntry<int> _maxCount = null!;
    private ConfigEntry<int> _renderWidth = null!;
    private ConfigEntry<int> _renderHeight = null!;
    private ConfigEntry<int> _thumbWidth = null!;
    private ConfigEntry<int> _thumbHeight = null!;
    private ConfigEntry<int> _modeImageWidth = null!;
    private ConfigEntry<int> _modeImageHeight = null!;
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

    /// <summary>
    /// Unity Awake 回调。
    /// </summary>
    private void Awake()
    {
        _enableExport = Config.Bind("General", "EnableExport", true, "是否在启动后执行图片导出。");
        _exportCards = Config.Bind("General", "ExportCards", true, "是否导出完整卡牌图片。");
        _exportFormatModes = Config.Bind("General", "ExportFormatModes", true, "是否导出狂野、标准、休闲三个格式模式图片。");
        _outputDir = Config.Bind("General", "OutputDir", Path.Combine(BepInEx.Paths.BepInExRootPath, "HsCardExport", "cards"), "完整卡牌 PNG 输出目录。");
        _modeOutputDir = Config.Bind("General", "ModeOutputDir", Path.Combine(BepInEx.Paths.BepInExRootPath, "HsCardExport", "modes"), "格式模式 PNG 输出目录。");
        _maxCount = Config.Bind("General", "MaxCount", 0, "本次最多导出的卡牌数量。0 表示全部。");
        _renderWidth = Config.Bind("General", "RenderWidth", 1536, "内部渲染宽度。");
        _renderHeight = Config.Bind("General", "RenderHeight", 2304, "内部渲染高度。");
        _thumbWidth = Config.Bind("General", "ThumbWidth", 512, "列表图宽度。");
        _thumbHeight = Config.Bind("General", "ThumbHeight", 768, "列表图高度。");
        _modeImageWidth = Config.Bind("General", "ModeImageWidth", 512, "格式模式图片宽度。");
        _modeImageHeight = Config.Bind("General", "ModeImageHeight", 512, "格式模式图片高度。");
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

    /// <summary>
    /// Unity Start 回调。
    /// </summary>
    private void Start()
    {
        Logger.LogInfo($"{PluginName} loaded.");
        if (_enableExport.Value)
            StartCoroutine(WaitAndExport());
    }

    /// <summary>
    /// 等待游戏资源系统就绪后开始导图。
    /// </summary>
    private System.Collections.IEnumerator WaitAndExport()
    {
        while (!AreExportPrerequisitesReady())
            yield return new WaitForSeconds(1f);

        EnsureExportRuntime();
        if (_exportCards.Value)
            Directory.CreateDirectory(_outputDir.Value);
        if (_exportFormatModes.Value)
            Directory.CreateDirectory(_modeOutputDir.Value);
        Logger.LogInfo("Image export prerequisites are ready.");

        if (_exportCards.Value)
            yield return ExportCards();

        if (_exportFormatModes.Value)
            yield return ExportFormatModes();

        if (_autoDisableWhenDone.Value)
        {
            _enableExport.Value = false;
            Config.Save();
        }
    }

    /// <summary>
    /// 判断本次启用的导出功能是否已具备运行条件。
    /// </summary>
    private bool AreExportPrerequisitesReady()
    {
        if (AssetLoader.Get() == null)
            return false;

        if (!_exportCards.Value && !_exportFormatModes.Value)
            return true;

        return GameDbf.Card != null &&
               GameDbf.Card.GetRecords().Count > 0 &&
               DefLoader.Get() != null &&
               DefLoader.Get().HasLoadedEntityDefs();
    }

    /// <summary>
    /// 确保离屏导图运行时对象已创建。
    /// </summary>
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

    /// <summary>
    /// 批量导出卡牌图片。
    /// </summary>
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

    /// <summary>
    /// 导出狂野、标准、休闲三个格式模式图片。
    /// </summary>
    private IEnumerator ExportFormatModes()
    {
        var targets = FormatModeExportTarget.CreateDefaults();
        var pendingTargets = _skipExisting.Value
            ? targets.Where(target => !File.Exists(GetModeOutputPath(target))).ToList()
            : targets;
        if (pendingTargets.Count == 0)
        {
            Logger.LogInfo("Format mode image export skipped: all files already exist.");
            yield break;
        }

        Logger.LogInfo($"Format mode export queue size: {pendingTargets.Count}");
        GameObject popupObject = null;

        try
        {
            popupObject = AssetLoader.Get().InstantiatePrefab(FormatTypePickerPrefabPath, AssetLoadingOptions.IgnorePrefabPosition);
            if (popupObject == null)
            {
                Logger.LogWarning("Skip format mode export: popup prefab unavailable.");
                yield break;
            }

            popupObject.transform.SetParent(_exportRoot, false);
            popupObject.transform.localPosition = Vector3.zero;
            popupObject.transform.localRotation = Quaternion.identity;
            popupObject.transform.localScale = Vector3.one;
            SetLayerRecursively(popupObject.transform, ExportLayer);

            var widget = popupObject.GetComponent<Widget>() ?? popupObject.GetComponentInChildren<Widget>(true);
            if (widget == null)
            {
                Logger.LogWarning("Skip format mode export: popup widget missing.");
                yield break;
            }

            ShowFormatModePicker(widget);
            yield return WaitForFormatModePickerReady(popupObject);
            yield return InitializeNestedFormatModeWidgets(popupObject);
            InitializeFormatModeYearIcons(popupObject);
            yield return new WaitForEndOfFrame();

            var rendererGroups = CreateFormatModeRendererGroups(popupObject, targets);
            if (!rendererGroups.TryGetValue(FormatModeExportKind.Wild, out _) ||
                !rendererGroups.TryGetValue(FormatModeExportKind.Standard, out _) ||
                !rendererGroups.TryGetValue(FormatModeExportKind.Casual, out _))
            {
                Logger.LogWarning("Skip format mode export: visible renderers could not be split into three mode groups.");
                yield break;
            }

            foreach (var target in pendingTargets)
            {
                if (!rendererGroups.TryGetValue(target.Kind, out var rendererGroup))
                    continue;

                yield return ExportSingleFormatMode(popupObject, target, rendererGroups.Values, rendererGroup);
            }

            Logger.LogInfo("Format mode image export finished.");
        }
        finally
        {
            if (popupObject != null)
                Destroy(popupObject);
        }
    }

    /// <summary>
    /// 初始化格式弹窗内的嵌套 UI 模板，标准模式年度图标正是通过 WidgetInstance 加载出来的。
    /// </summary>
    private static IEnumerator InitializeNestedFormatModeWidgets(GameObject popupObject)
    {
        var widgetInstances = popupObject.GetComponentsInChildren<WidgetInstance>(true);
        foreach (var widgetInstance in widgetInstances)
        {
            if (widgetInstance == null)
                continue;

            widgetInstance.Initialize();
            widgetInstance.Show();
        }

        for (var frame = 0; frame < ModeCaptureWarmupFrames; frame++)
        {
            yield return new WaitForEndOfFrame();
            if (popupObject == null)
                yield break;

            SetLayerRecursively(popupObject.transform, ExportLayer);
            var hasYearIcon = HasFormatModeYearIcon(popupObject);
            var allWidgetsReady = popupObject
                .GetComponentsInChildren<WidgetInstance>(true)
                .All(widgetInstance => widgetInstance == null || !widgetInstance.StartedInitialization || widgetInstance.IsInitialized);

            if (hasYearIcon && allWidgetsReady)
                yield break;
        }

        if (!HasFormatModeYearIcon(popupObject))
        {
            InstantiateStandardSetRotationIcon(popupObject);
            yield return new WaitForEndOfFrame();
            if (popupObject != null)
                SetLayerRecursively(popupObject.transform, ExportLayer);
        }
    }

    /// <summary>
    /// 判断标准模式年度图标模板是否已经实例化。
    /// </summary>
    private static bool HasFormatModeYearIcon(GameObject popupObject)
    {
        return popupObject
            .GetComponentsInChildren<SetRotationIcon>(true)
            .Any(icon => icon != null && icon.m_YearIconQuad != null);
    }

    /// <summary>
    /// 在离屏环境中手动实例化标准模式年度图标模板。
    /// </summary>
    private static void InstantiateStandardSetRotationIcon(GameObject popupObject)
    {
        var iconHost = popupObject
            .GetComponentsInChildren<WidgetInstance>(true)
            .FirstOrDefault(widgetInstance =>
                widgetInstance != null &&
                widgetInstance.gameObject.name.Equals("SetRotationIcon", StringComparison.OrdinalIgnoreCase));
        if (iconHost == null)
            return;

        var iconObject = AssetLoader.Get().InstantiatePrefab(SetRotationIconPrefabPath, AssetLoadingOptions.IgnorePrefabPosition);
        if (iconObject == null)
            return;

        iconObject.transform.SetParent(iconHost.transform, false);
        iconObject.transform.localPosition = Vector3.zero;
        iconObject.transform.localRotation = Quaternion.identity;
        iconObject.transform.localScale = Vector3.one;
    }

    /// <summary>
    /// 显式初始化标准模式徽章上的年度图标。
    /// </summary>
    private static void InitializeFormatModeYearIcons(GameObject popupObject)
    {
        var foregroundOffset = popupObject.transform.up.sqrMagnitude > 0.001f
            ? popupObject.transform.up.normalized * 0.05f
            : Vector3.up * 0.05f;

        foreach (var setRotationIcon in popupObject.GetComponentsInChildren<SetRotationIcon>(true))
            InitializeSingleFormatModeYearIcon(setRotationIcon, foregroundOffset);
    }

    /// <summary>
    /// 初始化单个年度图标节点的显示状态和年度贴图偏移。
    /// </summary>
    private static void InitializeSingleFormatModeYearIcon(
        SetRotationIcon setRotationIcon,
        Vector3 foregroundOffset)
    {
        if (setRotationIcon == null || setRotationIcon.m_YearIconQuad == null)
            return;

        setRotationIcon.m_YearIconQuad.SetActive(true);
        setRotationIcon.m_YearIconQuad.transform.position += foregroundOffset;

        var iconRenderer = setRotationIcon.m_YearIconQuad.GetComponent<Renderer>();
        if (iconRenderer == null)
            return;

        iconRenderer.enabled = true;
        var material = iconRenderer.material;
        if (material == null)
            return;

        var offset = SetRotationIcon.GetYearIconTextureOffset();
        material.mainTextureScale = new Vector2(0.5f, 0.5f);
        material.mainTextureOffset = offset;
        if (material.HasProperty("_MainTex"))
        {
            material.SetTextureScale("_MainTex", new Vector2(0.5f, 0.5f));
            material.SetTextureOffset("_MainTex", offset);
        }
    }

    /// <summary>
    /// 使用游戏原生事件打开三按钮格式选择弹窗。
    /// </summary>
    private static void ShowFormatModePicker(Widget widget)
    {
        widget.Show();
        widget.TriggerEvent("3BUTTONS", default(TriggerEventParameters));
        widget.TriggerEvent("OPEN", new TriggerEventParameters(null, VisualsFormatType.VFT_STANDARD, false, false));
    }

    /// <summary>
    /// 等待格式选择弹窗动画和异步子资源稳定。
    /// </summary>
    private static IEnumerator WaitForFormatModePickerReady(GameObject popupObject)
    {
        var previousRendererCount = -1;
        var hasPreviousBounds = false;
        var previousBounds = default(Bounds);
        var stableFrameCount = 0;

        for (var frame = 0; frame < ModeCaptureWarmupFrames; frame++)
        {
            yield return new WaitForEndOfFrame();
            if (popupObject == null)
                yield break;

            SetLayerRecursively(popupObject.transform, ExportLayer);

            var visibleRenderers = GetVisibleRenderers(popupObject);
            var visibleRendererCount = visibleRenderers.Count;
            var hasCurrentBounds = TryGetRendererGroupBounds(visibleRenderers, out var currentBounds);
            var isWaitingOnAssets = IsObjectWaitingOnAssets(popupObject);
            if (!isWaitingOnAssets &&
                visibleRendererCount > 0 &&
                visibleRendererCount == previousRendererCount &&
                hasPreviousBounds &&
                hasCurrentBounds &&
                AreBoundsClose(previousBounds, currentBounds))
            {
                stableFrameCount++;
                if (stableFrameCount >= MinModeCaptureStableFrames)
                    yield break;
            }
            else
            {
                stableFrameCount = 0;
            }

            previousRendererCount = visibleRendererCount;
            previousBounds = currentBounds;
            hasPreviousBounds = hasCurrentBounds;
        }
    }

    /// <summary>
    /// 判断连续帧包围盒是否已经基本停止变化。
    /// </summary>
    private static bool AreBoundsClose(Bounds previousBounds, Bounds currentBounds)
    {
        return (previousBounds.center - currentBounds.center).sqrMagnitude < 0.0001f &&
               (previousBounds.size - currentBounds.size).sqrMagnitude < 0.0001f;
    }

    /// <summary>
    /// 判断对象层级内是否还有异步资源未实例化。
    /// </summary>
    private static bool IsObjectWaitingOnAssets(GameObject rootObject)
    {
        var assetLoader = AssetLoader.Get();
        if (assetLoader == null || rootObject == null)
            return false;

        foreach (var transform in rootObject.GetComponentsInChildren<Transform>(true))
        {
            if (assetLoader.IsWaitingOnObject(transform.gameObject))
                return true;
        }

        return false;
    }

    /// <summary>
    /// 导出单个格式模式图片。
    /// </summary>
    private IEnumerator ExportSingleFormatMode(
        GameObject popupObject,
        FormatModeExportTarget target,
        IEnumerable<List<Renderer>> allGroups,
        List<Renderer> activeGroup)
    {
        var upperRendererGroup = CreateUpperFormatModeRendererGroup(activeGroup);
        SetRendererGroupVisibility(allGroups, upperRendererGroup);
        yield return new WaitForEndOfFrame();

        if (!TryGetRendererGroupBounds(upperRendererGroup, out var bounds))
        {
            Logger.LogWarning($"Skip format mode {target.FileName}: render bounds unavailable.");
            RestoreRendererVisibility(allGroups);
            yield break;
        }

        try
        {
            bounds = ExpandBounds(bounds, _boundsPaddingRatio.Value);
            ConfigureCamera(bounds, popupObject.transform, new ExportRenderStrategy(string.Empty, ExportRenderStrategyKind.Default), false);
            SaveFormatModePng(target);
            Logger.LogInfo($"Exported format mode image: {target.FileName}");
        }
        finally
        {
            RestoreRendererVisibility(allGroups);
        }
    }

    /// <summary>
    /// 从单列模式渲染器中只保留上方徽章区域。
    /// </summary>
    private static List<Renderer> CreateUpperFormatModeRendererGroup(IReadOnlyList<Renderer> renderers)
    {
        if (!TryGetRendererGroupBounds(renderers, out var bounds))
            return renderers.ToList();

        var splitY = FindFormatModeUpperRendererSplitY(renderers, bounds);
        var tolerance = bounds.size.y * 0.03f;
        var upperRenderers = renderers
            .Where(renderer =>
                renderer != null &&
                renderer.bounds.max.y >= splitY + tolerance)
            .ToList();

        return upperRenderers.Count > 0 ? upperRenderers : renderers.ToList();
    }

    /// <summary>
    /// 根据上下渲染器包围盒之间的空隙定位徽章和说明卷轴的分界线。
    /// </summary>
    private static float FindFormatModeUpperRendererSplitY(IReadOnlyList<Renderer> renderers, Bounds groupBounds)
    {
        var intervals = renderers
            .Where(renderer => renderer != null && renderer.bounds.size.y > 0.0001f)
            .Select(renderer => new Vector2(renderer.bounds.min.y, renderer.bounds.max.y))
            .OrderBy(interval => interval.x)
            .ToList();

        if (intervals.Count == 0)
            return groupBounds.center.y;

        var contentHeight = Mathf.Max(0.001f, groupBounds.size.y);
        var minGapHeight = Mathf.Max(0.001f, contentHeight * 0.035f);
        var currentMaxY = intervals[0].y;

        for (var i = 1; i < intervals.Count; i++)
        {
            var interval = intervals[i];
            if (interval.x <= currentMaxY)
            {
                currentMaxY = Mathf.Max(currentMaxY, interval.y);
                continue;
            }

            var gapHeight = interval.x - currentMaxY;
            var gapCenterY = (currentMaxY + interval.x) * 0.5f;
            var gapRatio = (gapCenterY - groupBounds.min.y) / contentHeight;

            // 卷轴内部的小间隙通常偏下，徽章内部装饰间隙通常偏上；中段第一个明显空隙就是两块内容的分界。
            if (gapHeight >= minGapHeight && gapRatio >= 0.25f && gapRatio <= 0.78f)
                return gapCenterY;

            currentMaxY = interval.y;
        }

        return groupBounds.min.y + contentHeight * 0.52f;
    }

    /// <summary>
    /// 将弹窗可见渲染器按横向位置拆成狂野、标准、休闲三组。
    /// </summary>
    private static Dictionary<FormatModeExportKind, List<Renderer>> CreateFormatModeRendererGroups(
        GameObject popupObject,
        IReadOnlyList<FormatModeExportTarget> targets)
    {
        var renderers = GetVisibleRenderers(popupObject);
        var groups = targets.ToDictionary(target => target.Kind, _ => new List<Renderer>());
        if (renderers.Count == 0)
            return groups;

        if (!TryGetRendererGroupBounds(renderers, out var allBounds))
            return groups;

        var anchors = CreateFallbackFormatModeAnchors(allBounds, targets);
        ApplyTooltipZoneAnchors(popupObject, anchors);

        foreach (var renderer in renderers)
        {
            var centerX = renderer.bounds.center.x;
            var nearestTarget = targets[0];
            var nearestDistance = Mathf.Abs(centerX - anchors[nearestTarget.Kind]);
            for (var i = 1; i < targets.Count; i++)
            {
                var target = targets[i];
                var distance = Mathf.Abs(centerX - anchors[target.Kind]);
                if (distance < nearestDistance)
                {
                    nearestTarget = target;
                    nearestDistance = distance;
                }
            }

            groups[nearestTarget.Kind].Add(renderer);
        }

        return groups;
    }

    /// <summary>
    /// 按弹窗整体宽度创建左中右三列的兜底锚点。
    /// </summary>
    private static Dictionary<FormatModeExportKind, float> CreateFallbackFormatModeAnchors(
        Bounds allBounds,
        IReadOnlyList<FormatModeExportTarget> targets)
    {
        var minX = allBounds.min.x;
        var width = Mathf.Max(0.001f, allBounds.size.x);
        var ratios = new[] { 0.17f, 0.50f, 0.83f };
        var anchors = new Dictionary<FormatModeExportKind, float>();

        for (var i = 0; i < targets.Count && i < ratios.Length; i++)
            anchors[targets[i].Kind] = minX + width * ratios[i];

        return anchors;
    }

    /// <summary>
    /// 使用弹窗自带 TooltipZone 锚点修正三列定位。
    /// </summary>
    private static void ApplyTooltipZoneAnchors(GameObject popupObject, IDictionary<FormatModeExportKind, float> anchors)
    {
        var tooltipDisplay = popupObject.GetComponentInChildren<FormatTooltipDisplay>(true);
        if (tooltipDisplay == null)
            return;

        ApplyTooltipZoneAnchor(anchors, FormatModeExportKind.Wild, tooltipDisplay.m_wildToolTipZone);
        ApplyTooltipZoneAnchor(anchors, FormatModeExportKind.Standard, tooltipDisplay.m_standardToolTipZone);
        ApplyTooltipZoneAnchor(anchors, FormatModeExportKind.Casual, tooltipDisplay.m_casualToolTipZone);
    }

    /// <summary>
    /// 将单个 TooltipZone 的世界坐标写入模式分组锚点。
    /// </summary>
    private static void ApplyTooltipZoneAnchor(
        IDictionary<FormatModeExportKind, float> anchors,
        FormatModeExportKind kind,
        TooltipZone tooltipZone)
    {
        if (tooltipZone != null)
            anchors[kind] = tooltipZone.transform.position.x;
    }

    /// <summary>
    /// 获取当前可见且参与截图的渲染器。
    /// </summary>
    private static List<Renderer> GetVisibleRenderers(GameObject rootObject)
    {
        return rootObject
            .GetComponentsInChildren<Renderer>(true)
            .Where(renderer =>
                renderer != null &&
                renderer.enabled &&
                renderer.gameObject.activeInHierarchy &&
                renderer.bounds.size.sqrMagnitude > 0.0001f)
            .ToList();
    }

    /// <summary>
    /// 计算一组渲染器的总包围盒。
    /// </summary>
    private static bool TryGetRendererGroupBounds(IReadOnlyList<Renderer> renderers, out Bounds bounds)
    {
        var firstRenderer = renderers.FirstOrDefault(renderer =>
            renderer != null &&
            renderer.enabled &&
            renderer.gameObject.activeInHierarchy);
        if (firstRenderer == null)
        {
            bounds = default;
            return false;
        }

        bounds = firstRenderer.bounds;
        foreach (var renderer in renderers)
        {
            if (renderer == null ||
                !renderer.enabled ||
                !renderer.gameObject.activeInHierarchy)
            {
                continue;
            }

            bounds.Encapsulate(renderer.bounds);
        }

        return true;
    }

    /// <summary>
    /// 只保留当前模式组渲染器可见，避免相邻模式进入截图。
    /// </summary>
    private static void SetRendererGroupVisibility(IEnumerable<List<Renderer>> allGroups, IReadOnlyCollection<Renderer> activeGroup)
    {
        foreach (var renderer in allGroups.SelectMany(group => group))
        {
            if (renderer != null)
                renderer.enabled = activeGroup.Contains(renderer);
        }
    }

    /// <summary>
    /// 恢复格式弹窗全部模式渲染器可见。
    /// </summary>
    private static void RestoreRendererVisibility(IEnumerable<List<Renderer>> allGroups)
    {
        foreach (var renderer in allGroups.SelectMany(group => group))
        {
            if (renderer != null)
                renderer.enabled = true;
        }
    }

    /// <summary>
    /// 获取本次需要导出的卡牌列表。
    /// </summary>
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

        if (cardSetFilters.Count > 0 ||
            cardIdFilters.Count > 0 ||
            excludedCardSetFilters.Count > 0 ||
            excludedCardTypes.Count > 0 ||
            excludedSpellSchools.Count > 0)
        {
            ids = ids
                .Where(cardId =>
                {
                    var entityDef = DefLoader.Get().GetEntityDef(cardId);
                    return entityDef != null &&
                           IsCardMatchedByFilters(
                               cardId,
                               entityDef,
                               cardSetFilters,
                               cardIdFilters,
                               excludedCardSetFilters,
                               excludedCardTypes,
                               excludedSpellSchools);
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

    /// <summary>
    /// 当前是否使用预览抽样导出。
    /// </summary>
    private bool ShouldUsePreviewSampling()
    {
        // 指定卡包或卡牌后直接导出完整结果，避免仍然落到 preview 子集。
        return _previewMode.Value && !HasExplicitFilter();
    }

    /// <summary>
    /// 当前是否配置了显式导出筛选。
    /// </summary>
    private bool HasExplicitFilter()
    {
        return !string.IsNullOrWhiteSpace(_cardSetFilter.Value) ||
               !string.IsNullOrWhiteSpace(_cardIdFilter.Value) ||
               !string.IsNullOrWhiteSpace(_excludedCardSets.Value) ||
               !string.IsNullOrWhiteSpace(_excludedCardTypes.Value) ||
               !string.IsNullOrWhiteSpace(_excludedSpellSchools.Value);
    }

    /// <summary>
    /// 将逗号分隔的筛选配置拆分为去重后的条件列表。
    /// </summary>
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

    /// <summary>
    /// 判断卡牌是否命中本次导出筛选。
    /// </summary>
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

    /// <summary>
    /// 将法术派系排除配置解析为标签数值集合。
    /// </summary>
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

    /// <summary>
    /// 将卡牌类型排除配置解析为标签数值集合。
    /// </summary>
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

    /// <summary>
    /// 尝试将法术派系配置解析为 GAME_TAG.SPELL_SCHOOL 对应数值。
    /// </summary>
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

    /// <summary>
    /// 尝试将卡牌类型配置解析为 GAME_TAG.CARDTYPE 对应数值。
    /// </summary>
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

    /// <summary>
    /// 判断卡包是否命中筛选条件。
    /// </summary>
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

    /// <summary>
    /// 忽略大小写判断字符串是否包含筛选值。
    /// </summary>
    private static bool ContainsIgnoreCase(string source, string value)
    {
        return !string.IsNullOrWhiteSpace(source) &&
               !string.IsNullOrWhiteSpace(value) &&
               source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>
    /// 预览模式下按渲染类别抽样卡牌。
    /// </summary>
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

    /// <summary>
    /// 导出单张卡牌图片。
    /// </summary>
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
                // 放进收藏页固定卡槽后，必须保留游戏原生的槽内定位。
                // 如果继续按每张卡自己的包围盒居中，会把地标水晶和法力值位置重新挪偏。
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

    /// <summary>
    /// 按渲染策略初始化卡牌 Actor。
    /// </summary>
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
            // 收藏页标准链路会先完成组件刷新，再在 CollectionCardVisual 内统一执行 Show。
            actor.UpdateAllComponents(strategy.UpdateAllComponentsIgnoreSpells);
        }
        else
        {
            FinalizeActorPresentationForExport(actorObject, actor, strategy);
        }

        SetLayerRecursively(actorObject.transform, ExportLayer);
    }

    /// <summary>
    /// 将 Actor 切换到可截图的最终展示状态。
    /// </summary>
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

    /// <summary>
    /// 针对多职业上下文卡补充初始化后的可见性修正。
    /// </summary>
    private static void ApplyPostInitializeOverrides(Actor actor, bool forceMulticlassRibbon)
    {
        if (forceMulticlassRibbon && actor.m_multiclassRibbon != null)
            actor.m_multiclassRibbon.SetActive(true);
    }

    /// <summary>
    /// 等待卡牌 Actor 在导出前进入稳定状态。
    /// </summary>
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

            // 全量批跑时，侧边挂件可能在前几帧才补进层级。
            // 这里等到层级和渲染器数量稳定，再进入双底色截图，避免两次渲染之间对象状态不一致。
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

    /// <summary>
    /// 判断当前 Actor 是否仍有资源实例化尚未完成。
    /// </summary>
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

    /// <summary>
    /// 判断导出时容易缺失的挂件 prefab 是否已经就绪。
    /// </summary>
    private static bool HasPendingExportDecor(Actor actor)
    {
        if (actor == null)
            return false;

        return IsPendingNestedPrefab(actor.m_hearthstoneFactionBannerContainer) ||
               IsPendingNestedPrefab(actor.m_tradeableBannerContainer) ||
               IsPendingNestedPrefab(actor.m_forgeBannerContainer) ||
               IsPendingNestedPrefab(actor.m_bannedRibbonContainer);
    }

    /// <summary>
    /// 判断激活中的嵌套 prefab 是否仍未真正创建完成。
    /// </summary>
    private static bool IsPendingNestedPrefab(NestedPrefab nestedPrefab)
    {
        return nestedPrefab != null &&
               nestedPrefab.gameObject.activeSelf &&
               !nestedPrefab.PrefabIsLoaded();
    }

    /// <summary>
    /// 递归设置导图层。
    /// </summary>
    private static void SetLayerRecursively(Transform root, int layer)
    {
        root.gameObject.layer = layer;
        foreach (Transform child in root)
            SetLayerRecursively(child, layer);
    }

    /// <summary>
    /// 计算卡牌 Actor 的渲染包围盒。
    /// </summary>
    private static bool TryGetRenderBounds(GameObject actorObject, Actor actor, out Bounds bounds)
    {
        if (TryGetStableSlotBounds(actorObject, out bounds))
            return true;

        if (TryGetCuratedActorBounds(actor, out bounds))
            return true;

        return TryGetFallbackRendererBounds(actorObject, out bounds);
    }

    /// <summary>
    /// 使用收藏页卡槽中心作为统一取景原点，再按实际卡面内容补足边界。
    /// </summary>
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

    /// <summary>
    /// 对标准卡面优先使用主卡框和玩法锚点计算边界，避免被外扩装饰拉歪统一基准。
    /// </summary>
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

        // 法力、稀有度和属性宝石才是玩家真正感知位置的稳定锚点。
        EncapsulateRendererBounds(actor.m_manaObject, ref bounds);
        EncapsulateRendererBounds(actor.m_rarityGemMesh, ref bounds);
        EncapsulateRendererBounds(actor.m_rarityNoGemMesh, ref bounds);
        EncapsulateRendererBounds(actor.m_attackObject, ref bounds);
        EncapsulateRendererBounds(actor.m_healthObject, ref bounds);
        EncapsulateRendererBounds(actor.m_armorObject, ref bounds);
        return true;
    }

    /// <summary>
    /// 将指定节点下已激活渲染器的边界并入结果。
    /// </summary>
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

    /// <summary>
    /// 尝试用收藏页卡槽的碰撞盒作为统一取景边界。
    /// </summary>
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

    /// <summary>
    /// 按游戏 BigCard 逻辑优先使用主卡面网格和策划标注的补充网格计算边界。
    /// </summary>
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

    /// <summary>
    /// 当 Actor 缺少策划边界配置时，退回到当前实例的全部渲染器边界。
    /// </summary>
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

    /// <summary>
    /// 与 BigCard 保持一致，侧任务等卡面需要把法力水晶网格纳入顶部边界。
    /// </summary>
    private static bool RequiresManaGemBounds(Actor actor)
    {
        var entity = actor.GetEntity();
        if (entity != null)
            return entity.IsSideQuest() || entity.IsSigil() || entity.IsObjective();

        var entityDef = actor.GetEntityDef();
        return entityDef != null &&
               (entityDef.IsSideQuest() || entityDef.IsSigil() || entityDef.IsObjective());
    }

    /// <summary>
    /// 在对象层级中查找指定名称的 MeshRenderer。
    /// </summary>
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

    /// <summary>
    /// 按比例扩张取景边界，为外扩边框和挂件预留安全留白。
    /// </summary>
    private static Bounds ExpandBounds(Bounds bounds, float paddingRatio)
    {
        if (paddingRatio <= 0f)
            return bounds;

        var scale = 1f + paddingRatio * 2f;
        bounds.extents = bounds.extents * scale;
        return bounds;
    }

    /// <summary>
    /// 尝试把 Actor 放进收藏页固定槽位，复用游戏自己的卡槽缩放和样式补偿。
    /// </summary>
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

    /// <summary>
    /// 判断当前卡牌是否应该复用收藏页统一卡槽取景规则。
    /// </summary>
    private static bool ShouldUseUnifiedCardSlotFraming(ExportRenderStrategy strategy)
    {
        return strategy.Kind != ExportRenderStrategyKind.HeroSkin &&
               strategy.Kind != ExportRenderStrategyKind.Pet;
    }

    /// <summary>
    /// 根据包围盒调整离屏相机。
    /// </summary>
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

    /// <summary>
    /// 将当前 RenderTexture 保存为 PNG。
    /// </summary>
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

    /// <summary>
    /// 保存单个格式模式 PNG。
    /// </summary>
    private void SaveFormatModePng(FormatModeExportTarget target)
    {
        var previous = RenderTexture.active;
        var texture = CaptureTextureWithAccurateAlpha();
        Texture2D modeTexture = null;

        try
        {
            modeTexture = ResizeTextureToCanvas(texture, _modeImageWidth.Value, _modeImageHeight.Value, _trimPadding.Value);
            var outputPath = GetModeOutputPath(target);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            File.WriteAllBytes(outputPath, modeTexture.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            if (modeTexture != null && modeTexture != texture)
                Destroy(modeTexture);
            Destroy(texture);
        }
    }

    /// <summary>
    /// 按相机当前 alpha 直接抓取渲染结果。
    /// </summary>
    private Texture2D CaptureTextureWithCameraAlpha()
    {
        var texture = new Texture2D(_renderWidth.Value, _renderHeight.Value, TextureFormat.RGBA32, false);
        CaptureCameraToTexture(new Color(0f, 0f, 0f, 0f), texture);
        return texture;
    }

    /// <summary>
    /// 通过黑底和白底两次渲染重建真实 alpha。
    /// </summary>
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

    /// <summary>
    /// 用指定背景色渲染一次并读回纹理。
    /// </summary>
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

    /// <summary>
    /// 根据黑底和白底渲染结果重建颜色与透明度。
    /// </summary>
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

            // 某些卡面材质颜色是对的，但 alpha 没有正确写入，改用黑白底色差反推。
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

    /// <summary>
    /// 将整张截图等比缩放到目标画布，避免按单卡透明边界再次改变内容大小。
    /// </summary>
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

    /// <summary>
    /// 获取单张卡牌输出路径。
    /// </summary>
    private string GetOutputPath(string cardId, TAG_CARD_SET cardSet, string variant)
    {
        var cardSetDir = GetCardSetDirectoryName(cardSet);
        var root = ShouldUsePreviewSampling()
            ? Path.Combine(_outputDir.Value, "preview")
            : _outputDir.Value;
        return Path.Combine(root, variant, cardSetDir, $"{cardId}.png");
    }

    /// <summary>
    /// 获取格式模式图片输出路径。
    /// </summary>
    private string GetModeOutputPath(FormatModeExportTarget target)
    {
        return Path.Combine(_modeOutputDir.Value, $"{target.FileName}.png");
    }

    /// <summary>
    /// 获取卡牌包输出目录名。
    /// </summary>
    private static string GetCardSetDirectoryName(TAG_CARD_SET cardSet)
    {
        var displayName = cardSet == TAG_CARD_SET.INVALID ? "UNKNOWN" : GameStrings.GetCardSetName(cardSet);
        var safeDisplayName = SanitizePathSegment(displayName);
        var enumName = cardSet.ToString();
        return $"{(int)cardSet:D4}_{enumName}_{safeDisplayName}";
    }

    /// <summary>
    /// 清理非法路径字符，生成可用目录名。
    /// </summary>
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

    /// <summary>
    /// 为导图准备实体定义，必要时补齐多职业卡的职业上下文。
    /// </summary>
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

        // 一部分多职业卡的边框和关键词文案直接读取 entityDef.GetClass()，
        // 导图时把当前上下文职业写进克隆体，再单独把多职业丝带显示回来。
        var clonedEntityDef = sourceEntityDef.Clone();
        clonedEntityDef.SetTag(GAME_TAG.CLASS, (int)resolvedClassContext);
        clonedEntityDef.SetTag(GAME_TAG.MULTIPLE_CLASSES, 0);
        return (clonedEntityDef, true);
    }

    /// <summary>
    /// 解析本次导图可用的职业上下文。
    /// </summary>
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

    /// <summary>
    /// 获取当前对局或收藏页提供的职业上下文。
    /// </summary>
    private static List<TAG_CLASS> GetRuntimeClassContexts()
    {
        var contexts = new List<TAG_CLASS>();
        AddFriendlyHeroClassContext(contexts);

        foreach (var collectionClass in CollectionManager.GetCollectionManagerClasses())
            AddDistinctClassContext(contexts, collectionClass);

        return contexts;
    }

    /// <summary>
    /// 从当前友方英雄读取导图上下文职业。
    /// </summary>
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

    /// <summary>
    /// 将职业上下文去重后加入结果列表。
    /// </summary>
    private static void AddDistinctClassContext(List<TAG_CLASS> contexts, TAG_CLASS tagClass)
    {
        if (tagClass == TAG_CLASS.INVALID || contexts.Contains(tagClass))
            return;

        contexts.Add(tagClass);
    }

    /// <summary>
    /// 将配置中的职业上下文文本解析为职业枚举。
    /// </summary>
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

    /// <summary>
    /// 获取导图使用的 actor 路径，尽量与收藏页展示逻辑保持一致。
    /// </summary>
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
                    // 单卡展示更接近弹出卡面，使用 Popup 遮罩配置避免列表页的占位文字和层级依赖。
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

    /// <summary>
    /// 为标准卡面创建统一的导图策略，避免不同类型继续分散维护取景倍率。
    /// </summary>
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

    /// <summary>
    /// 对符文和侧边挂件等容易丢透明层的卡面自动启用双底色 alpha 重建。
    /// </summary>
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

    /// <summary>
    /// 判断当前卡面是否包含容易在单次透明抓取中丢失的装饰层。
    /// </summary>
    private static bool RequiresAccurateAlphaCapture(EntityDef entityDef)
    {
        if (entityDef.HasRuneCost || entityDef.HasDeckAction() || entityDef.IsElite())
            return true;

        var classes = new List<TAG_CLASS>();
        entityDef.GetClasses(classes);
        return classes.Count > 2;
    }
}

// ============================================================================
// 格式模式导出目标
//
// 文件说明：
//   定义狂野、标准、休闲三个格式模式图片的导出顺序和文件名。
//   顺序必须与游戏格式选择弹窗的左、中、右三列保持一致。
// ============================================================================

/// <summary>
/// 格式模式图片导出类型。
/// </summary>
internal enum FormatModeExportKind
{
    Wild,
    Standard,
    Casual
}

/// <summary>
/// 单个格式模式图片的导出目标。
/// </summary>
internal sealed class FormatModeExportTarget
{
    private FormatModeExportTarget(FormatModeExportKind kind, string fileName)
    {
        Kind = kind;
        FileName = fileName;
    }

    /// <summary>
    /// 格式模式类型。
    /// </summary>
    public FormatModeExportKind Kind { get; }

    /// <summary>
    /// 输出文件名，不包含扩展名。
    /// </summary>
    public string FileName { get; }

    /// <summary>
    /// 创建默认的三模式导出目标。
    /// </summary>
    internal static List<FormatModeExportTarget> CreateDefaults()
    {
        return new List<FormatModeExportTarget>
        {
            new FormatModeExportTarget(FormatModeExportKind.Wild, "wild"),
            new FormatModeExportTarget(FormatModeExportKind.Standard, "standard"),
            new FormatModeExportTarget(FormatModeExportKind.Casual, "casual")
        };
    }
}

// ============================================================================
// 导图渲染策略
//
// 文件说明：
//   将不同卡牌类型的 actor 选择、初始化方式和相机参数收口到统一策略对象。
//   后续若某一类卡牌样式异常，只需修改对应分支。
// ============================================================================

/// <summary>
/// 导图渲染策略分类。
/// </summary>
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

/// <summary>
/// 单类卡牌的导图渲染策略。
/// </summary>
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

    /// <summary>
    /// 是否使用黑白底双通道重建透明度。
    /// </summary>
    public bool UseDualBackgroundAlphaCapture { get; }

    /// <summary>
    /// 是否在 Show 之前强制激活 actor 根对象。
    /// </summary>
    public bool ActivateRootObjectBeforeShow { get; }

    /// <summary>
    /// 是否按展示链路改为 Show 之后再刷新组件。
    /// </summary>
    public bool UpdateComponentsAfterShow { get; }

    /// <summary>
    /// 调用 UpdateAllComponents 时传入的 ignoreSpells 参数。
    /// </summary>
    public bool UpdateAllComponentsIgnoreSpells { get; }

    public float OrthographicSizeMultiplier { get; }

    public float DistanceMultiplier { get; }

    public Vector3 CameraOffset { get; }
}
