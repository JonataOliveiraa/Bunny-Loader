import java.util.Properties

// Fase 1 nao precisa da libbunny.so. Desligue o build nativo com
//   -Pbl.nativeBuild=false   (ou a mesma linha em gradle.properties)
// para compilar e rodar o launcher sem instalar NDK/CMake.
val nativeBuild = (project.findProperty("bl.nativeBuild") as String?)?.toBoolean() ?: true

// A fonte do launcher e do menu de cheats é a Andy Bold (Monotype), a mesma do
// Terraria. Não vai para o Git: fica em src/main/res/font/bunny.ttf, integrada
// à mão como os binários do jogo. Sem ela, entra a Comic Neue (OFL) de
// src/fontFallback, com o mesmo nome de recurso.
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
        versionName = "0.2.0"

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
val syncSampleMods by tasks.registering(Sync::class) {
    from(rootProject.file("samples")) { include(BUNDLED_SAMPLES.map { "$it/**" }) }
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
tasks.matching { it.name.startsWith("merge") && it.name.endsWith("Assets") }
    .configureEach { dependsOn(syncSampleMods, syncSprites, syncFonte, syncLegal) }

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

    // Hooking nativo (ARM32/ARM64). Consumido via prefab.
    // Confirme a versão mais recente em Maven Central.
    if (nativeBuild) {
        implementation("com.bytedance.android:shadowhook:1.0.10")
    }

    debugImplementation("androidx.compose.ui:ui-tooling")
    testImplementation("junit:junit:4.13.2")
}
