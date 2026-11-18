#include "content/common/PngDecode.h"

#include <zlib.h>

#include <algorithm>
#include <cstdio>
#include <cstdlib>
#include <cstring>

namespace bl::runtime::content {

namespace {

constexpr uint8_t kSignature[8] = {0x89, 'P', 'N', 'G', '\r', '\n', 0x1A, '\n'};
constexpr int kMaxSide = 16384;

struct Pass {
    int x0, y0, dx, dy;
};

constexpr Pass kAdam7[7] = {{0, 0, 8, 8}, {4, 0, 8, 8}, {0, 4, 4, 8}, {2, 0, 4, 4},
                            {0, 2, 2, 4}, {1, 0, 2, 2}, {0, 1, 1, 2}};
constexpr Pass kWhole[1] = {{0, 0, 1, 1}};

struct Header {
    int width = 0, height = 0, depth = 0, color = 0, interlace = 0, channels = 0;
    uint8_t palette[256][4]{};
    int paletteSize = 0;
    bool hasKey = false;
    uint16_t key[3]{};
};

uint32_t be32(const uint8_t* p) {
    return (uint32_t(p[0]) << 24) | (uint32_t(p[1]) << 16) | (uint32_t(p[2]) << 8) | uint32_t(p[3]);
}

uint16_t be16(const uint8_t* p) { return static_cast<uint16_t>((p[0] << 8) | p[1]); }

bool fail(std::string* error, const char* what) {
    if (error) *error = what;
    return false;
}

int channelsOf(int color) {
    switch (color) {
        case 0: return 1;
        case 2: return 3;
        case 3: return 1;
        case 4: return 2;
        case 6: return 4;
        default: return 0;
    }
}

bool validDepth(int color, int depth) {
    switch (color) {
        case 0: return depth == 1 || depth == 2 || depth == 4 || depth == 8 || depth == 16;
        case 3: return depth == 1 || depth == 2 || depth == 4 || depth == 8;
        case 2:
        case 4:
        case 6: return depth == 8 || depth == 16;
        default: return false;
    }
}

int passSize(int full, int start, int step) { return full > start ? (full - start + step - 1) / step : 0; }

size_t rowBytes(const Header& h, int width) {
    return (static_cast<size_t>(width) * h.channels * h.depth + 7) / 8;
}

uint8_t paeth(int a, int b, int c) {
    const int p = a + b - c, pa = std::abs(p - a), pb = std::abs(p - b), pc = std::abs(p - c);
    return static_cast<uint8_t>(pa <= pb && pa <= pc ? a : pb <= pc ? b : c);
}

bool unfilter(uint8_t* data, size_t bytes, int rows, size_t bpp) {
    const uint8_t* prev = nullptr;
    for (int y = 0; y < rows; ++y) {
        uint8_t* row = data + static_cast<size_t>(y) * (bytes + 1);
        uint8_t* cur = row + 1;
        switch (row[0]) {
            case 0: break;
            case 1:
                for (size_t i = bpp; i < bytes; ++i) cur[i] = static_cast<uint8_t>(cur[i] + cur[i - bpp]);
                break;
            case 2:
                if (prev)
                    for (size_t i = 0; i < bytes; ++i) cur[i] = static_cast<uint8_t>(cur[i] + prev[i]);
                break;
            case 3:
                for (size_t i = 0; i < bytes; ++i) {
                    const int left = i >= bpp ? cur[i - bpp] : 0, up = prev ? prev[i] : 0;
                    cur[i] = static_cast<uint8_t>(cur[i] + ((left + up) >> 1));
                }
                break;
            case 4:
                for (size_t i = 0; i < bytes; ++i) {
                    const int left = i >= bpp ? cur[i - bpp] : 0, up = prev ? prev[i] : 0,
                              upLeft = prev && i >= bpp ? prev[i - bpp] : 0;
                    cur[i] = static_cast<uint8_t>(cur[i] + paeth(left, up, upLeft));
                }
                break;
            default: return false;
        }
        prev = cur;
    }
    return true;
}

uint32_t sample(const uint8_t* row, int index, int depth) {
    if (depth == 8) return row[index];
    if (depth == 16) return be16(row + static_cast<size_t>(index) * 2);
    const int bit = index * depth;
    return (row[bit >> 3] >> (8 - depth - (bit & 7))) & ((1u << depth) - 1u);
}

uint8_t to8(uint32_t value, int depth) {
    switch (depth) {
        case 1: return value ? 255 : 0;
        case 2: return static_cast<uint8_t>(value * 85);
        case 4: return static_cast<uint8_t>(value * 17);
        case 16: return static_cast<uint8_t>(value >> 8);
        default: return static_cast<uint8_t>(value);
    }
}

void convertRow(const Header& h, const uint8_t* row, int count, const Pass& pass, uint8_t* dst) {
    const int d = h.depth;
    if (pass.dx == 1 && h.color == 6 && d == 8) {
        std::memcpy(dst, row, static_cast<size_t>(count) * 4);
        return;
    }
    for (int x = 0; x < count; ++x) {
        uint8_t* out = dst + static_cast<size_t>(pass.x0 + x * pass.dx) * 4;
        switch (h.color) {
            case 0: {
                const uint32_t g = sample(row, x, d);
                out[0] = out[1] = out[2] = to8(g, d);
                out[3] = h.hasKey && g == h.key[0] ? 0 : 255;
                break;
            }
            case 2: {
                const uint32_t r = sample(row, x * 3, d), g = sample(row, x * 3 + 1, d), b = sample(row, x * 3 + 2, d);
                out[0] = to8(r, d);
                out[1] = to8(g, d);
                out[2] = to8(b, d);
                out[3] = h.hasKey && r == h.key[0] && g == h.key[1] && b == h.key[2] ? 0 : 255;
                break;
            }
            case 3: {
                const uint32_t i = sample(row, x, d);
                if (i < static_cast<uint32_t>(h.paletteSize)) std::memcpy(out, h.palette[i], 4);
                else out[0] = out[1] = out[2] = 0, out[3] = 255;
                break;
            }
            case 4: {
                out[0] = out[1] = out[2] = to8(sample(row, x * 2, d), d);
                out[3] = to8(sample(row, x * 2 + 1, d), d);
                break;
            }
            default: {
                for (int c = 0; c < 4; ++c) out[c] = to8(sample(row, x * 4 + c, d), d);
                break;
            }
        }
    }
}

}

bool decodePng(const uint8_t* data, size_t size, bool bottomUp, int* width, int* height,
               std::vector<uint8_t>& rgba, std::string* error) {
    if (!data || size < 8 || std::memcmp(data, kSignature, 8) != 0) return fail(error, "not a PNG file");
    Header h;
    bool haveHeader = false;
    std::vector<uint8_t> raw;
    z_stream z{};
    bool zOpen = false, zEnded = false;
    const char* problem = nullptr;
    size_t pos = 8;
    while (!problem && pos + 12 <= size) {
        const uint32_t length = be32(data + pos);
        if (length > size - pos - 12) {
            problem = "truncated chunk";
            break;
        }
        const uint8_t* type = data + pos + 4;
        const uint8_t* body = data + pos + 8;
        pos += 12 + static_cast<size_t>(length);
        if (!std::memcmp(type, "IHDR", 4)) {
            if (haveHeader || length < 13) {
                problem = "bad header";
                break;
            }
            h.width = static_cast<int>(std::min<uint32_t>(be32(body), 0x7FFFFFFF));
            h.height = static_cast<int>(std::min<uint32_t>(be32(body + 4), 0x7FFFFFFF));
            h.depth = body[8];
            h.color = body[9];
            h.interlace = body[12];
            h.channels = channelsOf(h.color);
            if (h.width <= 0 || h.height <= 0 || h.width > kMaxSide || h.height > kMaxSide) problem = "unsupported size";
            else if (!h.channels || !validDepth(h.color, h.depth)) problem = "unsupported pixel format";
            else if (body[10] != 0 || body[11] != 0 || h.interlace > 1) problem = "unsupported compression or interlace";
            if (problem) break;
            size_t total = 0;
            const Pass* passes = h.interlace ? kAdam7 : kWhole;
            const int count = h.interlace ? 7 : 1;
            for (int i = 0; i < count; ++i) {
                const int pw = passSize(h.width, passes[i].x0, passes[i].dx), ph = passSize(h.height, passes[i].y0, passes[i].dy);
                if (pw && ph) total += static_cast<size_t>(ph) * (rowBytes(h, pw) + 1);
            }
            raw.resize(total);
            if (inflateInit(&z) != Z_OK) {
                problem = "zlib did not start";
                break;
            }
            zOpen = true;
            z.next_out = raw.data();
            z.avail_out = static_cast<uInt>(total);
            haveHeader = true;
        } else if (!std::memcmp(type, "PLTE", 4)) {
            h.paletteSize = static_cast<int>(std::min<uint32_t>(256, length / 3));
            for (int i = 0; i < h.paletteSize; ++i) {
                h.palette[i][0] = body[i * 3];
                h.palette[i][1] = body[i * 3 + 1];
                h.palette[i][2] = body[i * 3 + 2];
                h.palette[i][3] = 255;
            }
        } else if (!std::memcmp(type, "tRNS", 4)) {
            if (h.color == 3) {
                for (uint32_t i = 0; i < length && i < 256; ++i) h.palette[i][3] = body[i];
            } else if (h.color == 0 && length >= 2) {
                h.hasKey = true;
                h.key[0] = be16(body);
            } else if (h.color == 2 && length >= 6) {
                h.hasKey = true;
                h.key[0] = be16(body);
                h.key[1] = be16(body + 2);
                h.key[2] = be16(body + 4);
            }
        } else if (!std::memcmp(type, "IDAT", 4)) {
            if (!haveHeader) {
                problem = "image data before the header";
                break;
            }
            if (zEnded || z.avail_out == 0) continue;
            z.next_in = const_cast<Bytef*>(body);
            z.avail_in = length;
            const int status = inflate(&z, Z_NO_FLUSH);
            if (status == Z_STREAM_END) zEnded = true;
            else if (status != Z_OK && status != Z_BUF_ERROR) problem = "corrupt image data";
        } else if (!std::memcmp(type, "IEND", 4)) {
            break;
        }
    }
    if (zOpen) inflateEnd(&z);
    if (problem) return fail(error, problem);
    if (!haveHeader) return fail(error, "no header");
    if (z.avail_out != 0) return fail(error, "not enough image data");

    rgba.assign(static_cast<size_t>(h.width) * h.height * 4, 0);
    const size_t bpp = std::max<size_t>(1, static_cast<size_t>(h.channels * h.depth) / 8);
    const Pass* passes = h.interlace ? kAdam7 : kWhole;
    const int count = h.interlace ? 7 : 1;
    size_t offset = 0;
    for (int i = 0; i < count; ++i) {
        const Pass& pass = passes[i];
        const int pw = passSize(h.width, pass.x0, pass.dx), ph = passSize(h.height, pass.y0, pass.dy);
        if (!pw || !ph) continue;
        const size_t bytes = rowBytes(h, pw);
        if (!unfilter(raw.data() + offset, bytes, ph, bpp)) return fail(error, "bad row filter");
        for (int y = 0; y < ph; ++y) {
            const uint8_t* row = raw.data() + offset + static_cast<size_t>(y) * (bytes + 1) + 1;
            const int iy = pass.y0 + y * pass.dy;
            uint8_t* dst = rgba.data() + static_cast<size_t>(bottomUp ? h.height - 1 - iy : iy) * h.width * 4;
            convertRow(h, row, pw, pass, dst);
        }
        offset += static_cast<size_t>(ph) * (bytes + 1);
    }
    if (width) *width = h.width;
    if (height) *height = h.height;
    return true;
}

bool decodePngFile(const std::string& path, bool bottomUp, int* width, int* height,
                   std::vector<uint8_t>& rgba, std::string* error) {
    FILE* f = std::fopen(path.c_str(), "rb");
    if (!f) return fail(error, "the file does not open");
    std::fseek(f, 0, SEEK_END);
    const long n = std::ftell(f);
    std::fseek(f, 0, SEEK_SET);
    std::vector<uint8_t> bytes(n > 0 ? static_cast<size_t>(n) : 0);
    const size_t got = n > 0 ? std::fread(bytes.data(), 1, bytes.size(), f) : 0;
    std::fclose(f);
    if (n <= 0 || got != bytes.size()) return fail(error, "the file does not read");
    return decodePng(bytes.data(), bytes.size(), bottomUp, width, height, rgba, error);
}

}
