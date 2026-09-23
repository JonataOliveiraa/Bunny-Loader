// Fase 1 nao precisa da libbunny.so. Desligue o build nativo com
//   -Pbl.nativeBuild=false   (ou a mesma linha em gradle.properties)
// para compilar e rodar o launcher sem instalar NDK/CMake.
val nativeBuild = (project.findProperty("bl.nativeBuild") as String?)?.toBoolean() ?: true

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
        versionCode = 1
        versionName = "0.1.0"

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
        buildConfig = true // Eligibility usa BuildConfig.DEBUG
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

    // Asset pack install-time NÃO entra num APK gerado por assembleDebug — ele
    // só materializa via bundle (bundletool/Play). Testar no aparelho exigiria
    // bundletool --local-testing, que não vem pronto no SDK.
    //
    // Com -Pbl.assetsInApk=true os assets do pack entram direto no APK de
    // debug, que fica grande (~200 MB) mas roda por adb install. Fora dessa
    // flag nada muda, então o AAB de release continua com os assets só no pack
    // e sem duplicação.
    if ((project.findProperty("bl.assetsInApk") as String?)?.toBoolean() == true) {
        sourceSets["debug"].assets.srcDir("../terraria1456_assets/src/main/assets")
    }

    // Os mods de samples/ viram o catálogo embutido do launcher. Uma cópia, não
    // uma segunda fonte: samples/ continua sendo a verdade (o CMake também lê
    // de lá para embutir o JS na libbunny), e o que o app lista é exatamente o
    // que o motor carrega.
    sourceSets["main"].assets.srcDir(layout.buildDirectory.dir("generated/blAssets"))

    packaging {
        jniLibs {
            useLegacyPackaging = true // extractNativeLibs=true

            // Iteração de UI: -Pbl.uiOnly=true tira as libs do jogo do pacote.
            // Elas são 66 MB dos ~190 MB do APK e nunca mudam enquanto se mexe
            // em tela — zipar e instalar isso a cada ajuste de layout é o que
            // fazia o ciclo levar meia dúzia de dezenas de segundos.
            //
            // O launcher funciona inteiro assim; só o JOGAR não sobe, porque a
            // libil2cpp não está lá. Para voltar a jogar, refaça sem a flag.
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
        )
    }

    buildTypes {
        debug { isMinifyEnabled = false }
        release {
            isMinifyEnabled = false
            proguardFiles(getDefaultProguardFile("proguard-android-optimize.txt"))
        }
    }

    assetPacks += listOf(":terraria1456_assets")
}

/** samples/<Mod>/ -> assets/mods/<Mod>/, antes do merge de assets. */
val syncSampleMods by tasks.registering(Sync::class) {
    from(rootProject.file("samples"))
    into(layout.buildDirectory.dir("generated/blAssets/mods"))
}
tasks.matching { it.name.startsWith("merge") && it.name.endsWith("Assets") }
    .configureEach { dependsOn(syncSampleMods) }

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
    implementation(files("libs/unity-classes.jar"))

    // Assinatura de APK on-device (patch do Terraria instalado). Lib pura Java
    // do proprio Android build, roda em runtime no aparelho.
    implementation("com.android.tools.build:apksig:8.5.2")

    // Hooking nativo (ARM32/ARM64). Consumido via prefab.
    // Confirme a versão mais recente em Maven Central.
    if (nativeBuild) {
        implementation("com.bytedance.android:shadowhook:1.0.10")
    }

    debugImplementation("androidx.compose.ui:ui-tooling")
}
