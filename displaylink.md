# Some notes about DisplayLink (DL-1x5 USB 2.0 devices)

## USB control requests

`bRequestType bRequest wValue wIndex wLength`

- `0xc0 0x02 offset 0x10a1 length+1`

  Reads `length` bytes of the EDID at `offset` (big-endian). The response has 1 byte for the status (0x00 for success) and the EDID bytes follows.

- `0xc0 0x02 offset<<8 0x00a1 2`

  Reads 1 byte of the EDID at `offset`. The response has 1 byte for the status (0x00 for success) and the EDID byte follows.

- `0x40 0x03 0x0000 address length`

  Writes `length` bytes of RAM at `address`.

- `0x40 0x03 param1&0xff param2&0xff 0`

  Unknown, maybe some memory clear?

- `0xc0 0x04 0x0000 address length`

  Reads `length` bytes of RAM at `address`.

- `0xc0 0x05 0x0000 0x0002 4`

  Seems to be a verification of the 0x5f descriptor. The reply is a 32 bits checksum of the descriptor, skipping the first 2 bytes (bLength and bDescriptorType).

- `0xc0 0x06 0x0000 0x0000 4`

  Gets the status/capabilities of the device. The response is a uint32 (little-endian).

  |Bits|Value|Meaning|
  |-|-|-|
  |0|Reconfigure||
  ||0|No|
  ||1|Yes, descriptor 0x5f is required|
  |7|ProtocolErrors|
  ||0|Yes|
  ||1|No|
  |8-15|uint8|CfgRetries for Micro chip type, FifoStatus for FPGA chip type|
  |16-18|Blanking||
  ||000|None|
  ||001|Black|
  ||011|Suspend|
  ||101|Standby|
  ||110|TempPowerOff|
  ||111|PowerOff|
  |24|Color depths|Meaning of values in register 0x00|
  ||0|0x00: 16 bits, 0x01: 24 bits, 0x0a: unknown, 0x0b: unknown|
  ||1|0x08: 16 bits, 0x09: 24 bits, 0x0c: unknown, 0x0d: unknown|
  |28-29|Boot type||
  ||00|Micro|
  ||11|Standalone|
  |30-31|Chip type||
  ||01|Micro|
  ||10|FPGA|
  ||11|Asic|

- `0x40 0x07 0x0000 offset 0`
- `0x40 0x07 0x007f 0x0005 0`
- `0x40 0x07 0x007f 0x0000 0`
- `0xc0 0x08 0x0000 offset 1`
- `0x40 0x0e 0x0005 0x0005 0`
- `0x40 0x0e 0x000b 0x0005 0`
- `0x40 0x10 0x0001 0x0000 0`
- `0x40 0x11 0x0000 0x0000 0`

- `0x40 0x12 0x0000 0x0000 16`

  Sets the 128 bits encryption key.

- `0xc0 0x13 uint32_param&0xffff uint32_param>>16 4`
- `0x40 0x14 0x0000 0x0000 0`
- `0x40 0x15 0x0000 0x0381 0`
- `0x40 0x15 0x0000 0x0881 0`
- `0x40 0x15 0x0000 0x4000 0`

## DisplayLink vendor descriptor (bDescritorType = 0x5f)

This optional descriptor specifies the parameters of the device and what protocols are supported.

### General format
|Offset|Size|Field name|Value|
|-|-|-|-|
|0|1|bLength|total length|
|1|1|bDescriptorType|0x5f|
|2|2|version|0x0001 (LE)|
|4|1|dataLength|total length - 2|
|Data piece|
|n+5|2|type|piece type|
|n+7|1|length|piece data length|
|n+8|length|data|variable data|

### Types
|Type (LE)|Name|Data|
|-|-|-|
|0x0000|RenderingProtocolVersion|int16_le|
|0x0001|Raw mode|uint8|
|0x0002|RL mode|uint8|
|0x0003|RawRL mode|uint8|
|0x0004|RLX mode|uint8|
|0x0005|DIFF mode|uint8|
|0x0006|DIFF padding|
|0x0007|RDIFF flags|
|0x0100|Ob type|
|0x0101|Ob method|
|0x0102|Ob taps|
|0x0103|Ob length|
|0x0104|Ob cycles|
|0x0200|MaxArea|uint32_le|
|0x0201|MaxWidth|uint32_le|
|0x0202|MaxHeight|uint32_le|
|0x0203|MaxPixelClock|
|0x0204|MinPixelClock|
|0x0300|VideoRAMStart|
|0x0301|VideoRAMEnd|
|0x0302|RAMBandwidth|
|0x0400|Chip ID|4 bytes|
|0x0401|ProtoEngineChannels|
|0x0402|Min SW version|
|0x0403|Min SW revision|
|0x0500|ModeRegisterSize|
|0x0501|BigEndianFrequency|
|0x0502|SyncPulsePolarity688|
|0x0600|Extended EDID|
|0x0601|AVI infoframe|

