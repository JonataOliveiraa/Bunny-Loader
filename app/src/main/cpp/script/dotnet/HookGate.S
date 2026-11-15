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
    add     x16, x16, x17, lsl #6
    add     x11, x16, #44
    ldar    w10, [x11]
    tbnz    w10, #1, 9f
    ldr     w11, [x16, #24]
    cmp     w11, #8
    b.eq    7f
    b.hi    9f
    adr     x12, 1f
    add     x12, x12, x11, lsl #3
    br      x12
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
7:
    ldr     x9, [x16, #48]
2:
    tbnz    w10, #2, 34f
    cbz     x9, 5f
    ldrsw   x11, [x16, #16]
    tbnz    x11, #63, 3f
    ldr     x9, [x9, x11]
    cbz     x9, 5f
3:
    ldrsw   x11, [x16, #20]
    tbnz    x11, #63, 4f
    ldr     w12, [x16, #28]
    cmp     w12, #1
    b.eq    31f
    ldr     w12, [x9, x11]
    b       32f
31:
    ldrb    w12, [x9, x11]
32:
    ldr     x11, [x16, #32]
    cbz     x11, 33f
    ldr     w9, [x16, #40]
    cmp     w12, w9
    b.hs    4f
    lsr     w9, w12, #3
    ldrb    w9, [x11, w9, uxtw]
    and     w12, w12, #7
    lsr     w12, w9, w12
    and     w12, w12, #1
33:
    cbz     w12, 5f
4:
    tbnz    w10, #0, 8f
9:
    ldr     x17, [x16]
    br      x17
5:
    tbnz    w10, #0, 9b
8:
    ldr     x17, [x16, #8]
    ldr     x17, [x17]
    br      x17
34:
    mov     w12, w9
    ldr     w11, [x16, #28]
    cmp     w11, #1
    b.ne    32b
    and     w12, w12, #0xff
    b       32b
    .size   bl_gate_common, . - bl_gate_common

    .p2align 3
    .globl  bl_gate_thunks
    .hidden bl_gate_thunks
bl_gate_thunks:
    .rept 256
    adr     x17, .
    b       bl_gate_common
    .endr

#endif

    .section .note.GNU-stack, "", %progbits
