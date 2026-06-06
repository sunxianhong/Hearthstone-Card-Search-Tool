using System.Text.Encodings.Web;
using System.Text.Json;

namespace HearthstoneCardSearchTool.Web;

public sealed record AppearanceSettingsConfig(
    string? BackgroundImageFileName,
    string? BackgroundName,
    bool BackgroundBlur,
    bool GlassUi);

public sealed class AppearanceSettingsStore
{
    private const string ConfigDirectoryName = "config";
    private const string ConfigFileName = "appearance-settings.json";
    private const string ImageDirectoryName = "appearance";
    private const string BackgroundImageBaseName = "background";
    private const int MaxBackgroundImageBytes = 8 * 1024 * 1024;

    private static readonly IReadOnlyDictionary<string, string> ImageExtensions =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["image/jpeg"] = ".jpg",
            ["image/png"] = ".png",
            ["image/webp"] = ".webp",
            ["image/gif"] = ".gif",
        };

    private readonly string _configDirectory;
    private readonly string _configPath;
    private readonly string _imageDirectory;
    private readonly SemaphoreSlim _sync = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public AppearanceSettingsStore(IConfiguration configuration, IWebHostEnvironment environment)
    {
        var configuredRoot = configuration["APPEARANCE_CONFIG_ROOT"]
            ?? configuration["AppearanceConfigRoot"]
            ?? configuration["FILTER_BAR_CONFIG_ROOT"]
            ?? configuration["FilterBarConfigRoot"];

        _configDirectory = ResolveConfigDirectory(configuredRoot, environment);
        _configPath = Path.Combine(_configDirectory, ConfigFileName);
        _imageDirectory = Path.Combine(_configDirectory, ImageDirectoryName);
    }

    public async Task<AppearanceSettingsConfig> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _sync.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(_configDirectory);
            Directory.CreateDirectory(_imageDirectory);

            if (!File.Exists(_configPath))
            {
                return Normalize(null);
            }

            AppearanceSettingsConfig? existing = null;
            try
            {
                await using var stream = File.Open(_configPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                existing = await JsonSerializer.DeserializeAsync<AppearanceSettingsConfig>(
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

    public async Task<AppearanceSettingsConfig> SaveAsync(
        AppearanceSettingsSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        await _sync.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(_configDirectory);
            Directory.CreateDirectory(_imageDirectory);

            var existing = await LoadCoreAsync(cancellationToken);
            var backgroundFileName = existing.BackgroundImageFileName;
            var backgroundName = existing.BackgroundName;

            if (request.ClearBackgroundImage)
            {
                DeleteBackgroundImages();
                backgroundFileName = null;
                backgroundName = null;
            }
            else if (!string.IsNullOrWhiteSpace(request.BackgroundImageDataUrl))
            {
                var image = DecodeDataUrlImage(request.BackgroundImageDataUrl);
                DeleteBackgroundImages();

                backgroundFileName = $"{BackgroundImageBaseName}{image.Extension}";
                var targetPath = Path.Combine(_imageDirectory, backgroundFileName);
                var tempPath = $"{targetPath}.{Guid.NewGuid():N}.tmp";
                await File.WriteAllBytesAsync(tempPath, image.Bytes, cancellationToken);
                File.Move(tempPath, targetPath, overwrite: true);
                backgroundName = string.IsNullOrWhiteSpace(request.BackgroundName)
                    ? backgroundFileName
                    : request.BackgroundName.Trim();
            }
            else if (backgroundFileName is not null)
            {
                backgroundName = string.IsNullOrWhiteSpace(request.BackgroundName)
                    ? backgroundFileName
                    : request.BackgroundName.Trim();
            }

            var saved = Normalize(new AppearanceSettingsConfig(
                backgroundFileName,
                backgroundName,
                request.BackgroundBlur,
                request.GlassUi));

            await SaveCoreAsync(saved, cancellationToken);
            return saved;
        }
        finally
        {
            _sync.Release();
        }
    }

    public string? GetBackgroundImagePath(AppearanceSettingsConfig config)
    {
        var fileName = Path.GetFileName(config.BackgroundImageFileName);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        var path = Path.GetFullPath(Path.Combine(_imageDirectory, fileName));
        var root = Path.GetFullPath(_imageDirectory);
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
        {
            return null;
        }

        return path;
    }

    private async Task<AppearanceSettingsConfig> LoadCoreAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_configPath))
        {
            return Normalize(null);
        }

        try
        {
            await using var stream = File.Open(_configPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return Normalize(await JsonSerializer.DeserializeAsync<AppearanceSettingsConfig>(
                stream,
                _jsonOptions,
                cancellationToken));
        }
        catch (IOException)
        {
            return Normalize(null);
        }
        catch (JsonException)
        {
            return Normalize(null);
        }
    }

    private async Task SaveCoreAsync(AppearanceSettingsConfig config, CancellationToken cancellationToken)
    {
        var tempPath = $"{_configPath}.{Guid.NewGuid():N}.tmp";
        await using var stream = File.Create(tempPath);
        await JsonSerializer.SerializeAsync(stream, config, _jsonOptions, cancellationToken);
        await stream.FlushAsync(cancellationToken);
        stream.Close();

        File.Move(tempPath, _configPath, overwrite: true);
    }

    private void DeleteBackgroundImages()
    {
        foreach (var path in Directory.EnumerateFiles(_imageDirectory, $"{BackgroundImageBaseName}.*"))
        {
            File.Delete(path);
        }
    }

    private static DecodedImage DecodeDataUrlImage(string dataUrl)
    {
        var markerIndex = dataUrl.IndexOf(";base64,", StringComparison.OrdinalIgnoreCase);
        if (!dataUrl.StartsWith("data:", StringComparison.OrdinalIgnoreCase) || markerIndex <= "data:".Length)
        {
            throw new InvalidOperationException("背景图片数据格式无效。");
        }

        var contentType = dataUrl["data:".Length..markerIndex].Trim();
        if (!ImageExtensions.TryGetValue(contentType, out var extension))
        {
            throw new InvalidOperationException("背景图片仅支持 JPG、PNG、WEBP 或 GIF。");
        }

        var base64 = dataUrl[(markerIndex + ";base64,".Length)..];
        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(base64);
        }
        catch (FormatException)
        {
            throw new InvalidOperationException("背景图片数据格式无效。");
        }

        if (bytes.Length == 0 || bytes.Length > MaxBackgroundImageBytes)
        {
            throw new InvalidOperationException("背景图片太大，请换一张更小的图片。");
        }

        return new DecodedImage(bytes, extension);
    }

    private static AppearanceSettingsConfig Normalize(AppearanceSettingsConfig? current)
    {
        if (current is null)
        {
            return new AppearanceSettingsConfig(null, null, BackgroundBlur: false, GlassUi: true);
        }

        return new AppearanceSettingsConfig(
            string.IsNullOrWhiteSpace(current.BackgroundImageFileName)
                ? null
                : Path.GetFileName(current.BackgroundImageFileName),
            string.IsNullOrWhiteSpace(current.BackgroundName)
                ? null
                : current.BackgroundName.Trim(),
            current.BackgroundBlur,
            current.GlassUi);
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

    private sealed record DecodedImage(byte[] Bytes, string Extension);
}
