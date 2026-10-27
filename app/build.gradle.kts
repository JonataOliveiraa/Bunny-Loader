import java.net.URI
import java.security.MessageDigest
import java.util.Properties

// Fase 1 nao precisa da libbunny.so. Desligue o build nativo com
//   -Pbl.nativeBuild=false   (ou a mesma linha em gradle.properties)
// para compilar e rodar o launcher sem instalar NDK/CMake.
val nativeBuild = (project.findProperty("bl.nativeBuild") as String?)?.toBoolean() ?: true

// A fonte do launcher e do menu de cheats é a Andy Bold (Monotype), a mesma do
// Terraria. Não vai para o Git: fica em src/main/res/font/bunny.ttf, integrada
// à mão como os binários do jogo. Sem ela, entra a Comic Neue (OFL) de
// src/fontFallback, com o mesmo nome de recurso.
// Os mods C# (runtime Mono + a API em src/main/csharp/). Pede o SDK do .NET
// no PATH (`dotnet`) e baixa o runtime do nuget.org uma vez. Sem eles:
//   -Pbl.csharp=false
// e o APK sai sem os mods C# (os JS seguem iguais).
val csharpEnabled = (project.findProperty("bl.csharp") as String?)?.toBoolean() ?: true

// O app de medicao C# x QuickJS (ver BENCH_TESTS).
val benchBuild = (project.findProperty("bl.bench") as String?)?.toBoolean() ?: false

val hasGameFont = file("src/main/res/font/bunny.ttf").isFile
val fontDir = file(if (hasGameFont) "src/main/res/font" else "src/fontFallback/res/font")

// A chave do APK de release fica fora do Git: keystore.properties na raiz
// (storeFile, storePassword, keyAlias, keyPassword; modelo em
// keystore.properties.example) ou as variáveis BL_RELEASE_STORE_FILE,
// BL_RELEASE_STORE_PASSWORD, BL_RELEASE_KEY_ALIAS e BL_RELEASE_KEY_PASSWORD.
// Sem nenhuma, o assembleRelease sai sem assinatura, como antes; com só parte
// delas, o build para em vez de soltar um APK que não instala.
val releaseKeys: Map<String, String>? = run {
    val props = Properties()
    val file = rootProject.file("keystore.properties")
    if (file.isFile) file.inputStream().use { props.load(it) }
    val keys = mapOf(
        "storeFile" to "BL_RELEASE_STORE_FILE",
        "storePassword" to "BL_RELEASE_STORE_PASSWORD",
        "keyAlias" to "BL_RELEASE_KEY_ALIAS",
        "keyPassword" to "BL_RELEASE_KEY_PASSWORD",
    ).mapValues { (name, env) -> props.getProperty(name) ?: System.getenv(env) }
    val missing = keys.filterValues { it.isNullOrBlank() }.keys
    when {
        missing.size == keys.size -> null
        missing.isNotEmpty() -> throw GradleException(
            "assinatura de release incompleta: falta ${missing.joinToString()} " +
                "(keystore.properties ou BL_RELEASE_*)")
        else -> keys.mapValues { it.value!! }
    }
}

// O Android só atualiza no lugar um APK com versionCode maior ou igual e a
// mesma assinatura; alguns instaladores (One UI, HyperOS) recusam o igual. O
// número de commits sobe a cada versão sem ninguém lembrar de mexer nele. Sem
// git (fonte baixada em zip), fica 1.
val commitCount: Int = runCatching {
    providers.exec {
        commandLine("git", "rev-list", "--count", "HEAD")
        workingDir = rootDir
    }.standardOutput.asText.get().trim().toInt()
}.getOrDefault(1)

plugins {
    id("com.android.application")
    id("org.jetbrains.kotlin.android")
    id("org.jetbrains.kotlin.plugin.compose")
    id("org.jetbrains.kotlin.plugin.serialization")
}

