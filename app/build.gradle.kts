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

    defaultConfig {
        applicationId = "dev.bunnyloader"
        minSdk = 24
        targetSdk = 34
        versionCode = 1
        versionName = "0.1.0"

        // O processo herda a arquitetura das .so do app: precisa das ABIs ARM
        // para conseguir carregar as libs do jogo.
        ndk {
            abiFilters += listOf("arm64-v8a", "armeabi-v7a")
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

    packaging {
        jniLibs {
            useLegacyPackaging = true // extractNativeLibs=true
        }
    }

    buildTypes {
        debug { isMinifyEnabled = false }
        release {
            isMinifyEnabled = false
            proguardFiles(getDefaultProguardFile("proguard-android-optimize.txt"))
        }
    }
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

    // Hooking nativo (ARM32/ARM64). Consumido via prefab.
    // Confirme a versão mais recente em Maven Central.
    if (nativeBuild) {
        implementation("com.bytedance.android:shadowhook:1.0.10")
    }

    debugImplementation("androidx.compose.ui:ui-tooling")
}
