#pragma once

#include <stddef.h>
#include <stdint.h>

typedef struct {
    uint16_t width;
    uint16_t height;
    uint16_t line_stride;
    uint16_t modified_x1;
    uint16_t modified_y1;
    uint16_t modified_x2;
    uint16_t modified_y2;
} zerokvm_frame_area_t;

/* Create a decoder context. Returns an opaque handle, or NULL on failure. */
void *zerokvm_udl_create(uint32_t max_width, uint32_t max_height);

/* Free a decoder context. Safe to call with NULL. */
void zerokvm_udl_destroy(void *handle);

/*
 * Process a USB bulk transfer payload. Returns the number of bytes consumed,
 * or -1 on error. Call repeatedly as USB packets arrive.
 */
int zerokvm_udl_process(void *handle, const uint8_t *data, size_t length);

/*
 * Copy the current frame to caller-supplied buffers. Either output pointer may
 * be NULL to skip that copy. Returns 0 on success, -1 on error.
 *
 * xrgb8888: destination in XBGR8888 format (B at bits 23:16, G at 15:8, R at 7:0).
 * rgb565:   destination in RGB565 little-endian format (raw framebuffer contents).
 * area:     if non-NULL, filled with the resolution and dirty rectangle of pixels
 *           changed since the previous call.
 */
int zerokvm_udl_copy_framebuffers(void *handle,
                                  uint16_t *rgb565,
                                  uint32_t rgb565_stride_pixels,
                                  uint32_t *xrgb8888,
                                  uint32_t xrgb8888_stride_pixels,
                                  zerokvm_frame_area_t *area);

/*
 * Copy 256 bytes of DisplayLink register memory into buf. Returns 0 on
 * success, -1 on error. buf must be at least 256 bytes.
 */
int zerokvm_udl_copy_registers(void *handle, uint8_t *buf, size_t length);

/* Query the current display mode. Color depth: 16 = RGB565, 24 = RGB888. */
uint8_t  zerokvm_udl_get_color_depth(void *handle);
uint16_t zerokvm_udl_get_hpixels(void *handle);
uint16_t zerokvm_udl_get_vpixels(void *handle);

/* Raw byte offsets into the internal framebuffer (advanced use). */
uint32_t zerokvm_udl_get_base16bpp(void *handle);
uint32_t zerokvm_udl_get_base8bpp(void *handle);
