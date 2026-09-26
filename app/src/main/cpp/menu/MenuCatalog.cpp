#include "menu/MenuCatalog.h"
#include "core/Log.h"
#include "il2cpp/Api.h"
#include "il2cpp/Resolver.h"
#include "il2cpp/Signature.h"
#include "content/common/GameRefs.h"
#include "content/buffs/ModBuffs.h"
#include "content/npcs/ModNpcs.h"

#include <cstring>
#include <functional>

namespace bl::runtime {

namespace {

std::vector<uint8_t> g_npcClass;
std::vector<std::u16string> g_buffNames;
std::vector<uint8_t> g_buffClass;

Il2CppArray* staticArray(Il2CppClass* c, const char* name) {
    FieldInfo* f = c ? il2cpp::findField(c, name) : nullptr;
    Il2CppArray* arr = nullptr;
    if (f) il2cpp::api().field_static_get_value(f, &arr);
    return arr;
}

bool flagAt(Il2CppArray* t, int i) {
    return t && i >= 0 && static_cast<uintptr_t>(i) < t->length && static_cast<const uint8_t*>(arrayData(t))[i];
}

/**
 * Cada entrada viva de um Dictionary<int, T> do jogo (ContentSamples), direto
 * no array de entradas — o mesmo caminho da classificacao de item (Cheats.cpp).
 */
bool forEachSample(Il2CppObject* dict, const std::function<void(int, Il2CppObject*)>& fn) {
    auto& a = il2cpp::api();
    if (!dict) return false;
    Il2CppClass* dc = a.object_get_class(dict);
    FieldInfo* fEntries = a.class_get_field_from_name(dc, "_entries");
    FieldInfo* fCount = a.class_get_field_from_name(dc, "_count");
    if (!fEntries || !fCount) return false;
    auto* entries = field<Il2CppArray*>(dict, static_cast<int32_t>(a.field_get_offset(fEntries)));
    const int32_t count = field<int32_t>(dict, static_cast<int32_t>(a.field_get_offset(fCount)));
    if (!entries) return false;
    Il2CppClass* ec = a.class_get_element_class(a.object_get_class(reinterpret_cast<Il2CppObject*>(entries)));
    FieldInfo* fKey = a.class_get_field_from_name(ec, "key");
    FieldInfo* fVal = a.class_get_field_from_name(ec, "value");
    FieldInfo* fHash = a.class_get_field_from_name(ec, "hashCode");
    if (!fKey || !fVal || !fHash) return false;
    uint32_t align = 0;
    const size_t stride = static_cast<size_t>(a.class_value_size(ec, &align));
    size_t oKey = a.field_get_offset(fKey), oVal = a.field_get_offset(fVal), oHash = a.field_get_offset(fHash);
    // Offset de campo de struct pode vir contando o cabecalho de objeto.
    if (oVal + sizeof(void*) > stride) {
        oKey -= sizeof(Il2CppObject);
        oVal -= sizeof(Il2CppObject);
        oHash -= sizeof(Il2CppObject);
    }
    const char* base = static_cast<const char*>(arrayData(entries));
    for (int32_t i = 0; i < count && static_cast<uintptr_t>(i) < entries->length; ++i) {
        const char* e = base + static_cast<size_t>(i) * stride;
        if (*reinterpret_cast<const int32_t*>(e + oHash) < 0) continue;
        fn(*reinterpret_cast<const int32_t*>(e + oKey), *reinterpret_cast<Il2CppObject* const*>(e + oVal));
    }
    return true;
}

void buildNpcClasses() {
    using namespace il2cpp;
    auto& a = api();
    Il2CppClass* cs = findClass({"Terraria.ID", "ContentSamples", {}});
    Il2CppClass* npc = findClass({"Terraria", "NPC", {}});
    Il2CppClass* sets = findClass({"Terraria.ID", "NPCID", "Sets"});
    FieldInfo* fDict = cs ? findField(cs, "NpcsByNetId") : nullptr;
    Il2CppObject* dict = nullptr;
    if (fDict) a.field_static_get_value(fDict, &dict);
    const int32_t oTown = npc ? fieldOffset(npc, "townNPC") : -1;
    const int32_t oBoss = npc ? fieldOffset(npc, "boss") : -1;
    const int32_t oFriendly = npc ? fieldOffset(npc, "friendly") : -1;
    const int32_t oDamage = npc ? fieldOffset(npc, "damage") : -1;
    const int32_t oCatch = npc ? fieldOffset(npc, "catchItem") : -1;
    Il2CppArray* critter = staticArray(sets, "CountsAsCritter");
    if (!dict || oTown < 0 || oBoss < 0 || oFriendly < 0 || oDamage < 0 || oCatch < 0) {
        BL_WARN("menu: amostras de NPC indisponiveis; os NPCs ficam sem subcategoria");
        return;
    }
    g_npcClass.assign(static_cast<size_t>(npcTypeCount()), kNpcOther);
    forEachSample(dict, [&](int id, Il2CppObject* n) {
        if (!n || id <= 0 || static_cast<size_t>(id) >= g_npcClass.size()) return;
        uint8_t c = kNpcOther;
        if (field<uint8_t>(n, oTown)) c = kNpcTown;
        else if (flagAt(critter, id) || field<int16_t>(n, oCatch) > 0) c = kNpcCritter;
        else if (field<uint8_t>(n, oBoss)) c = kNpcBoss;
        else if (!field<uint8_t>(n, oFriendly) && field<int32_t>(n, oDamage) > 0) c = kNpcMonster;
        g_npcClass[static_cast<size_t>(id)] = c;
    });
}

std::u16string toU16(Il2CppString* s) {
    if (!s || s->length <= 0) return {};
    return std::u16string(reinterpret_cast<const char16_t*>(s->chars), static_cast<size_t>(s->length));
}

void buildBuffs() {
    using namespace il2cpp;
    auto& a = api();
    Il2CppClass* lang = findClass({"Terraria", "Lang", {}});
    const MethodInfo* nameOf = lang ? findMethodBySignature(
        lang, parseSignature("string GetBuffName(int id)")) : nullptr;
    Il2CppClass* main = findClass({"Terraria", "Main", {}});
    Il2CppClass* sets = findClass({"Terraria.ID", "BuffID", "Sets"});
    if (!nameOf || !main) {
        BL_WARN("menu: Lang.GetBuffName indisponivel; a aba de buffs fica vazia");
        return;
    }
    Il2CppArray* debuff = staticArray(main, "debuff");
    Il2CppArray* vanityPet = staticArray(main, "vanityPet");
    Il2CppArray* lightPet = staticArray(main, "lightPet");
    Il2CppArray* noTime = staticArray(main, "buffNoTimeDisplay");
    Il2CppArray* wellFed = staticArray(sets, "IsWellFed");
    Il2CppArray* fed = staticArray(sets, "IsFedState");
    Il2CppArray* flask = staticArray(sets, "IsAFlaskBuff");

    const int total = buffTypeCount();
    g_buffNames.assign(static_cast<size_t>(total), std::u16string());
    g_buffClass.assign(static_cast<size_t>(total), kBuffOther);
    for (int id = 1; id < total; ++id) {
        int t = id;
        void* args[1] = {&t};
        Il2CppObject* exc = nullptr;
        Il2CppObject* r = a.runtime_invoke(nameOf, nullptr, args, &exc);
        if (!exc) g_buffNames[static_cast<size_t>(id)] = toU16(reinterpret_cast<Il2CppString*>(r));
        uint8_t c;
        if (flagAt(wellFed, id) || flagAt(fed, id)) c = kBuffFood;
        else if (flagAt(flask, id)) c = kBuffFlask;
        else if (flagAt(vanityPet, id) || flagAt(lightPet, id)) c = kBuffPet;
        else if (flagAt(debuff, id)) c = kBuffDebuff;
        else if (flagAt(noTime, id)) c = kBuffSummon;
        else c = kBuffGood;
        g_buffClass[static_cast<size_t>(id)] = c;
    }
}

} // namespace

void buildMenuCatalogExtras() {
    buildNpcClasses();
    buildBuffs();
    int perNpc[5] = {}, perBuff[7] = {};
    for (uint8_t c : g_npcClass) if (c < 5) ++perNpc[c];
    for (uint8_t c : g_buffClass) if (c < 7) ++perBuff[c];
    BL_INFO("menu: NPCs | chefe %d, monstro %d, morador %d, criatura %d, outros %d; "
            "buffs %zu | bom %d, debuff %d, comida %d, frasco %d, pet %d, invocacao %d",
            perNpc[1], perNpc[2], perNpc[3], perNpc[4], perNpc[0], g_buffNames.size(),
            perBuff[1], perBuff[2], perBuff[3], perBuff[4], perBuff[5], perBuff[6]);
}

const std::vector<uint8_t>& npcClasses() { return g_npcClass; }
const std::vector<std::u16string>& buffNames() { return g_buffNames; }
const std::vector<uint8_t>& buffClasses() { return g_buffClass; }

} // namespace bl::runtime
