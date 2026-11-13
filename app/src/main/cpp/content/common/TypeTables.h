#pragma once
#include <cstdint>
#include <string>
#include <vector>

#include "il2cpp/Types.h"

namespace bl::runtime {

struct TableClass {
    const char* ns;
    const char* name;
    const char* nested;
    const char* nested2 = "";
    std::vector<std::string> skip = {};
};

class TypeTables {
public:
    using Regrown = void (*)(FieldInfo* table, int size);

    enum class Fill {
        First,
        Mode,
    };

    TypeTables(const char* what, int vanillaCount, std::vector<TableClass> classes, Fill fill = Fill::First);

    int grow(int from, int to);

    void checkPending(int size);

    void watch(int size, Regrown onRegrown);

    static Il2CppArray* growArray(Il2CppArray* old, uintptr_t newLength, Fill fill = Fill::First);

    const std::vector<FieldInfo*>& tables() const { return tables_; }

    static Il2CppArray* resizedCopy(Il2CppArray* old, uintptr_t newLength);

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

}
