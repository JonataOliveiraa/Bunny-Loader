import java.security.MessageDigest
import java.util.Base64
import java.util.Properties

plugins {
    id("com.android.application")
    id("org.jetbrains.kotlin.android")
    id("org.jetbrains.kotlin.plugin.serialization")
}

val localProps = Properties().apply {
    rootProject.file("local.properties").takeIf { it.isFile }?.inputStream()?.use(::load)
}
// O token só do Bunny-Loader-Mods primeiro; o _token.txt antigo de reserva.
val embeddedToken = listOf("bunnyloadermodstoken.txt", "_token.txt").asSequence()
    .map { rootProject.file(it) }.filter { it.isFile }
    .map { it.readText().trim() }.firstOrNull { it.isNotEmpty() }
    ?: localProps.getProperty("bl.catalogToken", "").trim()

fun tokenMask(size: Int): ByteArray {
    val out = ByteArray(size)
    var block = "bunny-loader-catalog".toByteArray()
    var i = 0
    while (i < size) {
        block = MessageDigest.getInstance("SHA-256").digest(block)
        for (b in block) if (i < size) out[i++] = b
    }
    return out
}

val tokenBytes = embeddedToken.toByteArray()
val maskBytes = tokenMask(tokenBytes.size)
val hiddenToken = Base64.getEncoder().encodeToString(
    ByteArray(tokenBytes.size) { (tokenBytes[it].toInt() xor maskBytes[it].toInt()).toByte() })

android {
    namespace = "dev.bunnyloader.catalog"
    compileSdk = 34

    defaultConfig {
        applicationId = "com.bunnyloader.catalog"
        minSdk = 24
        targetSdk = 34
        versionCode = 1
        versionName = "1.0.0"
        buildConfigField("String", "EMBEDDED_TOKEN", "\"$hiddenToken\"")
    }

    buildFeatures {
        buildConfig = true
    }

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }
    kotlinOptions {
        jvmTarget = "17"
    }

    sourceSets["main"].assets.srcDir(layout.buildDirectory.dir("generated/sharedAssets"))
}

// A página é a mesma do gerenciador do PC (tools/mods/manager.html), e as
// imagens e a fonte são as do launcher: copiadas a cada build, um lugar só para editar.
val sharedArt = listOf(
    "img_title", "ic_bunny_menu", "ic_bunny_head", "ic_refresh", "ic_trash", "ic_start",
    "ic_fechar", "ic_tab_explorar", "ic_tab_pacotes", "ic_folder", "ic_lupa", "ic_config",
    "spr_loading", "bg_mountains", "bg_hills", "bg_forest", "bg_moon", "bg_star",
)
val syncSharedAssets by tasks.registering(Sync::class) {
    from(rootProject.file("tools/mods/manager.html"))
    from(rootProject.file("app/src/main/res/drawable-nodpi")) {
        include(sharedArt.map { "$it.png" })
        into("art")
    }
    // A Andy fica fora do Git; sem ela, a Comic Neue do app (ver app/build.gradle.kts).
    val fonts = rootProject.file("app/src/main/res/font").takeIf { it.resolve("bunny.ttf").isFile }
        ?: rootProject.file("app/src/fontFallback/res/font")
    from(fonts) {
        include("bunny.ttf")
        into("art")
    }
    into(layout.buildDirectory.dir("generated/sharedAssets"))
}
tasks.named("preBuild") { dependsOn(syncSharedAssets) }

dependencies {
    implementation("androidx.activity:activity-ktx:1.9.2")
    implementation("androidx.core:core-ktx:1.13.1")
    implementation("androidx.webkit:webkit:1.11.0")
    implementation("com.squareup.okhttp3:okhttp:4.12.0")
    implementation("org.jetbrains.kotlinx:kotlinx-coroutines-android:1.8.1")
    implementation("org.jetbrains.kotlinx:kotlinx-serialization-json:1.7.3")
    testImplementation("junit:junit:4.13.2")
}
