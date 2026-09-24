package dev.bunnyloader.game

import android.content.Context
import android.content.pm.PackageManager
import android.os.Build
import dev.bunnyloader.BuildConfig
import java.security.MessageDigest

/**
 * O jogador possui o Terraria?
 *
 * ## O que dá e o que NÃO dá para provar no cliente
 *
 * Não existe API da Play que prove que o usuário comprou OUTRO pacote. A LVL
 * (`checkLicense`) atesta o pacote que chama; o Play Integrity atesta este app.
 * Nenhum dos dois responde "esta pessoa comprou o Terraria".
 *
 * O teto real, então, é: existe um Terraria instalado, assinado pela chave
 * oficial, e instalado pela Play. Isso não é comprovante de compra — é
 * evidência de que há uma cópia legítima no aparelho. É o mesmo que o TL Pro
 * faz (`<queries>` + `CHECK_LICENSE` + `QUERY_ALL_PACKAGES`).
 *
 * Comprovação real de compra exigiria um backend da editora; enquanto não
 * houver, não vale chamar isto de entitlement.
 */
object Eligibility {
    /**
     * SHA-256 do certificado de assinatura oficial do Terraria mobile.
     *
     * Só vale digest de install GENUÍNO vindo da Play. Um APK extraído e
     * reassinado não serve: o `refs/base.apk` deste repo, por exemplo, está com
     * a chave de teste do AOSP (a40da80a…bf5dc), que é o que qualquer repack
     * produz.
     *
     * Se um dia ficar vazio, a checagem é PULADA e o motivo aparece no
     * resultado — um gate que aprova em silêncio é pior que gate nenhum.
     */
    private val OFFICIAL_CERT_SHA256 = setOf(
        // Lido de um Terraria instalado pela Play (Samsung SM-A156M, 1.4.5.8.6).
        // Não confira com o refs/base.apk do repo: aquele está assinado com a
        // chave de teste do AOSP (a40da80a…bf5dc), como qualquer repack.
        "df4faf826627a08e6b24489f4ef64b7330debe6c1d5dc5834bf422dfc19a8542",
    )

    /** Só a Play — evita aceitar um sideload arbitrário como prova. */
    private const val PLAY = "com.android.vending"

    data class Result(val ok: Boolean, val detail: String)

    fun check(ctx: Context): Result {
        val pm = ctx.packageManager
        val flags = if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.P) {
            PackageManager.GET_SIGNING_CERTIFICATES
        } else {
            @Suppress("DEPRECATION") PackageManager.GET_SIGNATURES
        }

        val info = runCatching { pm.getPackageInfo(GameInstall.PACKAGE, flags) }.getOrNull()
            ?: return debugPass("O Terraria não está instalado neste aparelho.")

        val installer = runCatching {
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.R) {
                pm.getInstallSourceInfo(GameInstall.PACKAGE).installingPackageName
            } else {
                @Suppress("DEPRECATION") pm.getInstallerPackageName(GameInstall.PACKAGE)
            }
        }.getOrNull()
        if (installer != PLAY) {
            return debugPass("O Terraria instalado não veio da Play (origem: ${installer ?: "desconhecida"}).")
        }

        val digests = certDigests(info)
        if (digests.none { it in OFFICIAL_CERT_SHA256 } && BuildConfig.DEBUG) {
            // Build de desenvolvimento: o Terraria do emulador é sideloadado e
            // reassinado, então nunca casaria. Passa, mas diz alto que passou —
            // em release BuildConfig.DEBUG é false e a checagem vale.
            return Result(
                true,
                "DEBUG: assinatura do Terraria NÃO confere e foi ignorada." +
                    System.lineSeparator() +
                    "Observado: " + digests.joinToString(", ").ifBlank { "(nenhum)" },
            )
        }
        if (OFFICIAL_CERT_SHA256.isEmpty()) {
            // Mostra o que ESTE aparelho tem. Só um install genuíno da Play
            // serve de referência, e quem roda o app é quem tem um — é daqui
            // que sai o valor para OFFICIAL_CERT_SHA256.
            return Result(
                true,
                "Terraria da Play encontrado, certificado NÃO conferido " +
                    "(digest oficial não configurado).\n" +
                    "Digest observado: " + digests.joinToString(", ").ifBlank { "(nenhum)" },
            )
        }
        if (digests.none { it in OFFICIAL_CERT_SHA256 }) {
            return Result(false, "A assinatura do Terraria instalado não é a oficial.")
        }
        return Result(true, "Terraria oficial da Play encontrado.")
    }

    /**
     * Recusa em release; em debug passa, dizendo alto o que faltou. É o que
     * deixa um segundo emulador (sem conta Google, logo sem Terraria da Play)
     * entrar nos testes de multijogador. O APK distribuído é release, e nele
     * BuildConfig.DEBUG é false: a checagem vale inteira.
     */
    private fun debugPass(reason: String): Result =
        if (BuildConfig.DEBUG) Result(true, "DEBUG: checagem ignorada. $reason")
        else Result(false, reason)

    private fun certDigests(info: android.content.pm.PackageInfo): List<String> {
        val raw = if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.P) {
            val si = info.signingInfo
            when {
                si == null -> emptyArray()
                si.hasMultipleSigners() -> si.apkContentsSigners
                else -> si.signingCertificateHistory
            }
        } else {
            @Suppress("DEPRECATION") info.signatures ?: emptyArray()
        }
        val sha = MessageDigest.getInstance("SHA-256")
        return raw.filterNotNull().map { sig ->
            sha.digest(sig.toByteArray()).joinToString("") { "%02x".format(it) }
        }
    }
}
