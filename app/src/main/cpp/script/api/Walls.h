#pragma once
#include "script/bridge/ScriptEngine.h"

#if BL_HAVE_QUICKJS
#include "quickjs.h"

namespace bl::script {

void installWallsApi(JSContext* ctx, JSValue bl);

} // namespace bl::script
#endif
