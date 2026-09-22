# refs/ — dump local do jogo

**Nada aqui é versionado.** Cada desenvolvedor gera o próprio dump a partir da
sua cópia comprada do Terraria.

## Como gerar

1. Obter o APK do Terraria instalado no aparelho/emulador:
   ```
   adb shell pm path com.and.games505.TerrariaPaid
   adb pull <caminho>/base.apk
   ```
   > Confirme o nome do pacote: `adb shell pm list packages | grep -i terraria`

2. Extrair do APK (é um zip):
   - `lib/arm64-v8a/libil2cpp.so`
   - `assets/bin/Data/Managed/Metadata/global-metadata.dat`

3. Rodar o **Il2CppDumper** (ou Cpp2IL):
   ```
   Il2CppDumper.exe libil2cpp.so global-metadata.dat .
   ```

4. Copiar para cá:
   ```
   refs/
     libil2cpp.so
     global-metadata.dat
     dump.cs                 <- referência de leitura (classes/campos/métodos)
     DummyDll/Assembly-CSharp.dll
     DummyDll/mscorlib.dll
   ```

O `Assembly-CSharp.dll` gerado só tem assinaturas, sem corpo de método — é
exatamente o necessário para consultar nomes, tipos e sobrecargas.
