#pragma once
#include "script/bridge/Value.h"

#if BL_HAVE_QUICKJS
#include "quickjs.h"
#include <string>

namespace bl::script {

/**
 * O que um NOME significa numa classe do jogo, resolvido uma vez e guardado.
 *
 * `self.statLife` num hook de Player.Update roda todo quadro. Sem cache, cada
 * leitura convertia o atomo em string, varria os campos de Player (centenas)
 * com strcmp, subia a hierarquia e ainda perguntava o nome do tipo ao IL2CPP.
 * Com o cache e uma consulta de hash por (classe, atomo) e a leitura em si.
 *
 * A chave e o JSAtom, nao a string: o QuickJS ja internou o nome, e comparar
 * inteiro e o que torna o caminho quente barato.
 */
enum class Space : uint8_t {
    Static,    // Terraria.Main.x      — campo estatico, get_/set_ estatico
    Instance,  // player.x             — offset desde o inicio do objeto
    Struct,    // player.position.X    — offset desde os DADOS do struct
};

struct Member {
    bool proto = false;       // e nome do nosso prototipo (new, toString): delega ao JS
    bool signature = false;   // o nome tem '(' — so `method` pode valer
    FieldInfo* field = nullptr;
    size_t offset = 0;
    const TypeDesc* type = nullptr;       // tipo do campo (describe e perpetuo)
    const MethodInfo* getter = nullptr;   // get_<nome>, 0 parametros
    const MethodInfo* setter = nullptr;   // set_<nome>, 1 parametro
    const MethodInfo* method = nullptr;   // por assinatura; ou, em Static, nome de overload unico
    int overloads = 0;                    // Static, nome puro: quantos metodos tem o nome
    Il2CppClass* nested = nullptr;        // Static: classe aninhada (SpriteFont.Glyph)
    mutable bool classReady = false;      // Static, campo: construtor estatico ja garantido
};

/**
 * Resolve (ou devolve do cache) o membro `atom` de `cls`.
 *
 * `protoId` e a classe JS do objeto: um nome que exista no prototipo dela
 * (`new`, `toString`, `hook`) e marcado `proto` e fica com o JS — o mesmo que
 * o exotic fazia antes, olhando o prototipo primeiro a cada acesso.
 *
 * So com o motor travado (JsLock). A referencia vale para sempre.
 */
const Member& member(JSContext* ctx, Il2CppClass* cls, JSAtom atom, Space space,
                     JSClassID protoId);

/** Le a propriedade do prototipo da classe JS `id` (o caminho `proto`). */
JSValue protoGet(JSContext* ctx, JSClassID id, JSAtom atom);

/** O atomo como string. So para mensagens de erro — fora do caminho quente. */
std::string atomName(JSContext* ctx, JSAtom atom);

} // namespace bl::script
#endif
