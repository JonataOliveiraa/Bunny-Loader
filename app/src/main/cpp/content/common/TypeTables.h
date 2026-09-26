#pragma once
#include <cstdint>
#include <vector>

#include "il2cpp/Types.h"

namespace bl::runtime {

/** Classe do jogo que guarda tabelas por tipo; `nested` "" quando nao e aninhada. */
struct TableClass {
    const char* ns;
    const char* name;
    const char* nested;
    const char* nested2 = "";   // segundo nivel: TileID.Sets.Conversion
};

/**
 * As tabelas do jogo indexadas por um tipo de conteudo (item, projetil...).
 *
 * Toda tabela dessas nasce com o Count de fabrica do tipo (ItemID.Count,
 * ProjectileID.Count) e o jogo le `tabela[tipo]` sem conferir: esta build do
 * IL2CPP NAO confere limite de array, entao um tipo novo le ou ESCREVE alem
 * do fim, sem erro. O ExMod do TL Pro aumenta as tabelas a mao, uma a uma;
 * aqui elas sao ACHADAS: em cada classe da lista, todo array estatico com
 * exatamente o Count de fabrica e uma delas. A lista de classes de cada tipo
 * foi conferida contra toda alocacao desse tamanho na libil2cpp (o `mov` da
 * constante antes de um new[]).
 *
 * So nas classes da lista, e nao no jogo inteiro: nelas o jogo ja passou
 * quando chegamos. E ler campo estatico NAO roda o construtor estatico, entao
 * uma tabela ainda nula fica pendente e e aumentada no quadro em que aparecer.
 *
 * So na thread do jogo (o jogo troca essas tabelas nela).
 */
class TypeTables {
public:
    /** Para reaplicar o que e nosso numa tabela que o jogo refez. */
    using Regrown = void (*)(FieldInfo* table, int size);

    /** Com que valor o tipo novo nasce em cada tabela. */
    enum class Fill {
        First,   // o do [0] (o "nada" de item, projetil, buff)
        Mode,    // o MAIS COMUM da tabela: o padrao com que a fabrica a criou
    };

    /**
     * `Fill::Mode` e o dos tiles: o [0] e a terra, e copiar faria o bloco de
     * mod ser terra para a grama e a corrupcao. O mais comum e o padrao da
     * tabela — false no tileSolid, -1 no tileGlowMask (com 0, todo tile de mod
     * desenharia o brilho 0), o material comum no TileMaterials — que e o que
     * o tModLoader consegue recriando o TileID.Sets pela fabrica.
     */
    TypeTables(const char* what, int vanillaCount, std::vector<TableClass> classes, Fill fill = Fill::First);

    /**
     * Aumenta as tabelas de `from` para `to` posicoes; na primeira vez, acha
     * todas. Devolve quantas cresceram, ou -1 se nenhuma foi achada.
     */
    int grow(int from, int to);

    /** Tabela que era nula e o jogo acabou de criar. A cada quadro. */
    void checkPending(int size);

    /** Tabela que o jogo refez com o tamanho de fabrica (troca de idioma...). */
    void watch(int size, Regrown onRegrown);

    /** Novo array com `newLength` posicoes: copia e enche o resto com o [0]. */
    static Il2CppArray* growArray(Il2CppArray* old, uintptr_t newLength, Fill fill = Fill::First);

    /** Os campos achados (as tabelas deste tipo). */
    const std::vector<FieldInfo*>& tables() const { return tables_; }

    /**
     * Copia com `newLength` posicoes (maior ou menor): o que cabe vem de
     * `old`, o resto fica no padrao do tipo (0/null). O `cloneResized` do JS.
     */
    static Il2CppArray* resizedCopy(Il2CppArray* old, uintptr_t newLength);

    /**
     * Tabela de tipo de valor que mora num OBJETO (campo em `offset`), e nao
     * num estatico: aumenta com o resto ZERADO, se ainda tem `vanillaCount`.
     */
    static void growInstanceTable(Il2CppObject* obj, int32_t offset, int vanillaCount, int size,
                                  const char* what);

private:
    void find(uintptr_t size);

    const char* what_;
    int vanillaCount_;
    Fill fill_ = Fill::First;
    std::vector<TableClass> classes_;
    bool searched_ = false;
    std::vector<FieldInfo*> tables_;
    std::vector<FieldInfo*> pending_;
};

} // namespace bl::runtime
