using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;

namespace ZeroKvm;

internal static partial class Wwwroot
{
    private static partial byte[] GetAssetsArchive();

    private static readonly Lazy<FrozenDictionary<string, Asset>> _assets = new(LoadAssets, LazyThreadSafetyMode.ExecutionAndPublication);

    private static readonly FrozenDictionary<string, string> _mediaTypes = new (string Extension, string MediaType)[]
    {
        ("html", "text/html"),
        ("css", "text/css"),
        ("js", "application/javascript"),
        ("json", "application/json"),
        ("svg", "image/svg+xml"),
    }.ToFrozenDictionary(m => m.Extension, m => m.MediaType, StringComparer.OrdinalIgnoreCase);

    public static bool TryGetAsset(ReadOnlySpan<char> path, [NotNullWhen(true)] out Asset? asset)
    {
        return _assets.Value.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(path.Trim('/'), out asset);
    }

    private static FrozenDictionary<string, Asset> LoadAssets()
    {
        List<KeyValuePair<string, Asset>> assetList = new();
        byte[] archive = GetAssetsArchive();
        using (MemoryStream stream = new(archive))
        using (GZipStream gzip = new(stream, CompressionMode.Decompress))
        using (TarReader tar = new(gzip))
        {
            TarEntry? entry;
            while ((entry = tar.GetNextEntry()) is not null)
            {
                if (entry.EntryType == TarEntryType.RegularFile)
                {
                    Asset asset = new(entry);
                    assetList.Add(new(asset.Path, asset));
                }
            }
        }

        return assetList.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    public class Asset
    {
        public Asset(TarEntry entry)
        {
            Path = entry.Name.Trim('/');
            MediaType = GetMediaType(entry.Name);
            ModifiedDate = entry.ModificationTime.UtcDateTime;
            if (entry.Length > 0)
            {
                byte[] content = new byte[entry.Length];
                (entry.DataStream ?? throw new Exception("Invalid entry")).ReadExactly(content);
                Content = content;
            }
        }

        public Asset(FileInfo file, ReadOnlyMemory<byte> content)
        {
            Path = file.Name;
            MediaType = GetMediaType(Path);
            ModifiedDate = file.LastWriteTimeUtc;
            Content = content;
        }

        public string Path { get; }

        public string MediaType { get; }

        public DateTime ModifiedDate { get; }

        public ReadOnlyMemory<byte> Content { get; }

        private static string GetMediaType(string name)
        {
            ReadOnlySpan<char> extension = System.IO.Path.GetExtension(name.AsSpan()).TrimStart('.');
            if (extension.Length > 0 && _mediaTypes.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(extension, out string? mediaType))
            {
                return mediaType;
            }
            else
            {
                throw new Exception("Unknown file type: " + name);
            }
        }
    }
}
