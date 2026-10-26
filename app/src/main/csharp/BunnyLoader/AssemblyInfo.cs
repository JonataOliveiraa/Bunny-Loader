using System.Runtime.CompilerServices;

// O CallFrame de cada chamada tem 240 bytes: zerar a cada chamada custaria
// mais que montar os registradores. O codigo gerado escreve tudo o que le.
[module: SkipLocalsInit]
