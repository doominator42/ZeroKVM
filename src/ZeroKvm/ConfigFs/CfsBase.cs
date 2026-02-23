using System.Globalization;
using System.IO;
using System.Numerics;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace ZeroKvm.ConfigFs;

internal abstract class CfsBase
{
    protected CfsBase(string basePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(basePath);
        if (!Path.IsPathRooted(basePath))
        {
            throw new ArgumentException("Absolute path expected", nameof(basePath));
        }

        CfsBasePath = basePath.TrimEnd(Path.DirectorySeparatorChar);
    }

    protected string CfsBasePath { get; }

    public bool IsCreated => Directory.Exists(CfsBasePath);

    public virtual void Create()
    {
        Logger.LogDebug(static path => $"Creating ConfigFS: {path}", CfsBasePath);
        if (!Directory.Exists(Path.GetDirectoryName(CfsBasePath)))
        {
            throw new InvalidOperationException("The parent has not been created");
        }

        Directory.CreateDirectory(CfsBasePath);
    }

    public void EnsureCreated()
    {
        if (IsCreated)
        {
            Logger.LogDebug(static path => $"ConfigFS already created: {path}", CfsBasePath);
        }
        else
        {
            Create();
        }
    }

    public virtual void Delete()
    {
        Logger.LogDebug(static path => $"Deleting ConfigFS: {path}", CfsBasePath);
        Directory.Delete(CfsBasePath);
    }

    public void EnsureDeleted()
    {
        if (IsCreated)
        {
            Delete();
        }
        else
        {
            Logger.LogDebug(static path => $"ConfigFS already deleted: {path}", CfsBasePath);
        }
    }

    protected T ReadUIntHexAttr<T>(string fileName)
        where T : unmanaged, IBinaryInteger<T>
    {
        using SafeFileHandle handle = File.OpenHandle(Path.Combine(CfsBasePath, fileName), FileMode.Open, FileAccess.Read);
        Span<byte> valueBytes = stackalloc byte[32];
        int byteLength = RandomAccess.Read(handle, valueBytes, 0);
        return byteLength == 0 || valueBytes[0] == '\n' ?
            T.Zero :
            T.Parse(NormalizeBytes(valueBytes.Slice(0, byteLength)), NumberStyles.AllowHexSpecifier, null);

        static ReadOnlySpan<byte> NormalizeBytes(ReadOnlySpan<byte> bytes)
        {
            if (bytes.StartsWith("0x"u8))
            {
                bytes = bytes.Slice(2);
            }

            return bytes.TrimEnd((byte)'\n');
        }
    }

    protected void WriteUIntHexAttr<T>(string fileName, T value)
        where T : unmanaged, IBinaryInteger<T>
    {
        Span<byte> valueBytes = stackalloc byte[32];
        valueBytes[0] = (byte)'0';
        valueBytes[1] = (byte)'x';
        value.TryFormat(valueBytes.Slice(2), out int byteLength, ['x', (char)((value.GetByteCount() * 2) + '0')], null);
        valueBytes[byteLength + 2] = (byte)'\n';

        File.WriteAllBytes(Path.Combine(CfsBasePath, fileName), valueBytes.Slice(0, byteLength + 3));
    }

    protected T ReadIntAttr<T>(string fileName)
        where T : unmanaged, IBinaryInteger<T>
    {
        using SafeFileHandle handle = File.OpenHandle(Path.Combine(CfsBasePath, fileName), FileMode.Open, FileAccess.Read);
        Span<byte> valueBytes = stackalloc byte[32];
        int byteLength = RandomAccess.Read(handle, valueBytes, 0);
        return byteLength == 0 || valueBytes[0] == '\n' ?
            T.Zero :
            T.Parse(valueBytes.Slice(0, byteLength).TrimEnd((byte)'\n'), NumberStyles.None, null);
    }

    protected void WriteIntAttr<T>(string fileName, T value)
        where T : unmanaged, IBinaryInteger<T>
    {
        Span<byte> valueBytes = stackalloc byte[32];
        value.TryFormat(valueBytes, out int byteLength, default, null);
        valueBytes[byteLength] = (byte)'\n';

        File.WriteAllBytes(Path.Combine(CfsBasePath, fileName), valueBytes.Slice(0, byteLength + 1));
    }

    protected ReadOnlySpan<char> ReadSingleLineAttr(string fileName)
    {
        return File.ReadAllText(Path.Combine(CfsBasePath, fileName), Encoding.UTF8).AsSpan().TrimEnd('\n');
    }

    protected void WriteSingleLineAttr(string fileName, ReadOnlySpan<char> value)
    {
        value = value.TrimEnd('\n');
        string path = Path.Combine(CfsBasePath, fileName);
        if (value.Length == 0)
        {
            File.WriteAllBytes(path, [(byte)'\n']);
        }
        else
        {
            int byteLength = Encoding.UTF8.GetMaxByteCount(value.Length + 1);
            Span<byte> valueBytes = byteLength > 512 ? new byte[byteLength] : stackalloc byte[value.Length + 1];
            byteLength = Encoding.UTF8.GetBytes(value, valueBytes);
            valueBytes[byteLength] = (byte)'\n';
            File.WriteAllBytes(path, valueBytes.Slice(0, byteLength + 1));
        }
    }

    protected (uint, uint) ReadDevMajorMinor(string fileName)
    {
        using SafeFileHandle handle = File.OpenHandle(Path.Combine(CfsBasePath, fileName), FileMode.Open, FileAccess.Read);
        Span<byte> valueBytes = stackalloc byte[32];
        int byteLength = RandomAccess.Read(handle, valueBytes, 0);
        valueBytes = valueBytes.Slice(0, byteLength).TrimEnd((byte)'\n');
        int splitIndex = valueBytes.IndexOf((byte)':');
        if (splitIndex <= 0)
        {
            return (0, 0);
        }

        return (
            uint.Parse(valueBytes.Slice(0, splitIndex), NumberStyles.None, null),
            uint.Parse(valueBytes.Slice(splitIndex + 1), NumberStyles.None, null));
    }

    protected byte[] ReadBytesAttr(string fileName)
    {
        return File.ReadAllBytes(Path.Combine(CfsBasePath, fileName));
    }

    protected void WriteBytesAttr(string fileName, ReadOnlySpan<byte> value)
    {
        File.WriteAllBytes(Path.Combine(CfsBasePath, fileName), value);
    }

    protected static ushort ParseLangCode(ReadOnlySpan<char> str)
    {
        if (str.StartsWith("0x"))
        {
            str = str.Slice(2);
        }

        return ushort.Parse(str, NumberStyles.AllowHexSpecifier, null);
    }
}
