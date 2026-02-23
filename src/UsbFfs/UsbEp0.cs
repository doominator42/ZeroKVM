using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace UsbFfs;

public sealed class UsbEp0 : IDisposable
{
    internal UsbEp0(string gadgetPath)
    {
        FileHandle = File.OpenHandle(
            path: Path.Combine(gadgetPath, "ep0"),
            mode: FileMode.Open,
            access: FileAccess.ReadWrite);
    }

    private SafeFileHandle FileHandle { get; }

    public void ReadExactly(Span<byte> buffer)
    {
        Read(buffer, buffer.Length);
    }

    private int Read(Span<byte> buffer, int minimum)
    {
        const int EIDRM = 43;

        int readCount = 0;
        do
        {
            nint result = LibC.Read(FileHandle, buffer);
            switch (result)
            {
                case -EIDRM:
                    continue;

                case < 0:
                    ThrowReadError(result);
                    return 0;

                case 0:
                    if (buffer.Length > 0)
                    {
                        ThrowEndOfStream();
                    }

                    return readCount;

                default:
                    buffer = buffer.Slice((int)result);
                    readCount += (int)result;
                    break;
            }
        }
        while (buffer.Length > 0 && readCount < minimum);

        return readCount;

        [DoesNotReturn]
        static void ThrowReadError(nint result)
        {
            throw new ErrnoIOException((int)result, "Read error: ");
        }

        [DoesNotReturn]
        static void ThrowEndOfStream()
        {
            throw new EndOfStreamException();
        }
    }

    internal int ReadEvents(Span<FfsEvent> buffer)
    {
        return Read(MemoryMarshal.AsBytes(buffer), 0) / Unsafe.SizeOf<FfsEvent>();
    }

    public void Write(ReadOnlySpan<byte> buffer)
    {
        UsbInEp.Write(FileHandle, buffer);
    }

    internal void WriteDescriptors(
        ReadOnlySpan<UsbDescriptor> fullSpeedDescriptors,
        ReadOnlySpan<UsbDescriptor> highSpeedDescriptors,
        ReadOnlySpan<UsbDescriptor> superSpeedDescriptors,
        FfsDescsFlags otherFlags)
    {
        if (fullSpeedDescriptors.Length + highSpeedDescriptors.Length + superSpeedDescriptors.Length == 0)
        {
            throw new InvalidOperationException("No descriptors were provided");
        }

        ArrayBufferWriter<byte> buffer = new(64);
        buffer.WriteAsBytes(new FfsDescsHead()
        {
            Magic = FfsMagic.DescriptorsMagic,
            Length = (uint)Marshal.SizeOf<FfsDescsHead>() +
                GetDescriptorsLength(fullSpeedDescriptors) +
                GetDescriptorsLength(highSpeedDescriptors) +
                GetDescriptorsLength(superSpeedDescriptors),
            Flags = otherFlags |
                (fullSpeedDescriptors.Length > 0 ? FfsDescsFlags.HasFullSpeedDesc : 0) |
                (highSpeedDescriptors.Length > 0 ? FfsDescsFlags.HasHighSpeedDesc : 0) |
                (superSpeedDescriptors.Length > 0 ? FfsDescsFlags.HasSuperSpeedDesc : 0),
        });

        if (fullSpeedDescriptors.Length > 0)
        {
            buffer.WriteAsBytes((uint)fullSpeedDescriptors.Length);
        }

        if (highSpeedDescriptors.Length > 0)
        {
            buffer.WriteAsBytes((uint)highSpeedDescriptors.Length);
        }

        if (superSpeedDescriptors.Length > 0)
        {
            buffer.WriteAsBytes((uint)superSpeedDescriptors.Length);
        }

        WriteDescriptors(buffer, fullSpeedDescriptors);
        WriteDescriptors(buffer, highSpeedDescriptors);
        WriteDescriptors(buffer, superSpeedDescriptors);

        Write(buffer.WrittenSpan);

        static uint GetDescriptorsLength(ReadOnlySpan<UsbDescriptor> descriptors)
        {
            if (descriptors.Length == 0)
            {
                return 0;
            }

            uint length = (uint)Marshal.SizeOf<uint>();
            foreach (UsbDescriptor descriptor in descriptors)
            {
                length += (uint)descriptor.RawBytes.Length;
            }

            return length;
        }

        static void WriteDescriptors(ArrayBufferWriter<byte> buffer, ReadOnlySpan<UsbDescriptor> descriptors)
        {
            foreach (UsbDescriptor descriptor in descriptors)
            {
                buffer.Write(descriptor.RawBytes.Span);
            }
        }
    }

    internal void WriteStrings(ushort langCode, ReadOnlySpan<string> strings)
    {
        if (strings.Length == 0)
        {
            throw new InvalidOperationException("No strings were provided");
        }

        ArrayBufferWriter<byte> buffer = new(64);
        buffer.WriteAsBytes(new FfsStringsHead()
        {
            Magic = FfsMagic.StringsMagic,
            Length = (uint)Marshal.SizeOf<FfsStringsHead>() + (uint)Marshal.SizeOf<ushort>() + GetStringsLength(strings),
            LangCount = 1,
            StrCount = (uint)strings.Length,
        });

        buffer.WriteAsBytes(langCode);
        foreach (string str in strings)
        {
            int length = Encoding.UTF8.GetByteCount(str);
            Span<byte> bufferSpan = buffer.GetSpan(length + 1);
            Encoding.UTF8.GetBytes(str, bufferSpan);
            bufferSpan[length] = 0;
            buffer.Advance(length + 1);
        }

        Write(buffer.WrittenSpan);

        static uint GetStringsLength(ReadOnlySpan<string> strs)
        {
            uint length = 0;
            foreach (string str in strs)
            {
                length += (uint)Encoding.UTF8.GetByteCount(str) + 1;
            }

            return length;
        }
    }

    public void Dispose()
    {
        FileHandle.Dispose();
    }
}
