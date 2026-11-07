package dev.bunnyloader.game

import android.content.ActivityNotFoundException
import android.content.Context
import android.content.Intent
import android.content.pm.PackageManager
import android.content.pm.Signature
import android.net.Uri
import android.os.Build
import java.security.MessageDigest

/**
 * Pode jogar neste aparelho? O único lugar que decide: o JOGAR do launcher e o
 * GameActivity perguntam aqui, e qualquer [Status] diferente de [Status.OK]
 * bloqueia o jogo.
 *
 * Exige o Terraria instalado (o pacote da Play Store) e assinado com o
 * certificado oficial; quem chama já mostra título e texto de qualquer status.
 */
object Eligibility {
    /** [titleKey] e [messageKey]: chaves de assets/localization/<cultura>.json. */
    enum class Status(val titleKey: String, val messageKey: String) {
        OK("", ""),
        NOT_INSTALLED("TerrariaRequiredTitle", "TerrariaRequiredDescription"),
        NOT_ORIGINAL("TerrariaNotOriginalTitle", "TerrariaNotOriginalDescription"),
    }

    private val OFFICIAL_CERTS = setOf(
        "df4faf826627a08e6b24489f4ef64b7330debe6c1d5dc5834bf422dfc19a8542",
    )

    fun check(ctx: Context): Status = when {
        !isInstalled(ctx) -> Status.NOT_INSTALLED
        !isOfficial(ctx) -> Status.NOT_ORIGINAL
        else -> Status.OK
    }

    private fun isInstalled(ctx: Context): Boolean = runCatching {
        ctx.packageManager.getPackageInfo(GameInstall.PACKAGE, 0)
    }.isSuccess

    private fun isOfficial(ctx: Context): Boolean =
        runCatching { signers(ctx).any { sha256(it) in OFFICIAL_CERTS } }.getOrDefault(false)

    @Suppress("DEPRECATION")
    private fun signers(ctx: Context): Array<Signature> {
        val packages = ctx.packageManager
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.P) {
            val info = packages.getPackageInfo(GameInstall.PACKAGE, PackageManager.GET_SIGNING_CERTIFICATES)
                .signingInfo ?: return emptyArray()
            return if (info.hasMultipleSigners()) info.apkContentsSigners else info.signingCertificateHistory
        }
        return packages.getPackageInfo(GameInstall.PACKAGE, PackageManager.GET_SIGNATURES).signatures ?: emptyArray()
    }

    private fun sha256(signature: Signature): String =
        MessageDigest.getInstance("SHA-256").digest(signature.toByteArray())
            .joinToString("") { "%02x".format(it) }

    /** A página do Terraria na Play Store; sem o app da loja, no navegador. */
    fun openStore(ctx: Context) {
        val market = Intent(Intent.ACTION_VIEW, Uri.parse("market://details?id=${GameInstall.PACKAGE}"))
        val web = Intent(Intent.ACTION_VIEW, Uri.parse("https://play.google.com/store/apps/details?id=${GameInstall.PACKAGE}"))
        for (intent in listOf(market, web)) {
            try {
                ctx.startActivity(intent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK))
                return
            } catch (_: ActivityNotFoundException) {
            }
        }
    }
}
