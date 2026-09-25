#pragma once
#include "script/ScriptEngine.h"

#if BL_HAVE_QUICKJS
#include "quickjs.h"
#include "il2cpp/Types.h"
#include <cstddef>
#include <cstdint>
#include <string>

namespace bl::script {

/**
 * O SISTEMA DE TIPOS da ponte, num lugar so.
 *
 * Antes cada ponto de contato com o jogo tinha o proprio if-else de nomes de
 * tipo: campo de instancia, campo estatico, elemento de array, argumento de
 * metodo, retorno de metodo, argumento de hook. Seis copias divergentes — um
 * enum funcionava como argumento e explodia como campo, um float saia certo do
 * campo e errado do array. `TypeDesc` resolve o tipo UMA vez; `readAt`/
 * `writeAt` sao os unicos que tocam a memoria do jogo.
 */
enum class Prim {
    Void, Bool, I8, U8, I16, U16, Char, I32, U32, I64, U64, F32, F64,
    String, Array, Object, Struct,
};

struct TypeDesc {
    Prim prim = Prim::Object;
    Il2CppClass* cls = nullptr;   // classe do tipo (struct, enum, objeto)
    size_t size = sizeof(void*);  // bytes que o valor ocupa em linha
    bool byValue = false;         // mora em linha; senao e um ponteiro
    bool isEnum = false;          // `prim` ja e o tipo SUBJACENTE
    bool byRef = false;           // `ref`/`out`: vira um Ref no JS (Ref.h)
    std::string name;             // nome do CLR, para mensagens de erro

    /**
     * `System.Nullable<T>` (o `float?` do C#): um struct {hasValue, value}.
     * No JS ele e `null` ou o proprio T — nunca um struct com esses dois
     * campos, que ninguem escreveria de proposito. `inner` e o T.
     */
    const TypeDesc* inner = nullptr;
    size_t hasValueOffset = 0;    // nos DADOS do struct, sem cabecalho
    size_t valueOffset = 0;
    bool nullable() const { return inner != nullptr; }
};

/**
 * O tipo, resolvido uma vez por Il2CppType e guardado.
 *
 * Sem o cache, cada `item.damage` pagava um il2cpp_type_get_name (malloc +
 * free) e uma fileira de comparacoes de string. A referencia devolvida vale
 * para sempre: os tipos do jogo nao somem, e o mapa nunca remove.
 *
 * So chamar com o motor JS travado (JsLock) — e o que protege o mapa.
 */
const TypeDesc& describe(const Il2CppType* t);

/**
 * Le/escreve um valor de tipo `d` no endereco `p`.
 *
 * `owner` e quem hospeda a memoria (o objeto, o array, o struct de fora). So
 * importa quando o resultado e uma VISTA — um GameStruct aponta para dentro do
 * dono, entao precisa segurar o dono vivo enquanto existir.
 */
JSValue readAt(JSContext* ctx, void* p, const TypeDesc& d, JSValueConst owner);
int writeAt(JSContext* ctx, void* p, const TypeDesc& d, JSValueConst v);

/**
 * Vista sobre um struct que vive dentro de `owner`. Escrever altera o jogo.
 * `size` = 0 pergunta o tamanho ao IL2CPP; passe o do TypeDesc quando tiver.
 */
JSValue makeStructView(JSContext* ctx, Il2CppClass* cls, void* data, JSValueConst owner,
                       size_t size = 0);

/** Copia independente. Escrever NAO volta pro jogo — semantica de valor do C#. */
JSValue makeStructCopy(JSContext* ctx, Il2CppClass* cls, const void* src, size_t n);

/**
 * Campo ESTATICO de struct (Main.screenPosition). O IL2CPP so entrega copia do
 * bloco de estaticos, entao a vista guarda o FieldInfo e devolve o valor ao
 * jogo depois de cada escrita.
 */
JSValue makeStaticStruct(JSContext* ctx, Il2CppClass* cls, FieldInfo* f, size_t n);

/** Dados crus de um struct vindo do JS (GameStruct ou objeto encaixotado). */
void* structDataOf(JSValueConst v, Il2CppClass** outCls, size_t* outSize);

/**
 * Offset de campo -> offset dentro dos DADOS de um struct.
 *
 * O IL2CPP conta o offset a partir do inicio do OBJETO, cabecalho incluso —
 * vale tambem para value type, porque a versao encaixotada tem cabecalho. Uma
 * vista aponta para os dados crus, sem cabecalho, entao desconta.
 */
size_t structFieldOffset(Il2CppClass* cls, size_t fieldOffset);

/** Registra a classe GameStruct. Chamado uma vez, por installBindings. */
void installStructClass(JSContext* ctx);

/**
 * Struct so de floats (ou so de doubles), ate 4 — o "homogeneous float
 * aggregate" do AAPCS, que viaja em d0-d3 em vez dos registradores inteiros.
 * Vector2 e exatamente isso. @return quantidade, ou 0 se nao for HFA.
 */
int hfaOf(Il2CppClass* cls, bool* isDouble);

} // namespace bl::script
#endif