android {
    namespace = "dev.bunnyloader"
    compileSdk = 34
    // NDK 26.1 veio incompleto no SDK Manager (sem source.properties); fixa a
    // versao integra instalada.
    ndkVersion = "30.0.16248370"

    defaultConfig {
        applicationId = "com.bunnyloader"
        minSdk = 24
        targetSdk = 34
        versionCode = commitCount
        versionName = "0.3.3"

        // -Pbl.bench=true: o app de medicao (com.bunnyloader.bench), ao lado
        // do Bunny Loader de verdade. Traz os testes de BENCH_TESTS ligados e
        // nenhum outro mod; o resultado sai no relatorio de Diagnostico.
        if (benchBuild) {
            applicationIdSuffix = ".bench"
            // Cada APK de medicao instala por cima do anterior (a One UI recusa
            // versionCode igual) e o personagem e o mundo criados la ficam.
            versionCode = (System.currentTimeMillis() / 60_000).toInt()
        }
        manifestPlaceholders["appLabel"] = if (benchBuild) "Bunny Loader Bench" else "@string/app_name"
        buildConfigField("boolean", "BENCH", benchBuild.toString())

        // Só arm64. O runtime do jogo é integrado e existe apenas para
        // arm64-v8a; permitir armeabi-v7a instalaria o Bunny Loader em
        // aparelhos onde o jogo nunca poderia subir.
        ndk {
            abiFilters.clear()
            abiFilters += "arm64-v8a"
        }
        if (nativeBuild) {
            externalNativeBuild {
                cmake {
                    cppFlags += "-std=c++20"
                }
            }
        }
    }

    if (nativeBuild) {
        externalNativeBuild {
            cmake {
                path = file("src/main/cpp/CMakeLists.txt")
                version = "3.22.1"
            }
        }
    }

    buildFeatures {
        compose = true
        buildConfig = true // VERSION_NAME no BootLog e nas Configurações
        prefab = true // necessário para consumir o ShadowHook via prefab
    }

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }
    kotlinOptions {
        jvmTarget = "17"
    }

    sourceSets["main"].java.srcDir("src/main/kotlin")
    if (!hasGameFont) sourceSets["main"].res.srcDir("src/fontFallback/res")

    // Os assets do jogo vão no APK, sempre.
    //
    // Eles já moraram num asset pack install-time, para caber no teto de
    // 200 MB da Play. Não funciona: a Unity 2021.3 deste build localiza
    // bin/Data abrindo o APK BASE pelo sourceDir e lendo o zip por mmap — é o
    // mesmo motivo de os assets precisarem de noCompress. Num split de asset
    // pack ela não olha, e o jogo morre em "Unable to initialize the Unity
    // Engine". Provado com bundletool: o pack instalou como
    // split_terraria1456_assets.apk e a Unity não achou nada.
    //
    // Como a distribuição é APK pelo GitHub, o teto da Play não se aplica e o
    // caminho do bundle não tem para que existir.
    //
    // -Pbl.uiOnly=true deixa os assets de fora (ver `packaging`): é o modo de
    // iterar em tela, onde o jogo nem sobe.
    if ((project.findProperty("bl.uiOnly") as String?)?.toBoolean() != true) {
        sourceSets["main"].assets.srcDir("../terraria1456_assets/src/main/assets")
    }

    // Os mods de samples/ em BUNDLED_SAMPLES viram o catálogo embutido do launcher. Uma cópia, não
    // uma segunda fonte: samples/ continua sendo a verdade (o CMake também lê
    // de lá para embutir o JS na libbunny), e o que o app lista é exatamente o
    // que o motor carrega.
    sourceSets["main"].assets.srcDir(layout.buildDirectory.dir("generated/blAssets"))
    // A libmonosgen e as libs nativas da BCL, tiradas do pacote do NuGet (ver
    // fetchMonoRuntime, lá embaixo).
    if (csharpEnabled) sourceSets["main"].jniLibs.srcDir(layout.buildDirectory.dir("generated/monoJniLibs"))

    packaging {
        jniLibs {
            useLegacyPackaging = true // extractNativeLibs=true

            // A outra metade do -Pbl.uiOnly=true: aqui saem as libs do jogo
            // (os assets saem lá em cima, não entrando no source set). Juntos
            // são ~225 MB de um APK de ~236 MB e nunca mudam enquanto se mexe
            // em tela — zipar e instalar isso a cada ajuste de layout é o que
            // fazia o ciclo levar quase um minuto.
            //
            // O launcher funciona inteiro assim; só o JOGAR não sobe. Para
            // voltar a jogar, refaça sem a flag.
            if ((project.findProperty("bl.uiOnly") as String?)?.toBoolean() == true) {
                excludes += listOf(
                    "**/libil2cpp.so", "**/libunity.so",
                    "**/libmain.so", "**/libc++_shared.so",
                )
            }
        }
    }

    androidResources {
        // A Unity abre data.unity3d e resources.resource por mmap. Comprimidos,
        // ela tem de inflar ~150 MB a cada boot — o APK do próprio jogo guarda
        // esses dois STORED justamente por isso. É a mesma lista que o Gradle
        // exportado pela Unity usa.
        noCompress += listOf(
            ".unity3d", ".ress", ".resource", ".obb", ".bundle", ".unityexp",
            // PNG ja e comprimido: deflatar de novo nao ganha byte e faz o
            // menu pagar uma inflada por miniatura aberta. Sao ~6800 arquivos.
            ".png",
        )
    }

    signingConfigs {
        if (releaseKeys != null) {
            create("release") {
                storeFile = rootProject.file(releaseKeys.getValue("storeFile"))
                storePassword = releaseKeys.getValue("storePassword")
                keyAlias = releaseKeys.getValue("keyAlias")
                keyPassword = releaseKeys.getValue("keyPassword")
            }
        }
    }

    buildTypes {
        // Com a chave oficial configurada, o debug sai com ela também: é o APK
        // que vai para out/ e para o Release. Sem ela, a chave de debug do PC
        // (o APK não atualiza por cima do oficial).
        debug {
            isMinifyEnabled = false
            signingConfig = signingConfigs.findByName("release") ?: signingConfigs.getByName("debug")
        }
        release {
            isMinifyEnabled = false
            proguardFiles(getDefaultProguardFile("proguard-android-optimize.txt"))
            signingConfig = signingConfigs.findByName("release")
        }
    }

}

