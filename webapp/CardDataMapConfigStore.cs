using System.Text.Json;
using HearthstoneCardSearchTool.Core;

namespace HearthstoneCardSearchTool.Web;

public sealed class CardDataMapConfigStore
{
    private const string ConfigDirectoryName = "config";
    private const string ConfigFileName = "card-data-map-overrides.json";

    private readonly string _configDirectory;
    private readonly string _configPath;
    private readonly SemaphoreSlim _sync = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public CardDataMapConfigStore(IConfiguration configuration, IWebHostEnvironment environment)
    {
        var configuredRoot = configuration["CARD_DATA_MAP_CONFIG_ROOT"]
            ?? configuration["CardDataMapConfigRoot"]
            ?? configuration["FILTER_BAR_CONFIG_ROOT"]
            ?? configuration["FilterBarConfigRoot"];

        _configDirectory = ResolveConfigDirectory(configuredRoot, environment);
        _configPath = Path.Combine(_configDirectory, ConfigFileName);
    }

    public async Task<CardDataMapOverrideConfig> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _sync.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(_configDirectory);

            if (!File.Exists(_configPath))
            {
                var empty = Normalize(null);
                await SaveCoreAsync(empty, cancellationToken);
                return empty;
            }

            CardDataMapOverrideConfig? existing = null;
            try
            {
                await using var stream = File.Open(_configPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                existing = await JsonSerializer.DeserializeAsync<CardDataMapOverrideConfig>(
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

            var normalized = Normalize(existing);
            await SaveCoreAsync(normalized, cancellationToken);
            return normalized;
        }
        finally
        {
            _sync.Release();
        }
    }

    public async Task<CardDataMapOverrideConfig> SaveAsync(
        CardDataMapOverrideConfig config,
        CancellationToken cancellationToken = default)
    {
        await _sync.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(_configDirectory);

            var normalized = Normalize(config);
            await SaveCoreAsync(normalized, cancellationToken);
            return normalized;
        }
        finally
        {
            _sync.Release();
        }
    }

    private async Task SaveCoreAsync(CardDataMapOverrideConfig config, CancellationToken cancellationToken)
    {
        var tempPath = $"{_configPath}.{Guid.NewGuid():N}.tmp";
        await using var stream = File.Create(tempPath);
        await JsonSerializer.SerializeAsync(stream, config, _jsonOptions, cancellationToken);
        await stream.FlushAsync(cancellationToken);
        stream.Close();

        File.Move(tempPath, _configPath, overwrite: true);
    }

    private static CardDataMapOverrideConfig Normalize(CardDataMapOverrideConfig? current)
    {
        return new CardDataMapOverrideConfig
        {
            UnknownEnumMap = NormalizeMap(current?.UnknownEnumMap),
            TagLabels = NormalizeMap(current?.TagLabels),
            ClassMap = NormalizeMap(current?.ClassMap),
            RarityMap = NormalizeMap(current?.RarityMap),
            RaceMap = NormalizeMap(current?.RaceMap),
            SchoolMap = NormalizeMap(current?.SchoolMap),
            KeywordMap = NormalizeMap(current?.KeywordMap),
            SetMap = NormalizeMap(current?.SetMap),
        };
    }

    private static Dictionary<string, string> NormalizeMap(IReadOnlyDictionary<string, string>? source)
    {
        var normalized = new Dictionary<string, string>(StringComparer.Ordinal);
        if (source is null)
        {
            return normalized;
        }

        foreach (var pair in source)
        {
            var key = pair.Key.Trim();
            var value = pair.Value.Trim();
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            normalized[key] = value;
        }

        return normalized;
    }

    private static string ResolveConfigDirectory(string? configuredRoot, IWebHostEnvironment environment)
    {
        if (!string.IsNullOrWhiteSpace(configuredRoot))
        {
            return Path.IsPathRooted(configuredRoot)
                ? configuredRoot
                : Path.GetFullPath(Path.Combine(environment.ContentRootPath, configuredRoot));
        }

        if (OperatingSystem.IsLinux() && string.Equals(environment.ContentRootPath, "/app", StringComparison.Ordinal))
        {
            return "/config";
        }

        return Path.Combine(environment.ContentRootPath, ConfigDirectoryName);
    }
}
