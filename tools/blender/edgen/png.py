"""A minimal 8-bit PNG reader and writer.

Blender has a perfectly good image API. It is not used for quantisation,
because every path through it runs the pixels through colour management, and
the transform applied on read is not exactly the inverse of the one applied on
write. The result is a file whose colours are *near* the palette but not on it
— which defeats the entire point of snapping to a palette, and does so
silently.

Working on the file bytes removes the question. What comes out is exactly what
was put in.

Scope is deliberately narrow: 8 bits per channel, colour type 2 (RGB) or 6
(RGBA), no interlacing. That is what Blender writes, and anything else raises
rather than guessing.

This module has no Blender dependency, so the quantiser can be exercised from
a plain Python REPL.
"""

from __future__ import annotations

import struct
import zlib

_SIGNATURE = b"\x89PNG\r\n\x1a\n"


class PngError(RuntimeError):
    pass


def read_rgba(path: str) -> tuple[int, int, bytearray]:
    """Reads a PNG into ``(width, height, rgba_bytes)``, 4 bytes per pixel."""
    with open(path, "rb") as handle:
        data = handle.read()

    if not data.startswith(_SIGNATURE):
        raise PngError(f"{path}: not a PNG")

    offset = len(_SIGNATURE)
    width = height = 0
    channels = 0
    idat = bytearray()

    while offset < len(data):
        (length,) = struct.unpack(">I", data[offset:offset + 4])
        kind = data[offset + 4:offset + 8]
        payload = data[offset + 8:offset + 8 + length]
        offset += 12 + length  # length + type + payload + crc

        if kind == b"IHDR":
            width, height, depth, colour_type, compression, filter_method, interlace = \
                struct.unpack(">IIBBBBB", payload)

            if depth != 8:
                raise PngError(f"{path}: {depth}-bit PNG; only 8-bit is supported")
            if interlace != 0:
                raise PngError(f"{path}: interlaced PNGs are not supported")
            if compression != 0 or filter_method != 0:
                raise PngError(f"{path}: unexpected compression or filter method")
            if colour_type == 6:
                channels = 4
            elif colour_type == 2:
                channels = 3
            else:
                raise PngError(f"{path}: colour type {colour_type}; expected 2 or 6")

        elif kind == b"IDAT":
            idat += payload

        elif kind == b"IEND":
            break

    if width == 0 or height == 0:
        raise PngError(f"{path}: missing or empty IHDR")

    raw = zlib.decompress(bytes(idat))
    pixels = _unfilter(raw, width, height, channels)

    if channels == 4:
        return width, height, pixels

    # Widen RGB to RGBA so callers only ever deal with one layout.
    rgba = bytearray(width * height * 4)
    for i in range(width * height):
        rgba[i * 4 + 0] = pixels[i * 3 + 0]
        rgba[i * 4 + 1] = pixels[i * 3 + 1]
        rgba[i * 4 + 2] = pixels[i * 3 + 2]
        rgba[i * 4 + 3] = 255
    return width, height, rgba


def _unfilter(raw: bytes, width: int, height: int, channels: int) -> bytearray:
    """Reverses PNG's per-scanline filters. Each row is prefixed by its filter byte."""
    stride = width * channels
    out = bytearray(height * stride)

    previous = bytearray(stride)
    pos = 0

    for y in range(height):
        filter_type = raw[pos]
        pos += 1
        line = bytearray(raw[pos:pos + stride])
        pos += stride

        if filter_type == 0:
            pass
        elif filter_type == 1:  # Sub
            for i in range(channels, stride):
                line[i] = (line[i] + line[i - channels]) & 0xFF
        elif filter_type == 2:  # Up
            for i in range(stride):
                line[i] = (line[i] + previous[i]) & 0xFF
        elif filter_type == 3:  # Average
            for i in range(stride):
                left = line[i - channels] if i >= channels else 0
                line[i] = (line[i] + ((left + previous[i]) >> 1)) & 0xFF
        elif filter_type == 4:  # Paeth
            for i in range(stride):
                left = line[i - channels] if i >= channels else 0
                up = previous[i]
                up_left = previous[i - channels] if i >= channels else 0
                line[i] = (line[i] + _paeth(left, up, up_left)) & 0xFF
        else:
            raise PngError(f"unknown scanline filter {filter_type}")

        out[y * stride:(y + 1) * stride] = line
        previous = line

    return out


def _paeth(a: int, b: int, c: int) -> int:
    p = a + b - c
    pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
    if pa <= pb and pa <= pc:
        return a
    return b if pb <= pc else c


def write_rgba(path: str, width: int, height: int, rgba: bytes) -> None:
    """Writes 8-bit RGBA with no filtering. Pixel art compresses fine without it."""
    if len(rgba) != width * height * 4:
        raise PngError(f"expected {width * height * 4} bytes, got {len(rgba)}")

    stride = width * 4
    raw = bytearray()
    for y in range(height):
        raw.append(0)  # filter type: None
        raw += rgba[y * stride:(y + 1) * stride]

    chunks = [
        _chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0)),
        _chunk(b"IDAT", zlib.compress(bytes(raw), 9)),
        _chunk(b"IEND", b""),
    ]

    with open(path, "wb") as handle:
        handle.write(_SIGNATURE)
        for chunk in chunks:
            handle.write(chunk)


def _chunk(kind: bytes, payload: bytes) -> bytes:
    return (
        struct.pack(">I", len(payload))
        + kind
        + payload
        + struct.pack(">I", zlib.crc32(kind + payload) & 0xFFFFFFFF)
    )