/** Os mods de samples/ que vêm dentro do APK. */
val BUNDLED_SAMPLES = listOf("ExampleMod")

/**
 * samples/<Mod>/ -> assets/mods/<Mod>/, antes do merge de assets.
 *
 * Só o ExampleMod vai no APK. Os outros de samples/ são publicados no catálogo
 * online (tools/mods/pack.py + publish.py) e baixados pela aba Explorar.
 */
/**
 * Os testes que o app de medicao (-Pbl.bench=true) traz embutidos e liga:
 * a medicao C# x QuickJS e o teste dos hooks C#. So o manifesto e content/.
 */
val BENCH_TESTS = listOf("benchcs", "benchjs", "hookcs", "compilebench")

/** Compila os .dll dos testes C# de BENCH_TESTS (tools/tests/<teste>/content/). */
val buildBenchTests by tasks.registering {
    enabled = benchBuild
    dependsOn("buildBunnyLoaderCs")
    doLast {
        for (test in listOf("benchcs", "hookcs", "compilebench")) {
            val csproj = rootProject.file("tools/tests/$test/src").listFiles { f -> f.extension == "csproj" }!!.single()
            project.exec { commandLine("dotnet", "build", csproj.absolutePath, "-c", "Release", "-nologo", "-v", "q") }
        }
    }
}

val syncSampleMods by tasks.registering(Sync::class) {
    from(rootProject.file("samples")) { include(BUNDLED_SAMPLES.map { "$it/**" }) }
    if (benchBuild) {
        dependsOn(buildBenchTests)
        from(rootProject.file("tools/tests")) {
            include(BENCH_TESTS.flatMap { listOf("$it/manifest.json", "$it/content/**") })
        }
    }
    into(layout.buildDirectory.dir("generated/blAssets/mods"))
}

/**
 * _sprites/{item,npc}/<id>.png -> assets/sprites/, para o menu de cheats.
 *
 * Vem de fora porque nao da para tirar de dentro: o atlas do jogo vive so na
 * GPU (medido — isReadable = 0, e Blit/ReadPixels/GetNativeTexturePtr foram
 * removidos deste binario). Sao 3,9 MB num APK de 184.
 */
