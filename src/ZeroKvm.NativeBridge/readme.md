# ZeroKVM as a library

## Data flow

The library processes raw DisplayLink USB bulk transfer packets and maintains an internal framebuffer. Call `Process` each time you receive a USB packet; call `CopyFrameBufferTo` when you want a rendered frame. The library handles the decode and diffing internally — only pixels that changed since the last copy are written to your output buffer.

# C shared library

Build with:

```
dotnet publish src/ZeroKvm.NativeBridge/ZeroKvm.NativeBridge.csproj -r linux-x64 -c Release
```

This produces `ZeroKvm.NativeBridge.so`. Link with `-l:ZeroKvm.NativeBridge.so` and `#include "zerokvm_udl.h"` (or declare the symbols manually).

Here's the API:
* `zerokvm_udl_create(maxWidth, maxHeight)` — allocate a context; returns an opaque handle or 0 on failure
* `zerokvm_udl_destroy(handle)` — free it
* `zerokvm_udl_process(handle, data, length)` — feed a USB packet; returns bytes consumed or -1
* `zerokvm_udl_copy_framebuffers(handle, rgb565, stride, xrgb8888, stride, area)` — copy out the rendered frame into your XRGB8888 or RGB565 buffer; either destination can be null to skip that copy. Returns 0 or -1. Fills an optional NativeFrameArea struct with the resolution and the bounding box of pixels changed since the last call.
* `zerokvm_udl_get_color_depth, _get_hpixels, _get_vpixels` — query current display mode
* `zerokvm_udl_get_base16bpp, zerokvm_udl_get_base8bpp` — raw framebuffer offsets (for direct access to internal memory, advanced use)
* `zerokvm_udl_copy_registers(handle, buf, 256)` — copy the 256-byte DisplayLink register block


# C# project reference

Add a `<ProjectReference>` to `ZeroKvm.csproj`. The primary types are `DlMemory` (holds all state), `DlDecoder.Process(ReadOnlySpan<byte>, DlMemory)`, and `memory.CopyFrameBufferTo(Span<uint> xrgb8888)`. The `FrameArea` returned by `CopyFrameBufferTo` gives the resolution and dirty rectangle, same as the C API.