#include <dlfcn.h>
#include <stdatomic.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <sys/syscall.h>
#include <time.h>
#include <unistd.h>

#define MAX 200000
typedef struct { void* method; uint64_t self, total; int tid; } Row;
static Row* rows;
static atomic_int count;
static char* (*full_name)(void*, int);
static __thread struct { void* m; uint64_t start, child; } stack[64];
static __thread int depth;

static uint64_t now(void) { struct timespec t; clock_gettime(CLOCK_MONOTONIC, &t); return t.tv_sec * 1000000000ull + t.tv_nsec; }

static void begin(void* p, void* m) {
    if (depth < 64) { stack[depth].m = m; stack[depth].start = now(); stack[depth].child = 0; }
    depth++;
}
static void finish(void* m) {
    depth--;
    if (depth >= 64 || depth < 0) { if (depth < 0) depth = 0; return; }
    uint64_t total = now() - stack[depth].start;
    uint64_t self = total - stack[depth].child;
    if (depth > 0 && depth <= 64) stack[depth - 1].child += total;
    int i = atomic_fetch_add(&count, 1);
    if (i < MAX) { rows[i].method = m; rows[i].self = self; rows[i].total = total; rows[i].tid = gettid(); }
}
static void done(void* p, void* m, void* j) { finish(m); }
static void failed(void* p, void* m) { finish(m); }

__attribute__((visibility("default"))) void jitprof_start(void) {
    void* lib = dlopen("libmonosgen-2.0.so", RTLD_NOLOAD | RTLD_NOW);
    if (!lib) return;
    rows = calloc(MAX, sizeof(Row));
    full_name = dlsym(lib, "mono_method_full_name");
    void* (*create)(void*) = dlsym(lib, "mono_profiler_create");
    void (*setb)(void*, void*) = dlsym(lib, "mono_profiler_set_jit_begin_callback");
    void (*setd)(void*, void*) = dlsym(lib, "mono_profiler_set_jit_done_callback");
    void (*setf)(void*, void*) = dlsym(lib, "mono_profiler_set_jit_failed_callback");
    void* h = create(NULL);
    setb(h, begin); setf(h, failed); setd(h, done);
}

__attribute__((visibility("default"))) void jitprof_dump(const char* path) {
    int n = atomic_load(&count);
    if (n > MAX) n = MAX;
    FILE* f = fopen(path, "w");
    if (!f) return;
    for (int i = 0; i < n; i++) {
        char* name = full_name(rows[i].method, 1);
        fprintf(f, "%.3f\t%.3f\t%d\t%s\n", rows[i].self / 1e6, rows[i].total / 1e6, rows[i].tid, name);
        free(name);
    }
    fclose(f);
}