val syncSprites by tasks.registering(Sync::class) {
    from(rootProject.file("_sprites")) { exclude("README.md") }
    into(layout.buildDirectory.dir("generated/blAssets/sprites"))
}
/**
 * res/font/bunny.ttf -> assets/fonte/, para o menu de cheats.
 *
 * O menu roda de um dex carregado em memoria, sem a classe R do app, entao nao
 * alcanca res/font — e `Resources.getFont` so existe da API 26 para cima,
 * enquanto o app vai ate a 24. Pelos assets funciona em todas.
 */
val syncFonte by tasks.registering(Sync::class) {
    from(fontDir)
    into(layout.buildDirectory.dir("generated/blAssets/fonte"))
}
/**
 * LICENSE.md e THIRD_PARTY_NOTICES.md -> assets/legal/, para a tela de licenças
 * (Config > Sobre). As licenças MIT e BSD das bibliotecas pedem que o aviso vá
 * junto com o binário; a raiz continua sendo o único lugar de editar.
 */
val syncLegal by tasks.registering(Sync::class) {
    from(rootProject.file("LICENSE.md"), rootProject.file("THIRD_PARTY_NOTICES.md"))
    into(layout.buildDirectory.dir("generated/blAssets/legal"))
}
// --- Runtime .NET dos mods C# ---
//
// O Mono oficial para Android (o mesmo do .NET for Android), do pacote
// Microsoft.NETCore.App.Runtime.Mono.android-arm64. A versão e o SHA-256 andam
// juntos: trocar uma sem a outra para o build.
val monoVersion = "10.0.14"
val monoPackSha256 = "4c33596a1d084cae705933632528834cbcd07526a54ae30d5bf96f556c945204"
val monoPack = rootProject.layout.buildDirectory.file("mono/$monoVersion/pack.nupkg")
/** As libs nativas que vão para o nativeLibraryDir. */
val monoNativeLibs = listOf(
    "libmonosgen-2.0.so", "libSystem.Native.so", "libSystem.Globalization.Native.so",
    "libmono-component-marshal-ilgen.so",
    // A criptografia do .NET no Android (SHA-1, SHA-256...) passa pelo Java:
    // esta lib e o .jar com as classes que ela procura (monoCryptoJar). O
    // compilador dos mods a usa: o selo do build e o SHA-1 de cada referência.
    "libSystem.Security.Cryptography.Native.Android.so",
)
val monoCryptoJar = layout.buildDirectory.file("generated/monoCrypto/libSystem.Security.Cryptography.Native.Android.jar")
/**
 * Os assemblies da BCL que vão no APK (assets/dotnet/, extraídos pelo
 * DotnetRuntime.kt). Só o que a API e um mod comum usam; o pacote tem 19 MB
 * deles.
 */
val monoManagedLibs = listOf(
    "System.Runtime", "System.Runtime.Extensions", "System.Runtime.InteropServices", "System.Runtime.Loader",
    "System.Runtime.Intrinsics", "System.Collections", "System.Collections.Concurrent", "System.Linq",
    "System.Console", "System.Memory", "System.Threading", "System.Threading.Thread", "System.Private.Uri",
    "System.Text.Encoding.Extensions", "System.Numerics.Vectors", "System.ObjectModel",
)
/**
 * O que o compilador dos mods com fontes (BunnyLoader.Compiler + Roslyn) puxa
 * além dos de cima: o fecho das referências do Microsoft.CodeAnalysis*.dll no
 * pacote Mono (~10 MB).
 */
