using System.Runtime.InteropServices;
using System.Text;

namespace ZeroKvm;

internal static unsafe partial class DevUtils
{
    public static string? FindDevicePath((uint Major, uint Minor) device)
    {
        const int ENOENT = 2;
        const int PathMaxLength = 64;

        byte* pathBytes = stackalloc byte[PathMaxLength];
        int result = find_dev_path_from_major_minor(new MajorMinor(device.Major, device.Minor), pathBytes, PathMaxLength);
        if (result == -ENOENT)
        {
            return null;
        }
        else if (result != 0)
        {
            throw new Exception(nameof(FindDevicePath) + " error: " + result);
        }

        return new((sbyte*)pathBytes);
    }

    public static void MountFfs(ReadOnlySpan<char> functionName)
    {
        byte* functionNameBytes = stackalloc byte[functionName.Length + 1];
        int functionNameByteLength = Encoding.ASCII.GetBytes(functionName, new(functionNameBytes, functionName.Length));
        if (functionNameByteLength != functionName.Length)
        {
            throw new ArgumentException("Invalid function name", nameof(functionName));
        }

        functionNameBytes[functionNameByteLength] = 0;
        int result = mount_ffs(functionNameBytes);
        if (result != 0)
        {
            throw new Exception("Mount FunctionFS error: " + result);
        }
    }

    [LibraryImport("devutils-wrapper")]
    private static partial int find_dev_path_from_major_minor(MajorMinor dev, byte* path, int path_size);

    [LibraryImport("devutils-wrapper")]
    private static partial int mount_ffs(byte* func_name);

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct MajorMinor
    {
        public MajorMinor(uint major, uint minor)
        {
            Major = major;
            Minor = minor;
        }

        public readonly uint Major;
        public readonly uint Minor;
    }
}
