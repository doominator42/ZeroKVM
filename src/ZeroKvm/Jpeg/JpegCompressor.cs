/*
License for libjpeg-turbo:

The Modified (3-clause) BSD License
===================================

Copyright (C) 2009-2026 D. R. Commander.  All Rights Reserved.
Copyright (C) 2015 Viktor Szathmáry.  All Rights Reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

- Redistributions of source code must retain the above copyright notice,
  this list of conditions and the following disclaimer.
- Redistributions in binary form must reproduce the above copyright notice,
  this list of conditions and the following disclaimer in the documentation
  and/or other materials provided with the distribution.
- Neither the name of the libjpeg-turbo Project nor the names of its
  contributors may be used to endorse or promote products derived from this
  software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS",
AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
ARE DISCLAIMED.  IN NO EVENT SHALL THE COPYRIGHT HOLDERS OR CONTRIBUTORS BE
LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
POSSIBILITY OF SUCH DAMAGE.
*/

using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.InteropServices;

namespace ZeroKvm.Jpeg;

internal unsafe partial class JpegCompressor : IDisposable
{
    public JpegCompressor()
    {
        _ptr = (nint)tjInitCompress();
    }

    private nint _ptr;

    public JpegSubsampling Subsampling { get; set; } = JpegSubsampling.S444;

    public int Quality { get; set; } = 75;

    public long GetJpegMaxByteCount(int width, int height)
    {
        return (long)tjBufSize(width, height, (int)Subsampling);
    }

    public int Compress(in JpegPixelBuffer source, Span<byte> output, int? quality = null)
    {
        source.Validate();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(output.Length);

        ulong outputSize = (uint)output.Length;
        int result;
        fixed (byte* pSource = &MemoryMarshal.GetReference(source.Buffer))
        fixed (byte* pOutput = &MemoryMarshal.GetReference(output))
        {
            result = tjCompress2(
                (void*)_ptr,
                pSource,
                source.Width,
                source.BytesPerLine,
                source.Height,
                (int)source.Format,
                &pOutput,
                &outputSize,
                (int)Subsampling,
                quality ?? Quality,
                (int)(JpegFlags.FastDct | JpegFlags.NoRealloc));
        }

        if (outputSize > (ulong)output.Length)
        {
            ThrowBufferOverflow();
        }

        if (result != 0)
        {
            ThrowError(result);
        }

        return (int)outputSize;

        [DoesNotReturn]
        static void ThrowError(int result)
        {
            throw new Exception($"JPEG compress error: {result}");
        }

        [DoesNotReturn]
        static void ThrowBufferOverflow()
        {
            throw new InternalBufferOverflowException();
        }
    }

    public void Dispose()
    {
        nint ptr = Interlocked.Exchange(ref _ptr, 0);
        if (ptr != 0)
        {
            tjDestroy((void*)ptr);
        }
    }

    private const string LibraryName = "libturbojpeg.so";

    [LibraryImport(LibraryName)]
    private static partial void* tjInitCompress();

    [LibraryImport(LibraryName)]
    private static partial int tjDestroy(void* handle);

    [LibraryImport(LibraryName)]
    private static partial int tjCompress2(
        void* handle,
        byte* srcBuf,
        int width,
        int pitch,
        int height,
        int pixelFormat,
        byte** jpegBuf,
        ulong* jpegSize,
        int jpegSubsamp,
        int jpegQual,
        int flags);

    [LibraryImport(LibraryName)]
    private static partial ulong tjBufSize(int width, int height, int jpegSubsamp);
}
