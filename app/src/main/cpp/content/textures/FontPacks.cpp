#include "content/textures/FontPacks.h"
#include "content/common/ContentAssets.h"
#include "content/common/ModContent.h"
#include "core/Log.h"
#include "il2cpp/Api.h"
#include "il2cpp/Resolver.h"

#include <algorithm>
#include <atomic>
#include <cmath>
#include <cstdio>
#include <cstring>
#include <dirent.h>
#include <map>
#include <mutex>
#include <sys/stat.h>
#include <vector>

// Fontes de pacote: content/Fonts/<Fonte>.json + paginas PNG trocam as
// fontes do jogo (FontAssets). O SpriteFont do celular indexa por uma tabela
// direta char -> glifo (ushort[65535]), entao o pacote substitui os glifos que
// o jogo ja tem e acrescenta os novos; o que o pacote nao traz continua com a
// fonte original. A troca e feita NO PROPRIO objeto SpriteFont: a interface
// do celular guarda a referencia (GetFont() dos modelos) e veria a antiga se
// so o Asset.Value mudasse.
//
// Nitidez: o SpriteBatch tira a UV de TexelWidth/TexelHeight (1/Width logico)
// e o tamanho na tela do retangulo do glifo. Uma pagina com "scale": N e N
// vezes maior que o retangulo logico; com TexelWidth = N/largura, o glifo
// sai no tamanho do jogo com N vezes mais pixels, filtrado (trilinear).

namespace bl::runtime {
namespace {

// ---- JSON minimo (objeto, lista, texto, numero, true/false/null) ----

struct Json {
    enum Kind { Null, Bool, Number, String, Array, Object } kind = Null;
    double number = 0;
    bool boolean = false;
    std::string text;
    std::vector<Json> items;
    std::vector<std::pair<std::string, Json>> fields;

    const Json* get(const char* key) const {
        for (const auto& f : fields) if (f.first == key) return &f.second;
        return nullptr;
    }
};

struct JsonParser {
    const char* p;
    const char* end;
    bool failed = false;

    void skip() { while (p < end && (*p == ' ' || *p == '\n' || *p == '\r' || *p == '\t')) ++p; }

    bool literal(const char* word) {
        const size_t n = std::strlen(word);
        if (static_cast<size_t>(end - p) < n || std::strncmp(p, word, n) != 0) return false;
        p += n;
        return true;
    }

    void appendUtf8(std::string& out, uint32_t c) {
        if (c < 0x80) out += static_cast<char>(c);
        else if (c < 0x800) { out += static_cast<char>(0xC0 | (c >> 6)); out += static_cast<char>(0x80 | (c & 0x3F)); }
        else if (c < 0x10000) {
            out += static_cast<char>(0xE0 | (c >> 12));
            out += static_cast<char>(0x80 | ((c >> 6) & 0x3F));
            out += static_cast<char>(0x80 | (c & 0x3F));
        } else {
            out += static_cast<char>(0xF0 | (c >> 18));
            out += static_cast<char>(0x80 | ((c >> 12) & 0x3F));
            out += static_cast<char>(0x80 | ((c >> 6) & 0x3F));
            out += static_cast<char>(0x80 | (c & 0x3F));
        }
    }

    bool string(std::string& out) {
        if (p >= end || *p != '"') return false;
        ++p;
        while (p < end && *p != '"') {
            if (*p != '\\') { out += *p++; continue; }
            if (++p >= end) return false;
            const char e = *p++;
            switch (e) {
                case 'n': out += '\n'; break;
                case 't': out += '\t'; break;
                case 'r': out += '\r'; break;
                case 'b': out += '\b'; break;
                case 'f': out += '\f'; break;
                case 'u': {
                    if (end - p < 4) return false;
                    uint32_t c = static_cast<uint32_t>(std::strtoul(std::string(p, 4).c_str(), nullptr, 16));
                    p += 4;
                    if (c >= 0xD800 && c < 0xDC00 && end - p >= 6 && p[0] == '\\' && p[1] == 'u') {
                        const uint32_t low = static_cast<uint32_t>(std::strtoul(std::string(p + 2, 4).c_str(), nullptr, 16));
                        if (low >= 0xDC00 && low < 0xE000) { c = 0x10000 + ((c - 0xD800) << 10) + (low - 0xDC00); p += 6; }
                    }
                    appendUtf8(out, c);
                    break;
                }
                default: out += e; break;
            }
        }
        if (p >= end) return false;
        ++p;
        return true;
    }