val monoCompilerLibs = listOf(
    "Microsoft.Win32.Primitives", "System.Collections.Immutable", "System.Collections.NonGeneric",
    "System.Collections.Specialized", "System.ComponentModel", "System.ComponentModel.Primitives",
    "System.Diagnostics.DiagnosticSource", "System.Diagnostics.StackTrace", "System.Diagnostics.TraceSource",
    "System.Diagnostics.Tracing", "System.Formats.Asn1", "System.IO.Compression", "System.IO.Compression.Brotli",
    "System.IO.MemoryMappedFiles", "System.Linq.Expressions", "System.Net.Http", "System.Net.NameResolution",
    "System.Net.NetworkInformation", "System.Net.Primitives", "System.Net.Quic", "System.Net.Security",
    "System.Net.Sockets", "System.Private.Xml", "System.Private.Xml.Linq", "System.Reflection.Emit",
    "System.Reflection.Emit.ILGeneration", "System.Reflection.Emit.Lightweight", "System.Reflection.Metadata",
    "System.Reflection.Primitives", "System.Runtime.Numerics", "System.Runtime.Serialization.Primitives",
    "System.Security.Claims", "System.Security.Cryptography", "System.Text.Encoding.CodePages",
    "System.Text.RegularExpressions", "System.Threading.Channels", "System.Threading.Tasks.Parallel",
    "System.Threading.ThreadPool", "System.Xml.ReaderWriter", "System.Xml.XDocument", "System.Xml.XPath.XDocument",
)
/** A BCL que vai no APK. */
val monoShippedLibs = (monoManagedLibs + monoCompilerLibs).distinct()
/** O compilador e o Roslyn, que o build da API copia para csharp/BunnyLoader. */
val compilerAssemblies = listOf("BunnyLoader.Compiler", "Microsoft.CodeAnalysis", "Microsoft.CodeAnalysis.CSharp")

fun sha256(file: File): String =
    MessageDigest.getInstance("SHA-256").digest(file.readBytes()).joinToString("") { "%02x".format(it) }

val fetchMonoRuntime by tasks.registering {
    inputs.property("sha256", monoPackSha256)
    outputs.file(monoPack)
    doLast {
        val pack = monoPack.get().asFile
        if (pack.isFile && sha256(pack) == monoPackSha256) return@doLast
        pack.parentFile.mkdirs()
        val id = "microsoft.netcore.app.runtime.mono.android-arm64"
        val url = "https://api.nuget.org/v3-flatcontainer/$id/$monoVersion/$id.$monoVersion.nupkg"
        URI(url).toURL().openStream().use { input -> pack.outputStream().use { input.copyTo(it) } }
        val got = sha256(pack)
        if (got != monoPackSha256) {
            pack.delete()
            throw GradleException("runtime Mono $monoVersion: SHA-256 $got, esperado $monoPackSha256")
        }
    }
}

val syncMonoJniLibs by tasks.registering(Sync::class) {
    dependsOn(fetchMonoRuntime)
    // O include vem de um zipTree preguiçoso: sem isto, trocar a lista não
    // refazia a cópia.
    inputs.property("libs", monoNativeLibs)
    from(monoPack.map { zipTree(it) }) {
        include(monoNativeLibs.map { "runtimes/android-arm64/native/$it" })
        eachFile { path = name }
    }
    includeEmptyDirs = false
    into(layout.buildDirectory.dir("generated/monoJniLibs/arm64-v8a"))
}

val syncMonoCryptoJar by tasks.registering(Sync::class) {
    dependsOn(fetchMonoRuntime)
    from(monoPack.map { zipTree(it) }) {
        include("runtimes/android-arm64/native/libSystem.Security.Cryptography.Native.Android.jar")
        eachFile { path = name }
    }
    includeEmptyDirs = false
    into(monoCryptoJar.map { it.asFile.parentFile })
}

/**
 * A API C# (src/main/csharp/BunnyLoader), compilada pelo SDK do .NET. O
 * proprio build dela roda o ProxyGen, que gera as proxies do jogo a partir de
 * refs/DummyDll (o dump local, ver refs/README.md).
 */
val buildBunnyLoaderCs by tasks.registering(Exec::class) {
    val source = file("src/main/csharp/BunnyLoader")
    val out = layout.buildDirectory.dir("csharp/BunnyLoader")
    inputs.files(fileTree(source) { exclude("bin/**", "obj/**") })
    inputs.files(fileTree(file("src/main/csharp/ProxyGen")) { exclude("bin/**", "obj/**") })
    inputs.files(fileTree(file("src/main/csharp/BunnyLoader.Compiler")) { exclude("bin/**", "obj/**") })
    inputs.files(rootProject.file("refs/DummyDll/Assembly-CSharp.dll"))
    outputs.dir(out)
    commandLine(
        "dotnet", "build", File(source, "BunnyLoader.csproj").absolutePath,
        "-c", "Release", "-nologo", "-v", "q", "-o", out.get().asFile.absolutePath,
    )
}

