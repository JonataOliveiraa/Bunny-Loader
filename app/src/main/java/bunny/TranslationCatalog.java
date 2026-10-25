package bunny;

import java.util.Collections;
import java.util.HashMap;
import java.util.IllegalFormatException;
import java.util.Locale;
import java.util.Map;

/** Catálogo compartilhado pelo launcher e pelo DEX do Mod Menu. */
public final class TranslationCatalog {
    /** O idioma de reserva: aparelho num idioma sem arquivo e tradução que falta. */
    public static final String FALLBACK = "en";
    /** Idiomas com arquivo próprio, em localization/<cultura>.json. */
    public static final String[] LANGUAGES = {"en", "pt-BR", "ru", "es", "zh", "vi", "th"};

    private final Map<String, Map<String, String>> translations;

    /** Tabelas por idioma: chave (nome em inglês) para o texto. */
    public TranslationCatalog(Map<String, Map<String, String>> translations) {
        Map<String, Map<String, String>> copy = new HashMap<>();
        for (Map.Entry<String, Map<String, String>> entry : translations.entrySet()) {
            copy.put(entry.getKey(), Collections.unmodifiableMap(new HashMap<>(entry.getValue())));
        }
        this.translations = Collections.unmodifiableMap(copy);
    }

    /**
     * A cultura do idioma, no formato do Terraria: nomeia o arquivo
     * `localization/<cultura>.json` e as traduções que o mod traz.
     */
    public static String culture(String language) {
        switch (language) {
            case "en": return "en-US";
            case "ru": return "ru-RU";
            case "es": return "es-ES";
            case "zh": return "zh-Hans";
            case "vi": return "vi-VN";
            case "th": return "th-TH";
            default: return language;
        }
    }

    public static String resolveLanguage(String choice, Locale systemLocale) {
        String tag = choice == null || choice.isEmpty() ? systemLocale.toLanguageTag() : choice;
        String language = Locale.forLanguageTag(tag.replace('_', '-')).getLanguage();
        for (String supported : LANGUAGES) {
            if (Locale.forLanguageTag(supported).getLanguage().equals(language)) return supported;
        }
        return FALLBACK;
    }

    public String text(String language, String key, Object... args) {
        String template = lookup(language, key);
        if (template == null) template = lookup(FALLBACK, key);
        if (template == null) template = key;
        if (args.length == 0) return template;
        try {
            return String.format(Locale.forLanguageTag(language), template, args);
        } catch (IllegalFormatException invalidTranslation) {
            // Um placeholder inválido numa tradução não deve derrubar a interface.
            String fallback = lookup(FALLBACK, key);
            try { return String.format(Locale.US, fallback != null ? fallback : key, args); }
            catch (IllegalFormatException invalidFallback) { return fallback != null ? fallback : key; }
        }
    }

    private String lookup(String language, String key) {
        Map<String, String> table = translations.get(language);
        String value = table != null ? table.get(key) : null;
        return value != null && !value.isEmpty() ? value : null;
    }
}
