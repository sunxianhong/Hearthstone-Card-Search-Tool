using System.Text.Json;

namespace HearthstoneCardSearchTool.Web;

public sealed record FilterBarOptionConfig(
    string Value,
    string Label,
    bool Visible,
    bool? VisibleInWild = null,
    bool? VisibleInStandard = null);

public sealed record FilterBarSectionConfig(
    string Key,
    string Label,
    bool Enabled,
    IReadOnlyList<FilterBarOptionConfig> Options);

public sealed record FilterBarConfig(IReadOnlyList<FilterBarSectionConfig> Sections);

public sealed class FilterBarConfigStore
{
    private const string ConfigDirectoryName = "config";
    private const string ConfigFileName = "filter-bar-config.json";

    private readonly string _configDirectory;
    private readonly string _configPath;
    private readonly SemaphoreSlim _sync = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public FilterBarConfigStore(IConfiguration configuration, IWebHostEnvironment environment)
    {
        var configuredRoot = configuration["FILTER_BAR_CONFIG_ROOT"]
            ?? configuration["FilterBarConfigRoot"];

        _configDirectory = ResolveConfigDirectory(configuredRoot, environment);

        _configPath = Path.Combine(_configDirectory, ConfigFileName);
    }

    public string ConfigDirectory => _configDirectory;

    public string ConfigPath => _configPath;

    private static string ResolveConfigDirectory(string? configuredRoot, IWebHostEnvironment environment)
    {
        if (!string.IsNullOrWhiteSpace(configuredRoot))
        {
            return Path.IsPathRooted(configuredRoot)
                ? configuredRoot
                : Path.GetFullPath(Path.Combine(environment.ContentRootPath, configuredRoot));
        }

        // In published Docker images the content root is typically /app, which is often read-only
        // for the running user. Prefer the mounted /config volume when available.
        if (OperatingSystem.IsLinux() && string.Equals(environment.ContentRootPath, "/app", StringComparison.Ordinal))
        {
            return "/config";
        }

        return Path.Combine(environment.ContentRootPath, ConfigDirectoryName);
    }

    public async Task<FilterBarConfig> LoadAsync(
        Func<FilterBarConfig> defaultFactory,
        CancellationToken cancellationToken = default)
    {
        await _sync.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(_configDirectory);

            var defaults = defaultFactory();
            if (!File.Exists(_configPath))
            {
                await SaveCoreAsync(defaults, cancellationToken);
                return defaults;
            }

            FilterBarConfig? existing = null;
            try
            {
                await using var stream = File.Open(_configPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                existing = await JsonSerializer.DeserializeAsync<FilterBarConfig>(
                    stream,
                    _jsonOptions,
                    cancellationToken);
            }
            catch (IOException)
            {
                existing = null;
            }
            catch (JsonException)
            {
                existing = null;
            }

            var normalized = Normalize(existing, defaults);
            await SaveCoreAsync(normalized, cancellationToken);
            return normalized;
        }
        finally
        {
            _sync.Release();
        }
    }

    public async Task<FilterBarConfig> SaveAsync(
        FilterBarConfig config,
        Func<FilterBarConfig> defaultFactory,
        CancellationToken cancellationToken = default)
    {
        await _sync.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(_configDirectory);

            var normalized = Normalize(config, defaultFactory());
            await SaveCoreAsync(normalized, cancellationToken);
            return normalized;
        }
        finally
        {
            _sync.Release();
        }
    }

    private async Task SaveCoreAsync(FilterBarConfig config, CancellationToken cancellationToken)
    {
        var tempPath = $"{_configPath}.{Guid.NewGuid():N}.tmp";
        await using var stream = File.Create(tempPath);
        await JsonSerializer.SerializeAsync(stream, config, _jsonOptions, cancellationToken);
        await stream.FlushAsync(cancellationToken);
        stream.Close();

        File.Move(tempPath, _configPath, overwrite: true);
    }

    private static FilterBarConfig Normalize(FilterBarConfig? current, FilterBarConfig defaults)
    {
        if (current is null)
        {
            return defaults;
        }

        var currentSections = current.Sections.ToDictionary(
            static section => section.Key,
            StringComparer.OrdinalIgnoreCase);

        var normalizedSections = defaults.Sections
            .Select(defaultSection =>
            {
                if (!currentSections.TryGetValue(defaultSection.Key, out var currentSection))
                {
                    return defaultSection;
                }

                var currentOptions = currentSection.Options.ToDictionary(
                    static option => option.Value,
                    StringComparer.OrdinalIgnoreCase);

                var normalizedOptions = defaultSection.Options
                    .Select(defaultOption =>
                    {
                        if (!currentOptions.TryGetValue(defaultOption.Value, out var currentOption))
                        {
                            return defaultOption;
                        }

                        var visibleInWild = currentSection.Key.Equals("set", StringComparison.OrdinalIgnoreCase)
                            ? currentOption.VisibleInWild ?? currentOption.Visible
                            : currentOption.VisibleInWild ?? defaultOption.VisibleInWild;

                        var visibleInStandard = currentSection.Key.Equals("set", StringComparison.OrdinalIgnoreCase)
                            ? currentOption.VisibleInStandard ?? currentOption.Visible
                            : currentOption.VisibleInStandard ?? defaultOption.VisibleInStandard;

                        return defaultOption with
                        {
                            Visible = currentOption.Visible,
                            VisibleInWild = visibleInWild,
                            VisibleInStandard = visibleInStandard,
                        };
                    })
                    .ToList();

                return defaultSection with
                {
                    Enabled = currentSection.Enabled,
                    Options = normalizedOptions,
                };
            })
            .ToList();

        return new FilterBarConfig(normalizedSections);
    }
}
