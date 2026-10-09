#pragma once
#include <cstdint>
struct alignas(16) RegisterFrame {
    uint64_t general[31];
    uint64_t flags;
    alignas(16) uint8_t vectors[32][16];
};
static_assert(sizeof(RegisterFrame) == 768);

#define BL_ARM64_SAVE_FRAME \
"sub sp, sp, #768\n" \
"stp x0, x1, [sp, #0]\n" \
"stp x2, x3, [sp, #16]\n" \
"stp x4, x5, [sp, #32]\n" \
"stp x6, x7, [sp, #48]\n" \
"stp x8, x9, [sp, #64]\n" \
"stp x10, x11, [sp, #80]\n" \
"stp x12, x13, [sp, #96]\n" \
"stp x14, x15, [sp, #112]\n" \
"stp x16, x17, [sp, #128]\n" \
"stp x18, x19, [sp, #144]\n" \
"stp x20, x21, [sp, #160]\n" \
"stp x22, x23, [sp, #176]\n" \
"stp x24, x25, [sp, #192]\n" \
"stp x26, x27, [sp, #208]\n" \
"stp x28, x29, [sp, #224]\n" \
"str x30, [sp, #240]\n" \
"mrs x9, nzcv\n" \
"str x9, [sp, #248]\n" \
"stp q0, q1, [sp, #256]\n" \
"stp q2, q3, [sp, #288]\n" \
"stp q4, q5, [sp, #320]\n" \
"stp q6, q7, [sp, #352]\n" \
"stp q8, q9, [sp, #384]\n" \
"stp q10, q11, [sp, #416]\n" \
"stp q12, q13, [sp, #448]\n" \
"stp q14, q15, [sp, #480]\n" \
"stp q16, q17, [sp, #512]\n" \
"stp q18, q19, [sp, #544]\n" \
"stp q20, q21, [sp, #576]\n" \
"stp q22, q23, [sp, #608]\n" \
"stp q24, q25, [sp, #640]\n" \
"stp q26, q27, [sp, #672]\n" \
"stp q28, q29, [sp, #704]\n" \
"stp q30, q31, [sp, #736]\n"

#define BL_ARM64_RESTORE_FRAME \
"ldp q0, q1, [sp, #256]\n" \
"ldp q2, q3, [sp, #288]\n" \
"ldp q4, q5, [sp, #320]\n" \
"ldp q6, q7, [sp, #352]\n" \
"ldp q8, q9, [sp, #384]\n" \
"ldp q10, q11, [sp, #416]\n" \
"ldp q12, q13, [sp, #448]\n" \
"ldp q14, q15, [sp, #480]\n" \
"ldp q16, q17, [sp, #512]\n" \
"ldp q18, q19, [sp, #544]\n" \
"ldp q20, q21, [sp, #576]\n" \
"ldp q22, q23, [sp, #608]\n" \
"ldp q24, q25, [sp, #640]\n" \
"ldp q26, q27, [sp, #672]\n" \
"ldp q28, q29, [sp, #704]\n" \
"ldp q30, q31, [sp, #736]\n" \
"ldr x9, [sp, #248]\n" \
"msr nzcv, x9\n" \
"ldp x0, x1, [sp, #0]\n" \
"ldp x2, x3, [sp, #16]\n" \
"ldp x4, x5, [sp, #32]\n" \
"ldp x6, x7, [sp, #48]\n" \
"ldp x8, x9, [sp, #64]\n" \
"ldp x10, x11, [sp, #80]\n" \
"ldp x12, x13, [sp, #96]\n" \
"ldp x14, x15, [sp, #112]\n" \
"ldp x16, x17, [sp, #128]\n" \
"ldp x18, x19, [sp, #144]\n" \
"ldp x20, x21, [sp, #160]\n" \
"ldp x22, x23, [sp, #176]\n" \
"ldp x24, x25, [sp, #192]\n" \
"ldp x26, x27, [sp, #208]\n" \
"ldp x28, x29, [sp, #224]\n" \
"ldr x30, [sp, #240]\n" \
"add sp, sp, #768\n"

