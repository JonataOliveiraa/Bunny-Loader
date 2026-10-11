// O trampolim da chamada guardada (ver GuardedCall.h).
//
// int bl_eh_try(void (*body)(void*), void* arg, void* guard)
//
// Salva os registradores preservados (x19-x28, d8-d15, x29, x30) no proprio
// frame e chama body(arg). Volta 0 se body voltou.
//
// O frame declara a personalidade bl_eh_personality (GuardedCall.cpp). Para
// ela achar o frame sem thread_local (emulado com o minSdk 24: uma chamada a
// __emutls_get_address por chamada ao jogo, medido ~50 ns no MuMu), o
// trampolim grava uma MARCA no frame:
//
//     [sp+160] = kMarkMagic ^ (sp+160)     o valor depende do endereco
//     [sp+168] = guard                     onde a personalidade escreve
//
// e a apaga ao sair, pelos dois caminhos: marca que sobrasse na pilha morta
// seria achada depois. A personalidade sobe a pilha a partir do proprio frame
// ate a primeira marca: e a do trampolim mais interno, o que esta tratando.
// Ela chama bl_eh_land(marca - 160), que poe o sp de volta e sai pelo mesmo
// epilogo com 1: os registradores preservados voltam os de quem chamou
// bl_eh_try, e para ele e so uma funcao que devolveu 1.
//
// kMarkMagic tem de ser o mesmo de GuardedCall.cpp: 0x626c5f65685f6d6b.

#if defined(__aarch64__)

    .text
    .p2align 2
    .globl  bl_eh_try
    .hidden bl_eh_try
    .type   bl_eh_try, %function
bl_eh_try:
    .cfi_startproc
    // 0x9b = indireto | relativo ao pc | sdata4: o .eh_frame aponta para uma
    // casa que guarda o endereco da personalidade.
    .cfi_personality 0x9b, bl_eh_personality_ref
    stp     x29, x30, [sp, #-176]!
    .cfi_def_cfa_offset 176
    .cfi_offset w30, -168
    .cfi_offset w29, -176
    mov     x29, sp
    .cfi_def_cfa w29, 176
    stp     x19, x20, [sp, #16]
    stp     x21, x22, [sp, #32]
    stp     x23, x24, [sp, #48]
    stp     x25, x26, [sp, #64]
    stp     x27, x28, [sp, #80]
    stp     d8, d9, [sp, #96]
    stp     d10, d11, [sp, #112]
    stp     d12, d13, [sp, #128]
    stp     d14, d15, [sp, #144]
    .cfi_offset w19, -160
    .cfi_offset w20, -152
    .cfi_offset w21, -144
    .cfi_offset w22, -136
    .cfi_offset w23, -128
    .cfi_offset w24, -120
    .cfi_offset w25, -112
    .cfi_offset w26, -104
    .cfi_offset w27, -96
    .cfi_offset w28, -88
    .cfi_offset b8, -80
    .cfi_offset b9, -72
    .cfi_offset b10, -64
    .cfi_offset b11, -56
    .cfi_offset b12, -48
    .cfi_offset b13, -40
    .cfi_offset b14, -32
    .cfi_offset b15, -24
    movz    x11, #0x6d6b
    movk    x11, #0x685f, lsl #16
    movk    x11, #0x5f65, lsl #32
    movk    x11, #0x626c, lsl #48
    add     x10, sp, #160
    eor     x11, x11, x10
    stp     x11, x2, [sp, #160]
    mov     x9, x0
    mov     x0, x1
    blr     x9
    mov     w0, #0
.Leh_restore:
    str     xzr, [sp, #160]
    ldp     d14, d15, [sp, #144]
    ldp     d12, d13, [sp, #128]
    ldp     d10, d11, [sp, #112]
    ldp     d8, d9, [sp, #96]
    ldp     x27, x28, [sp, #80]
    ldp     x25, x26, [sp, #64]
    ldp     x23, x24, [sp, #48]
    ldp     x21, x22, [sp, #32]
    ldp     x19, x20, [sp, #16]
    ldp     x29, x30, [sp], #176
    ret
    .cfi_endproc
    .size   bl_eh_try, .-bl_eh_try

// [[noreturn]] void bl_eh_land(uintptr_t sp)
//
// Chamada de dentro da personalidade, la no fundo da pilha do desenrolador.
// Tudo abaixo do frame do trampolim e descartado.
    .p2align 2
    .globl  bl_eh_land
    .hidden bl_eh_land
    .type   bl_eh_land, %function
bl_eh_land:
    mov     sp, x0
    mov     w0, #1
    b       .Leh_restore
    .size   bl_eh_land, .-bl_eh_land

    .section .data.rel.ro.bl_eh_personality_ref, "aw", %progbits
    .p2align 3
    .hidden bl_eh_personality_ref
bl_eh_personality_ref:
    .xword  bl_eh_personality
    .size   bl_eh_personality_ref, 8

#endif

    .section .note.GNU-stack, "", %progbits
