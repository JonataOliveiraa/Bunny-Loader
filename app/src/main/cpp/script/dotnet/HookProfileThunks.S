#if defined(__aarch64__)

    .text

.macro BL_HP_COMMON name, pre
    .p2align 2
    .globl  \name
    .hidden \name
    .type   \name, %function
\name:
    .cfi_startproc
    sub     sp, sp, #352
    .cfi_def_cfa_offset 352
    stp     x29, x30, [sp, #128]
    .cfi_offset w29, -224
    .cfi_offset w30, -216
    add     x29, sp, #128
    .cfi_def_cfa w29, 224
    stp     x0, x1, [sp, #144]
    stp     x2, x3, [sp, #160]
    stp     x4, x5, [sp, #176]
    stp     x6, x7, [sp, #192]
    stp     x8, x17, [sp, #208]
    stp     d0, d1, [sp, #224]
    stp     d2, d3, [sp, #240]
    stp     d4, d5, [sp, #256]
    stp     d6, d7, [sp, #272]
    add     x9, sp, #352
    ldp     x10, x11, [x9, #0]
    stp     x10, x11, [sp, #0]
    ldp     x10, x11, [x9, #16]
    stp     x10, x11, [sp, #16]
    ldp     x10, x11, [x9, #32]
    stp     x10, x11, [sp, #32]
    ldp     x10, x11, [x9, #48]
    stp     x10, x11, [sp, #48]
    ldp     x10, x11, [x9, #64]
    stp     x10, x11, [sp, #64]
    ldp     x10, x11, [x9, #80]
    stp     x10, x11, [sp, #80]
    ldp     x10, x11, [x9, #96]
    stp     x10, x11, [sp, #96]
    ldp     x10, x11, [x9, #112]
    stp     x10, x11, [sp, #112]
    mov     x0, x17
    add     x1, sp, #336
    add     x2, sp, #352
    bl      \pre
    mov     x9, x0
    ldp     x0, x1, [sp, #144]
    ldp     x2, x3, [sp, #160]
    ldp     x4, x5, [sp, #176]
    ldp     x6, x7, [sp, #192]
    ldr     x8, [sp, #208]
    ldp     d0, d1, [sp, #224]
    ldp     d2, d3, [sp, #240]
    ldp     d4, d5, [sp, #256]
    ldp     d6, d7, [sp, #272]
    blr     x9
    stp     x0, x1, [sp, #288]
    stp     d0, d1, [sp, #304]
    stp     d2, d3, [sp, #320]
    ldr     x0, [sp, #336]
    bl      bl_hp_post
    ldp     x0, x1, [sp, #288]
    ldp     d0, d1, [sp, #304]
    ldp     d2, d3, [sp, #320]
    ldp     x29, x30, [sp, #128]
    add     sp, sp, #352
    ret
    .cfi_endproc
    .size   \name, . - \name
.endm

BL_HP_COMMON bl_hp_enter_common, bl_hp_enter_pre
BL_HP_COMMON bl_hp_orig_common, bl_hp_orig_pre

    .p2align 3
    .globl  bl_hp_enter_thunks
    .hidden bl_hp_enter_thunks
bl_hp_enter_thunks:
    .rept 2048
    adr     x17, .
    b       bl_hp_enter_common
    .endr

    .p2align 3
    .globl  bl_hp_orig_thunks
    .hidden bl_hp_orig_thunks
bl_hp_orig_thunks:
    .rept 2048
    adr     x17, .
    b       bl_hp_orig_common
    .endr

#endif

    .section .note.GNU-stack, "", %progbits