# Video stream

All commands starts with the byte `0xAF` followed by the command opcode and variable-length data.

### Set register
```ts
0xAF 0x20
address: uint8
value: uint8
```

### Write 8 bits pixels (array)
```ts
0xAF 0x60
offset: uint24_be
count: uint8_wrap256
pixels: uint8_rgb323[count]
```

### Fill 8 bits pixels range
```ts
0xAF 0x61
offset: uint24_be
total_count: uint8_wrap256
{
  count: uint8_wrap256
  pixel: uint8_rgb323
}[until total_count pixels is read]
```

### Copy 8 bits pixels range
```ts
0xAF 0x62
target_offset: uint24_be
count: uint8_wrap256
source_offset: uint24_be
```

### Write 8 bits pixels (run-length encoded)
```ts
0xAF 0x63
offset: uint24_be
total_count: uint8_wrap256
{
  count: uint8_wrap256
  pixel: uint8_rgb323[count]
  last_pixel_repeat_count: uint8 // (omitted for the last chunk if all pixels are rendered)
}[until total_count pixels is read]
```

### Write 16 bits pixels (array)
```ts
0xAF 0x68
offset: uint24_be
count: uint8_wrap256
pixels: uint16_rgb565[count]
```

### Fill 16 bits pixels range
```ts
0xAF 0x69
offset: uint24_be
total_count: uint8_wrap256
{
  count: uint8_wrap256
  pixel: uint16_rgb565
}[until total_count pixels is read]
```

### Copy 16 bits pixels range
```ts
0xAF 0x6A
target_offset: uint24_be
count: uint8_wrap256
source_offset: uint24_be
```

### Write 16 bits pixels (run-length encoded)
```ts
0xAF 0x6B
offset: uint24_be
total_count: uint8_wrap256
{
  count: uint8_wrap256
  pixel: uint16_rgb565be[count]
  last_pixel_repeat_count: uint8 // (omitted for the last chunk if all pixels are read)
}[until total_count pixels is read]
```

### Write 8 bits pixels (compressed)
```ts
0xAF 0x70
offset: uint24_be
total_count: uint8_wrap256
table_lookup: bit[until total_count pixels is read]
```

### Write 16 bits pixels (compressed)
```ts
0xAF 0x78
offset: uint24_be
total_count: uint8_wrap256
table_lookup: bit[until total_count pixels is read]
```

### Flush pipe
```ts
0xAF 0xA0
```

### No-op
```ts
0xAF 0xAF
```

### Load decompression table
```ts
0xAF 0xE0
header: 0x26 0x38 0x71 0xCD
padding: uint16
length: uint16_be
entries: {
  colorA: uint16_rgb565be
  repeatA: uint8
  unknownA: uint3_msb
  jumpA_msb: uint5_lsb
  jumpA_lsb: uint4_msb
  jumpB_lsb: uint4_lsb
  colorB: uint16_rgb565be
  repeatB: uint8
  unknownB: uint3_msb
  jumpB_msb: uint5_lsb
}[length]
```

# Memory

|Memory|Size|
|-|-|
|RAM|64 KiB|
|Registers|256 Bytes|
|Graphics|16 MiB|

## RAM regions

|Offset|Size|Content|
|-|-|-|
|0xC300|256|Registers|

## Registers

|Address|Type|Content|
|-|-|-|
|0x00|uint8|Color depth|
|0x01|uint16_lfsr|XDisplayStart|
|0x03|uint16_lfsr|XDisplayEnd|
|0x05|uint16_lfsr|YDisplayStart|
|0x07|uint16_lfsr|YDisplayEnd|
|0x09|uint16_lfsr|XEndCount|
|0x0B|uint16_lfsr|HSyncStart|
|0x0D|uint16_lfsr|HSyncEnd|
|0x0F|uint16_be|HPixels|
|0x11|uint16_lfsr|YEndCount|
|0x13|uint16_lfsr|VSyncStart|
|0x15|uint16_lfsr|VSyncEnd|
|0x17|uint16_be|VPixels|
|0x1B|uint16_be|PixelClock5Khz|
|0x1F|uint8|Blank output|
|0x20|uint24_be|Base offset of 16 bits framebuffer|
|0x23|uint24_be|Line stride of 16 bits framebuffer|
|0x26|uint24_be|Base offset of 8 bits framebuffer|
|0x29|uint24_be|Line stride of 8 bits framebuffer|
|0xFF|uint8|Registers update|