    Json value(int depth = 0) {
        Json v;
        skip();
        if (p >= end || depth > 32) { failed = true; return v; }
        if (*p == '{') {
            v.kind = Json::Object;
            ++p; skip();
            if (p < end && *p == '}') { ++p; return v; }
            while (!failed) {
                skip();
                std::string key;
                if (!string(key)) { failed = true; break; }
                skip();
                if (p >= end || *p != ':') { failed = true; break; }
                ++p;
                v.fields.emplace_back(std::move(key), value(depth + 1));
                skip();
                if (p < end && *p == ',') { ++p; continue; }
                if (p < end && *p == '}') { ++p; break; }
                failed = true;
            }
        } else if (*p == '[') {
            v.kind = Json::Array;
            ++p; skip();
            if (p < end && *p == ']') { ++p; return v; }
            while (!failed) {
                v.items.push_back(value(depth + 1));
                skip();
                if (p < end && *p == ',') { ++p; continue; }
                if (p < end && *p == ']') { ++p; break; }
                failed = true;
            }
        } else if (*p == '"') {
            v.kind = Json::String;
            if (!string(v.text)) failed = true;
        } else if (literal("true")) { v.kind = Json::Bool; v.boolean = true; }
        else if (literal("false")) { v.kind = Json::Bool; }
        else if (literal("null")) {}
        else {
            char* stop = nullptr;
            v.kind = Json::Number;
            v.number = std::strtod(p, &stop);
            if (stop == p) failed = true;
            else p = stop;
        }
        return v;
    }
};

bool readFile(const std::string& path, std::string& out) {
    FILE* f = std::fopen(path.c_str(), "rb");
    if (!f) return false;
    char buf[16384];
    size_t n;
    while ((n = std::fread(buf, 1, sizeof(buf), f)) > 0) out.append(buf, n);
    std::fclose(f);
    return true;
}

// ---- Fontes do jogo ----

struct Target {
    const char* label;     // no log
    const char* field;     // FontAssets.<field>
    int index;             // -1: campo direto; senao FontAssets.CombatText[index]
    const char* file;      // nome do arquivo sem extensao, ja canonico (Mouse_Text e MouseText -> mousetext)
};

constexpr Target kTargets[] = {
    {"MouseText", "MouseText", -1, "mousetext"},
    {"ItemStack", "ItemStack", -1, "itemstack"},
    {"DeathText", "DeathText", -1, "deathtext"},
    {"CombatText[0]", "CombatText", 0, "combattext"},
    {"CombatText[1]", "CombatText", 1, "combatcrit"},
};

// SpriteFont.Glyph no celular (dump 1.4.5.8.6): 0x38 bytes.
constexpr size_t kGlyphSize = 0x38;
constexpr size_t kCharacter = 0x0, kBounds = 0x4, kCropping = 0x14, kLeft = 0x24, kRight = 0x28,
                 kWidth = 0x2C, kWidthWithBearings = 0x30, kTextureIndex = 0x34;
constexpr uint16_t kNoGlyph = 0xFFFF;

struct Pack { std::string uid, fonts; };

struct Patched {
    uint32_t font = 0, glyphs = 0, table = 0, textures = 0;   // gchandles: o SpriteFont e os arrays originais
    std::vector<uint32_t> pages;                              // texturas criadas pelo pacote
};

std::mutex g_mutex;
std::vector<Pack> g_pending;
std::atomic<bool> g_dirty{false};
std::vector<Patched> g_patched; // So a thread do jogo acessa.

struct Refs {
    bool tried = false, ok = false;
    Il2CppClass* fontAssets = nullptr;
    int32_t assetValue = -1;
    Il2CppClass* font = nullptr;
    int32_t glyphs = -1, table = -1, textures = -1, lineSpacing = -1, spacing = -1, end = -1;
    int32_t width = -1, height = -1, texelWidth = -1, texelHeight = -1;
};

Refs& refs() {
    static Refs r;
    if (r.tried) return r;
    r.tried = true;
    auto& a = il2cpp::api();
    r.fontAssets = il2cpp::findClass({"Terraria.GameContent", "FontAssets", {}});
    Il2CppClass* font = r.font = il2cpp::findClass({"Microsoft.Xna.Framework.Graphics", "SpriteFont", {}});
    Il2CppClass* texture = il2cpp::findClass({"Microsoft.Xna.Framework.Graphics", "Texture2D", {}});
    FieldInfo* mouse = r.fontAssets ? il2cpp::findField(r.fontAssets, "MouseText") : nullptr;
    Il2CppClass* assetCls = mouse ? a.class_from_il2cpp_type(a.field_get_type(mouse)) : nullptr;
    r.assetValue = assetCls ? il2cpp::fieldOffset(assetCls, "Value") : -1;
    if (font) {
        r.glyphs = il2cpp::fieldOffset(font, "_glyphs");
        r.table = il2cpp::fieldOffset(font, "_glyphIndexTable");
        r.textures = il2cpp::fieldOffset(font, "_textures");
        r.lineSpacing = il2cpp::fieldOffset(font, "<LineSpacing>k__BackingField");
        r.spacing = il2cpp::fieldOffset(font, "<Spacing>k__BackingField");
        // O ultimo campo (Vector2 tagSize): ate onde copiar numa fonte nova.
        const int32_t tag = il2cpp::fieldOffset(font, "tagSize");
        r.end = tag >= 0 ? tag + 8 : -1;
    }
    if (texture) {
        r.width = il2cpp::fieldOffset(texture, "Width");
        r.height = il2cpp::fieldOffset(texture, "Height");
        r.texelWidth = il2cpp::fieldOffset(texture, "TexelWidth");
        r.texelHeight = il2cpp::fieldOffset(texture, "TexelHeight");
    }
    r.ok = r.fontAssets && r.assetValue >= 0 && r.glyphs >= 0 && r.table >= 0 && r.textures >= 0 && r.width >= 0 &&
           r.height >= 0 && r.texelWidth >= 0 && r.texelHeight >= 0 && a.array_new && a.gc_wbarrier_set_field &&
           a.gchandle_new && a.gchandle_get_target && a.gchandle_free;
    if (!r.ok) BL_ERROR("Font packs: SpriteFont/FontAssets layout not found; fonts will not be replaced");
    return r;
}

template <typename T>
T& at(Il2CppObject* object, int32_t offset) {
    return *reinterpret_cast<T*>(reinterpret_cast<char*>(object) + offset);
}

void setRef(Il2CppObject* owner, void* slot, Il2CppObject* value) {
    il2cpp::api().gc_wbarrier_set_field(owner, reinterpret_cast<void**>(slot), value);
}

Il2CppObject* gameFont(const Target& t) {
    const Refs& r = refs();
    auto& a = il2cpp::api();
    FieldInfo* field = il2cpp::findField(r.fontAssets, t.field);
    if (!field) return nullptr;
    Il2CppObject* asset = nullptr;
    a.field_static_get_value(field, &asset);
    if (asset && t.index >= 0) {
        auto* array = reinterpret_cast<Il2CppArray*>(asset);
        asset = static_cast<uintptr_t>(t.index) < array->length ? static_cast<Il2CppObject**>(arrayData(array))[t.index] : nullptr;
    }
    return asset ? at<Il2CppObject*>(asset, r.assetValue) : nullptr;
}

std::string canonical(const std::string& name) {
    std::string out;
    for (char c : name) {
        if (c == '_' || c == ' ' || c == '-') continue;
        out += (c >= 'A' && c <= 'Z') ? static_cast<char>(c + 'a' - 'A') : c;
    }
    return out;
}

bool directory(const std::string& path) {
    struct stat st{};
    return lstat(path.c_str(), &st) == 0 && S_ISDIR(st.st_mode);
}

/** content/Fonts/<arquivo>.json do alvo, ou "" (aceita Mouse_Text.json e MouseText.json). */
std::string fontFile(const std::string& fonts, const Target& t) {
    DIR* dir = opendir(fonts.c_str());
    if (!dir) return {};
    std::string found;
    while (dirent* e = readdir(dir)) {
        const std::string name = e->d_name;
        if (name.size() <= 5 || canonical(name.substr(name.size() - 5)) != ".json") continue;
        if (canonical(name.substr(0, name.size() - 5)) == t.file) { found = fonts + "/" + name; break; }
    }
    closedir(dir);
    return found;
}

std::string parentOf(const std::string& path) {
    const size_t slash = path.rfind('/');
    return slash == std::string::npos ? std::string(".") : path.substr(0, slash);
}

/** O arquivo da pagina, sem sair da pasta content/Fonts do pacote. */
bool safeRelative(const std::string& name) {
    if (name.empty() || name[0] == '/' || name.find('\\') != std::string::npos) return false;
    size_t start = 0;
    while (start <= name.size()) {
        const size_t slash = name.find('/', start);
        const std::string part = name.substr(start, slash == std::string::npos ? std::string::npos : slash - start);
        if (part == ".." || part.empty()) return false;
        if (slash == std::string::npos) break;
        start = slash + 1;
    }
    return true;
}

void freePatch(Patched& p) {
    auto& a = il2cpp::api();
    for (auto handle : p.pages) {
        content::destroyGameTexture(a.gchandle_get_target(handle));
        a.gchandle_free(handle);
    }
    for (auto handle : {p.font, p.glyphs, p.table, p.textures}) if (handle) a.gchandle_free(handle);
    p = {};
}

void restore() {
    const Refs& r = refs();
    auto& a = il2cpp::api();
    for (auto& p : g_patched) {
        Il2CppObject* font = a.gchandle_get_target(p.font);
        if (font) {
            setRef(font, &at<Il2CppObject*>(font, r.glyphs), a.gchandle_get_target(p.glyphs));
            setRef(font, &at<Il2CppObject*>(font, r.table), a.gchandle_get_target(p.table));
            setRef(font, &at<Il2CppObject*>(font, r.textures), a.gchandle_get_target(p.textures));
        }
        freePatch(p);
    }
    g_patched.clear();
}

struct Record {
    uint16_t code;
    int page;
    int bounds[4], cropping[4];
    float left, width, right;
};

/**
 * Aplica um arquivo de fonte a um SpriteFont. false (com log, prefixado por
 * `owner`) deixa a fonte como estava. `metrics` tambem troca LineSpacing/Spacing pelos do arquivo
 * (fonte nova de mod; nas do jogo o layout das telas fica como esta).
 */
bool applyFont(const std::string& owner, const char* label, const std::string& path, Il2CppObject* font,
               Patched& patch, bool metrics) {
    const Refs& r = refs();
    auto& a = il2cpp::api();

    std::string text;
    if (!readFile(path, text)) { BL_WARN("%s: cannot read %s", owner.c_str(), path.c_str()); return false; }
    JsonParser parser{text.data(), text.data() + text.size()};
    const Json meta = parser.value();
    if (parser.failed || meta.kind != Json::Object) {
        BL_WARN("%s: %s is not valid JSON", owner.c_str(), path.c_str());
        return false;
    }
    const Json* format = meta.get("format");
    const std::string fmt = format && format->kind == Json::String ? format->text : "";
    if (fmt != "blfont/1" && fmt != "tlfont/1") {
        BL_WARN("%s: %s has unknown format '%s' (expected blfont/1)", owner.c_str(), path.c_str(), fmt.c_str());
        return false;
    }
    const Json* scaleField = meta.get("scale");
    const int scale = scaleField && scaleField->kind == Json::Number ? static_cast<int>(scaleField->number) : 1;
    if (scale < 1 || scale > 8) { BL_WARN("%s: %s scale %d out of range 1..8", owner.c_str(), path.c_str(), scale); return false; }
    const Json* pagesField = meta.get("pages");
    const Json* glyphsField = meta.get("glyphs");
    if (!pagesField || pagesField->kind != Json::Array || pagesField->items.empty() ||
        !glyphsField || glyphsField->kind != Json::Array) {
        BL_WARN("%s: %s needs 'pages' and 'glyphs'", owner.c_str(), path.c_str());
        return false;
    }

    // Os arrays do jogo.
    auto* oldGlyphs = at<Il2CppArray*>(font, r.glyphs);
    auto* oldTable = at<Il2CppArray*>(font, r.table);
    auto* oldTextures = at<Il2CppArray*>(font, r.textures);
    if (!oldGlyphs || !oldTable || !oldTextures || oldTable->length < 0xFFFF) {
        BL_WARN("%s: %s has no glyph arrays yet", owner.c_str(), label);
        return false;
    }
    const size_t count = oldGlyphs->length;
    auto* glyphBytes = static_cast<uint8_t*>(arrayData(oldGlyphs));
    auto* table = static_cast<uint16_t*>(arrayData(oldTable));
    // Confere o passo de 0x38: cada glifo do jogo esta na tabela pelo proprio char.
    for (size_t i = 0; i < count && i < 64; ++i) {
        const uint16_t c = *reinterpret_cast<uint16_t*>(glyphBytes + i * kGlyphSize + kCharacter);
        if (table[c] != i) {
            BL_ERROR("%s: unexpected SpriteFont.Glyph layout in %s (glyph %zu)", owner.c_str(), label, i);
            return false;
        }
    }
    const size_t oldPages = oldTextures->length;

    // Paginas: PNG com `scale` vezes o tamanho logico.
    patch = {};
    std::vector<Il2CppObject*> pages;
    std::vector<std::pair<int, int>> logical;
    const std::string folder = parentOf(path);
    for (const auto& item : pagesField->items) {
        const std::string name = item.kind == Json::String ? item.text : "";
        if (!safeRelative(name)) {
            BL_WARN("%s: %s has an invalid page name", owner.c_str(), path.c_str());
            freePatch(patch);
            return false;
        }
        int w = 0, h = 0;
        Il2CppObject* page = content::loadGameTexture(folder + "/" + name, nullptr, 0, name,
                                                      scale > 1 ? 2 /* Trilinear */ : 1 /* Bilinear */, &w, &h);
        uint32_t handle = page ? a.gchandle_new(page, false) : 0;
        if (!handle) {
            BL_WARN("%s: cannot load page %s", owner.c_str(), name.c_str());
            if (page) content::destroyGameTexture(page);
            freePatch(patch);
            return false;
        }
        patch.pages.push_back(handle);
        if (w <= 0 || h <= 0 || w % scale || h % scale)
            BL_WARN("%s: page %s is %dx%d, not a multiple of scale %d", owner.c_str(), name.c_str(), w, h, scale);
        const int lw = std::max(1, w / scale), lh = std::max(1, h / scale);
        // O SpriteBatch so le TexelWidth/Height para a UV; Width/Height logicos para quem medir a pagina.
        at<int32_t>(page, r.width) = lw;
        at<int32_t>(page, r.height) = lh;
        at<float>(page, r.texelWidth) = static_cast<float>(scale) / static_cast<float>(std::max(1, w));
        at<float>(page, r.texelHeight) = static_cast<float>(scale) / static_cast<float>(std::max(1, h));
        pages.push_back(page);
        logical.emplace_back(lw, lh);
    }
    if (oldPages + pages.size() > 256) {
        BL_WARN("%s: %s would have %zu texture pages (max 256)", owner.c_str(), label, oldPages + pages.size());
        freePatch(patch);
        return false;
    }

    // Glifos: [code, page, bx, by, bw, bh, cx, cy, cw, ch, left, width, right] em unidades logicas.
    std::vector<Record> records;
    std::vector<int32_t> slot(0x10000, -1);
    size_t skipped = 0;
    for (const auto& item : glyphsField->items) {
        if (item.kind != Json::Array || item.items.size() < 13) { ++skipped; continue; }
        double v[13];
        bool numbers = true;
        for (int i = 0; i < 13; ++i) {
            numbers = numbers && item.items[i].kind == Json::Number && std::isfinite(item.items[i].number);
            v[i] = numbers ? item.items[i].number : 0;
        }
        const int code = static_cast<int>(v[0]), page = static_cast<int>(v[1]);
        if (!numbers || code <= 0 || code >= 0xFFFF || page < 0 || static_cast<size_t>(page) >= pages.size()) { ++skipped; continue; }
        Record rec{static_cast<uint16_t>(code), page, {}, {}, static_cast<float>(v[10]), static_cast<float>(v[11]), static_cast<float>(v[12])};
        for (int i = 0; i < 4; ++i) { rec.bounds[i] = static_cast<int>(v[2 + i]); rec.cropping[i] = static_cast<int>(v[6 + i]); }
        const auto [lw, lh] = logical[page];
        if (rec.bounds[0] < 0 || rec.bounds[1] < 0 || rec.bounds[2] < 0 || rec.bounds[3] < 0 ||
            rec.bounds[0] + rec.bounds[2] > lw || rec.bounds[1] + rec.bounds[3] > lh) { ++skipped; continue; }
        if (slot[code] >= 0) { ++skipped; continue; }   // char repetido: vale o primeiro
        slot[code] = static_cast<int32_t>(records.size());
        records.push_back(rec);
    }
    if (records.empty()) {
        BL_WARN("%s: %s has no usable glyphs", owner.c_str(), path.c_str());
        freePatch(patch);
        return false;
    }

    // Quem o jogo ja tem e trocado no lugar; o resto vai no fim.
    size_t appended = 0;
    for (const auto& rec : records) {
        const uint16_t index = table[rec.code];
        const bool own = index != kNoGlyph && index < count &&
                         *reinterpret_cast<uint16_t*>(glyphBytes + index * kGlyphSize + kCharacter) == rec.code;
        if (!own) ++appended;
    }
    const size_t total = count + appended;
    if (total >= kNoGlyph) {
        BL_WARN("%s: %s would have %zu glyphs (max 65534)", owner.c_str(), label, total);
        freePatch(patch);
        return false;
    }
    Il2CppClass* glyphClass = a.class_get_element_class(a.object_get_class(reinterpret_cast<Il2CppObject*>(oldGlyphs)));
    Il2CppClass* shortClass = a.class_get_element_class(a.object_get_class(reinterpret_cast<Il2CppObject*>(oldTable)));
    Il2CppClass* textureClass = a.class_get_element_class(a.object_get_class(reinterpret_cast<Il2CppObject*>(oldTextures)));
    Il2CppArray* newGlyphs = a.array_new(glyphClass, total);
    Il2CppArray* newTable = a.array_new(shortClass, oldTable->length);
    Il2CppArray* newTextures = a.array_new(textureClass, oldPages + pages.size());
    if (!newGlyphs || !newTable || !newTextures) {
        BL_ERROR("%s: cannot allocate arrays for %s", owner.c_str(), label);
        freePatch(patch);
        return false;
    }
    auto* glyphOut = static_cast<uint8_t*>(arrayData(newGlyphs));
    auto* tableOut = static_cast<uint16_t*>(arrayData(newTable));
    std::memcpy(glyphOut, glyphBytes, count * kGlyphSize);
    std::memcpy(tableOut, table, oldTable->length * sizeof(uint16_t));
    auto** texturesIn = static_cast<Il2CppObject**>(arrayData(oldTextures));
    auto** texturesOut = static_cast<Il2CppObject**>(arrayData(newTextures));
    for (size_t i = 0; i < oldPages; ++i) setRef(reinterpret_cast<Il2CppObject*>(newTextures), &texturesOut[i], texturesIn[i]);
    for (size_t i = 0; i < pages.size(); ++i) setRef(reinterpret_cast<Il2CppObject*>(newTextures), &texturesOut[oldPages + i], pages[i]);

    size_t next = count, replaced = 0;
    for (const auto& rec : records) {
        const uint16_t index = table[rec.code];
        const bool own = index != kNoGlyph && index < count &&
                         *reinterpret_cast<uint16_t*>(glyphBytes + index * kGlyphSize + kCharacter) == rec.code;
        const size_t i = own ? index : next++;
        if (own) ++replaced;
        uint8_t* g = glyphOut + i * kGlyphSize;
        std::memset(g, 0, kGlyphSize);
        *reinterpret_cast<uint16_t*>(g + kCharacter) = rec.code;
        std::memcpy(g + kBounds, rec.bounds, sizeof(rec.bounds));
        std::memcpy(g + kCropping, rec.cropping, sizeof(rec.cropping));
        *reinterpret_cast<float*>(g + kLeft) = rec.left;
        *reinterpret_cast<float*>(g + kWidth) = rec.width;
        *reinterpret_cast<float*>(g + kRight) = rec.right;
        *reinterpret_cast<float*>(g + kWidthWithBearings) = rec.left + rec.width + rec.right;
        *(g + kTextureIndex) = static_cast<uint8_t>(oldPages + rec.page);
        tableOut[rec.code] = static_cast<uint16_t>(i);
    }

    patch.font = a.gchandle_new(font, false);
    patch.glyphs = a.gchandle_new(reinterpret_cast<Il2CppObject*>(oldGlyphs), false);
    patch.table = a.gchandle_new(reinterpret_cast<Il2CppObject*>(oldTable), false);
    patch.textures = a.gchandle_new(reinterpret_cast<Il2CppObject*>(oldTextures), false);
    if (!patch.font || !patch.glyphs || !patch.table || !patch.textures) {
        freePatch(patch);
        return false;
    }
    setRef(font, &at<Il2CppObject*>(font, r.glyphs), reinterpret_cast<Il2CppObject*>(newGlyphs));
    setRef(font, &at<Il2CppObject*>(font, r.table), reinterpret_cast<Il2CppObject*>(newTable));
    setRef(font, &at<Il2CppObject*>(font, r.textures), reinterpret_cast<Il2CppObject*>(newTextures));
    if (metrics) {
        const Json* line = meta.get("lineSpacing");
        const Json* spacing = meta.get("spacing");
        if (line && line->kind == Json::Number && line->number > 0 && r.lineSpacing >= 0)
            at<int32_t>(font, r.lineSpacing) = static_cast<int32_t>(line->number);
        if (spacing && spacing->kind == Json::Number && std::isfinite(spacing->number) && r.spacing >= 0)
            at<float>(font, r.spacing) = static_cast<float>(spacing->number);
    }
    BL_INFO("%s: %s <- %s (scale %d, %zu replaced, %zu added, %zu page(s)%s)", owner.c_str(), label,
            path.substr(path.rfind('/') + 1).c_str(), scale, replaced, appended, pages.size(),
            skipped ? ", some glyph records skipped" : "");
    if (skipped) BL_WARN("%s: %s skipped %zu invalid or repeated glyph record(s)", owner.c_str(), label, skipped);
    return true;
}

} // namespace

void configureFontPacks(const std::string& root, const std::vector<ModSpec>& enabled) {
    std::lock_guard<std::mutex> lock(g_mutex);
    g_pending.clear();
    for (const auto& spec : enabled) {
        if (spec.entry != "@texture" && spec.entry != "@font") continue;
        const std::string base = root + "/" + spec.id;
        for (const char* folder : {"/content/Fonts", "/Content/Fonts", "/content/fonts"}) {
            if (directory(base + folder)) { g_pending.push_back({spec.id, base + folder}); break; }
        }
    }
    g_dirty.store(true, std::memory_order_release);
}

void tickFontPacks() {
    // As refs de textura do conteudo (TextureAssets) so ficam prontas com o conteudo assentado.
    if (!g_dirty.load(std::memory_order_acquire) || !contentSettled()) return;
    std::vector<Pack> packs;
    {
        std::lock_guard<std::mutex> lock(g_mutex);
        packs = g_pending;
        g_dirty.store(false, std::memory_order_release);
    }
    if (packs.empty() && g_patched.empty()) return;
    if (!refs().ok) return;
    restore();
    for (const auto& target : kTargets) {
        Il2CppObject* font = gameFont(target);
        if (!font) { if (!packs.empty()) BL_WARN("Font packs: FontAssets.%s is not loaded", target.label); continue; }
        // O de cima na lista ganha; arquivo que falha deixa o de baixo tentar.
        for (const auto& pack : packs) {
            const std::string path = fontFile(pack.fonts, target);
            Patched patch;
            if (!path.empty() && applyFont("Font pack " + pack.uid, target.label, path, font, patch, false)) {
                g_patched.push_back(std::move(patch));
                break;
            }
        }
    }
}

Il2CppObject* loadModFont(const std::string& path) {
    static std::map<std::string, Patched> fonts;   // So a thread do jogo acessa.
    auto& a = il2cpp::api();
    auto it = fonts.find(path);
    // Um arquivo que falhou nao tenta de novo (o mod pode pedir a cada quadro).
    if (it != fonts.end()) return it->second.font ? a.gchandle_get_target(it->second.font) : nullptr;
    if (!contentSettled() || !refs().ok) return nullptr;
    const Refs& r = refs();
    Il2CppObject* base = gameFont(kTargets[0]);
    if (!base || r.end <= static_cast<int32_t>(sizeof(Il2CppObject))) {
        BL_WARN("Mod font: FontAssets.MouseText is not loaded; cannot create %s", path.c_str());
        return nullptr;
    }

    // Os arrays da MouseText do jogo, nao os de um pacote de fonte: os dele
    // somem quando o pacote e desligado.
    auto* glyphs = at<Il2CppObject*>(base, r.glyphs);
    auto* table = at<Il2CppObject*>(base, r.table);
    auto* textures = at<Il2CppObject*>(base, r.textures);
    for (const auto& p : g_patched) {
        if (a.gchandle_get_target(p.font) != base) continue;
        glyphs = a.gchandle_get_target(p.glyphs);
        table = a.gchandle_get_target(p.table);
        textures = a.gchandle_get_target(p.textures);
    }
    Il2CppObject* font = a.object_new(r.font);
    if (!font) return nullptr;
    const size_t header = sizeof(Il2CppObject);
    std::memcpy(reinterpret_cast<char*>(font) + header, reinterpret_cast<char*>(base) + header, r.end - header);
    setRef(font, &at<Il2CppObject*>(font, r.glyphs), glyphs);
    setRef(font, &at<Il2CppObject*>(font, r.table), table);
    setRef(font, &at<Il2CppObject*>(font, r.textures), textures);

    const std::string file = path.substr(path.rfind('/') + 1);
    const std::string owner = "Mod font " + file.substr(0, file.size() - 5);
    Patched patch;
    if (!applyFont(owner, "new SpriteFont", path, font, patch, true)) {
        fonts[path] = {};
        return nullptr;
    }
    Il2CppObject* result = a.gchandle_get_target(patch.font);
    fonts[path] = std::move(patch);
    return result;
}

} // namespace bl::runtime
