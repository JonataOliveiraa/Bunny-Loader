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
    const MethodInfo* method = nullptr;   // por assinatura; ou nome puro de metodo unico
    int overloads = 0;                    // nome puro: quantos metodos tem o nome
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

/**
 * O indexador C# (`this[int]`) de um struct do jogo: `proj.ai[0]`.
 *
 * O Terraria do celular trocou varios arrays por structs de tamanho fixo
 * (`Projectile.ai` e `localAI` sao Float_FixedArray_3, `oldPos` e
 * Vector2_DynamicArray_120). O codigo do tML/ExMod escreve `proj.ai[0]`, que
 * sem isto dava undefined.
 *
 * Dois jeitos, decididos uma vez por classe:
 *   - CAMPOS: o struct e N campos iguais em sequencia (`val0..val2`,
 *     `data0..data119`) e o get_Item devolve o tipo deles. `[i]` le o campo
 *     `i` direto, com limite conferido aqui — sem chamar o jogo, que nao
 *     confere nada, e o elemento struct sai como VISTA (`oldPos[3].X = 0`).
 *   - METODOS: o resto (BitsByte, Bits64, DictionaryIntArray) chama o
 *     get_Item/set_Item do jogo.
 */
struct Indexer {
    const TypeDesc* elem = nullptr;         // CAMPOS: tipo do elemento
    size_t offset = 0;                      // CAMPOS: elemento 0, nos DADOS do struct
    size_t stride = 0;
    int count = 0;                          // CAMPOS: quantos cabem (> 0 = modo CAMPOS)
    const MethodInfo* get = nullptr;        // METODOS: get_Item(int)
    const MethodInfo* set = nullptr;        // METODOS: set_Item(int, T); nulo = so leitura
    const MethodInfo* length = nullptr;     // METODOS: get_Length(), se houver (limite)
    bool fields() const { return count > 0; }
};

/** O indexador de `cls`, ou nullptr se o struct nao tem `this[int]`. Guardado. */
const Indexer* indexerOf(Il2CppClass* cls);

/**
 * O indice que o atomo representa, sem passar por texto. -1 = nao e indice.
 * (`arr[3]`, `proj.ai[0]`.)
 */
int64_t indexOf(JSContext* ctx, JSAtom atom);

/** Le a propriedade do prototipo da classe JS `id` (o caminho `proto`). */
JSValue protoGet(JSContext* ctx, JSClassID id, JSAtom atom);

/** O atomo como string. So para mensagens de erro — fora do caminho quente. */
std::string atomName(JSContext* ctx, JSAtom atom);

} // namespace bl::script
#endif
