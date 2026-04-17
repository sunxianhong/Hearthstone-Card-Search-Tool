namespace HearthstoneCardSearchTool.Core;

public static class ResourceLocator
{
    public static string LocateResourceRoot(params string?[] startingPaths)
    {
        foreach (var path in startingPaths.Where(static item => !string.IsNullOrWhiteSpace(item)))
        {
            foreach (var candidate in EnumerateSelfAndParents(path!))
            {
                if (HasCardResources(candidate))
                {
                    return candidate;
                }
            }
        }

        foreach (var candidate in EnumerateSelfAndParents(AppContext.BaseDirectory))
        {
            if (HasCardResources(candidate))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException("未找到 CardDefs.xml 和 cardpng 资源目录。");
    }

    private static IEnumerable<string> EnumerateSelfAndParents(string path)
    {
        DirectoryInfo? current = Directory.Exists(path)
            ? new DirectoryInfo(path)
            : new FileInfo(path).Directory;

        while (current is not null)
        {
            yield return current.FullName;
            current = current.Parent;
        }
    }

    private static bool HasCardResources(string directory)
    {
        return File.Exists(Path.Combine(directory, "CardDefs.xml"))
            && Directory.Exists(Path.Combine(directory, "cardpng"));
    }
}
