package bunny;

import java.util.Collections;
import java.util.HashMap;
import java.util.IllegalFormatException;
import java.util.Locale;
import java.util.Map;

/** Catálogo compartilhado pelo launcher e pelo DEX do Mod Menu. */
public final class TranslationCatalog {
    private final Map<String, String[]> translations;

    public TranslationCatalog(Map<String, String[]> translations) {
        Map<String, String[]> copy = new HashMap<>();
        for (Map.Entry<String, String[]> entry : translations.entrySet()) {
            copy.put(entry.getKey(), entry.getValue().clone());
        }
        this.translations = Collections.unmodifiableMap(copy);
    }

    public static String resolveLanguage(String choice, Locale systemLocale) {
        String tag = choice == null || choice.isEmpty() ? systemLocale.toLanguageTag() : choice;
        String language = Locale.forLanguageTag(tag.replace('_', '-')).getLanguage();
        switch (language) {
            case "en": return "en";
            case "ru": return "ru";
            case "es": return "es";
            case "zh": return "zh";
            default: return "pt-BR";
        }
    }

    public String text(String language, String source, Object... args) {
        int column = "en".equals(language) ? 0 : "ru".equals(language) ? 1 : "es".equals(language) ? 2
            : "zh".equals(language) ? 3 : -1;
        String[] row = translations.get(source);
        String translated = column >= 0 && row != null && column < row.length
            && row[column] != null && !row[column].isEmpty() ? row[column] : source;
        if (args.length == 0) return translated;
        Locale locale = Locale.forLanguageTag(language);
        try {
            return String.format(locale, translated, args);
        } catch (IllegalFormatException invalidTranslation) {
            // Um placeholder inválido numa tradução não deve derrubar a interface.
            try { return String.format(locale, source, args); }
            catch (IllegalFormatException invalidSource) { return source; }
        }
    }
}
