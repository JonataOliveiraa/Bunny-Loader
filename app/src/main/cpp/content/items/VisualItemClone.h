#pragma once

namespace bl::runtime {

using VisualItemCopyHook = void (*)(void* source, void* destination);

void installVisualItemClone();

void setVisualItemCopyHook(VisualItemCopyHook hook);

}