/**
 * O references.txt: contra o que um mod com fontes compila (a base do .NET
 * que vai no APK e a API). O mesmo no app, no catálogo e no PC.
 */
val writeDotnetReferences by tasks.registering {
    val out = layout.buildDirectory.file("generated/dotnetReferences/references.txt")
    val names = listOf("System.Private.CoreLib.dll") + monoShippedLibs.map { "$it.dll" } + "BunnyLoader.dll"
    inputs.property("names", names)
    outputs.file(out)
    doLast {
        out.get().asFile.apply { parentFile.mkdirs() }.writeText(
            "# Gerado pelo app/build.gradle.kts: as referências dos mods C# com fontes.\n" +
                names.joinToString("\n", postfix = "\n"))
    }
}

val syncDotnetAssets by tasks.registering(Sync::class) {
    dependsOn(fetchMonoRuntime, buildBunnyLoaderCs, writeDotnetReferences)
    inputs.property("libs", monoShippedLibs)
    from(monoPack.map { zipTree(it) }) {
        include("runtimes/android-arm64/native/System.Private.CoreLib.dll")
        include(monoShippedLibs.map { "runtimes/android-arm64/lib/net10.0/$it.dll" })
        // O app de medicao leva a biblioteca inteira: o compilador do C#
        // (tools/tests/compilebench) puxa dezenas delas.
        if (benchBuild) include("runtimes/android-arm64/lib/net10.0/*.dll")
        include("THIRD-PARTY-NOTICES.TXT")
        eachFile { path = name }
    }
    from(layout.buildDirectory.dir("csharp/BunnyLoader")) {
        include("BunnyLoader.dll")
        include(compilerAssemblies.map { "$it.dll" })
    }
    from(writeDotnetReferences)
    includeEmptyDirs = false
    into(layout.buildDirectory.dir("generated/blAssets/dotnet"))
}

tasks.matching { it.name.startsWith("merge") && it.name.endsWith("Assets") }
    .configureEach {
        dependsOn(syncSampleMods, syncSprites, syncFonte, syncLegal)
        if (csharpEnabled) dependsOn(syncDotnetAssets)
    }
if (csharpEnabled) {
    tasks.matching { it.name.startsWith("merge") && it.name.endsWith("JniLibFolders") }
        .configureEach { dependsOn(syncMonoJniLibs) }
}

dependencies {
    implementation(platform("androidx.compose:compose-bom:2024.09.02"))
    implementation("androidx.compose.ui:ui")
    implementation("androidx.compose.ui:ui-tooling-preview")
    implementation("androidx.compose.material3:material3")
    implementation("androidx.activity:activity-compose:1.9.2")
    implementation("androidx.core:core-ktx:1.13.1")
    implementation("androidx.lifecycle:lifecycle-runtime-ktx:2.8.6")
    implementation("org.jetbrains.kotlinx:kotlinx-serialization-json:1.7.3")

    // Unity Android Player 2021.3.56f2 (il2cpp/Release) — hospedamos o jogo no
    // nosso processo e precisamos de uma UnityPlayer LIMPA; a do APK do Terraria
    // vem com as strings cifradas pelo PairIP. Ver app/libs/README.md.
    // O jar é da Unity e fica fora do Git. Sem ele o app compila (a UnityPlayer
    // é carregada por reflection), mas o jogo não sobe, como sem os .so do jogo.
    val unityClasses = file("libs/unity-classes.jar")
    if (unityClasses.isFile) implementation(files(unityClasses))

    if (csharpEnabled) implementation(files(monoCryptoJar).builtBy(syncMonoCryptoJar))

    // Hooking nativo (ARM32/ARM64). Consumido via prefab.
    // Confirme a versão mais recente em Maven Central.
    if (nativeBuild) {
        implementation("com.bytedance.android:shadowhook:1.0.10")
    }

    debugImplementation("androidx.compose.ui:ui-tooling")
    testImplementation("junit:junit:4.13.2")
}
