#if defined(__aarch64__)

    .text

    .p2align 2
    .globl  bl_gate_common
    .hidden bl_gate_common
    .type   bl_gate_common, %function
bl_gate_common:
    adrp    x16, bl_gate_thunks
    add     x16, x16, :lo12:bl_gate_thunks
    sub     x17, x17, x16
    lsr     x17, x17, #3
    adrp    x16, bl_gate_data
    add     x16, x16, :lo12:bl_gate_data
    add     x16, x16, x17, lsl #5
    ldr     w10, [x16, #24]
    cmp     w10, #7
    b.hi    9f
    adr     x11, 1f
    add     x11, x11, x10, lsl #3
    br      x11
1:
    mov     x9, x0
    b       2f
    mov     x9, x1
    b       2f
    mov     x9, x2
    b       2f
    mov     x9, x3
    b       2f
    mov     x9, x4
    b       2f
    mov     x9, x5
    b       2f
    mov     x9, x6
    b       2f
    mov     x9, x7
    b       2f
2:
    cbz     x9, 8f
    ldrsw   x10, [x16, #16]
    tbnz    x10, #63, 3f
    ldr     x9, [x9, x10]
    cbz     x9, 8f
3:
    ldrsw   x10, [x16, #20]
    ldr     w11, [x16, #28]
    cmp     w11, #1
    b.eq    4f
    ldr     w12, [x9, x10]
    cbz     w12, 8f
    b       9f
4:
    ldrb    w12, [x9, x10]
    cbz     w12, 8f
9:
    ldr     x17, [x16]
    br      x17
8:
    ldr     x17, [x16, #8]
    ldr     x17, [x17]
    br      x17
    .size   bl_gate_common, . - bl_gate_common

    .p2align 3
    .globl  bl_gate_thunks
    .hidden bl_gate_thunks
bl_gate_thunks:
    .rept 64
    adr     x17, .
    b       bl_gate_common
    .endr

#endif

    .section .note.GNU-stack, "", %progbits
