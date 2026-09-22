package dev.bunnyloader.game

import android.content.Context
import android.content.pm.PackageManager
import android.os.Build
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
     * VAZIO de propósito. Preencher com o digest de um install GENUÍNO vindo da
     * Play — não de um APK extraído e reassinado. O `refs/base.apk` deste repo,
     * por exemplo, está assinado com a chave de teste do AOSP
     * (a40da80a…bf5dc), que é o que qualquer repack produz.
     *
     * Enquanto estiver vazio, a checagem de certificado é PULADA e o motivo
     * aparece no resultado, para não virar um gate que aprova qualquer coisa
     * silenciosamente.
     */
    private val OFFICIAL_CERT_SHA256 = emptySet<String>()

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
            ?: return Result(false, "O Terraria não está instalado neste aparelho.")

        val installer = runCatching {
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.R) {
                pm.getInstallSourceInfo(GameInstall.PACKAGE).installingPackageName
            } else {
                @Suppress("DEPRECATION") pm.getInstallerPackageName(GameInstall.PACKAGE)
            }
        }.getOrNull()
        if (installer != PLAY) {
            return Result(false, "O Terraria instalado não veio da Play (origem: ${installer ?: "desconhecida"}).")
        }

        if (OFFICIAL_CERT_SHA256.isEmpty()) {
            return Result(true, "Terraria da Play encontrado (certificado NÃO conferido: digest oficial não configurado).")
        }
        val digests = certDigests(info)
        if (digests.none { it in OFFICIAL_CERT_SHA256 }) {
            return Result(false, "A assinatura do Terraria instalado não é a oficial.")
        }
        return Result(true, "Terraria oficial da Play encontrado.")
    }

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
