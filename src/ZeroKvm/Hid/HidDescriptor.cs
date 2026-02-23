using System.Diagnostics.CodeAnalysis;

namespace ZeroKvm.Hid;

internal class HidDescriptor
{
    public HidDescriptor(ReadOnlySpan<byte> descriptorBytes, ReadOnlySpan<Type> reportTypes, int maximumReportLength)
    {
        _bytes = descriptorBytes.ToArray();
        _reportTypes = reportTypes.ToArray();
        MaximumReportLength = maximumReportLength;
    }

    private readonly byte[] _bytes;
    private readonly Type[] _reportTypes;

    public ReadOnlyMemory<byte> Bytes => _bytes;

    public int MaximumReportLength { get; }

    public byte GetReportId(Type reportType)
    {
        Type[] types = _reportTypes;
        for (int i = 0; i < types.Length; i++)
        {
            if (types[i] == reportType)
            {
                return (byte)(i + 1);
            }
        }

        ThrowNotFound();
        return 0;

        [DoesNotReturn]
        static void ThrowNotFound()
        {
            throw new ArgumentException("Report type not found", nameof(reportType));
        }
    }
}
