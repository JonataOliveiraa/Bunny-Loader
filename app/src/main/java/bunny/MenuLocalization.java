package bunny;

import android.content.Context;
import android.content.SharedPreferences;
import android.util.Log;
import org.json.JSONObject;
import java.io.BufferedReader;
import java.io.InputStreamReader;
import java.nio.charset.StandardCharsets;
import java.util.HashMap;
import java.util.Iterator;
import java.util.Locale;
import java.util.Map;

public final class MenuLocalization {
    private static volatile TranslationCatalog catalog = new TranslationCatalog(new HashMap<String, Map<String, String>>());
    private static volatile String language = TranslationCatalog.FALLBACK;
    private static String choice = "";
    private static boolean loaded;

    private MenuLocalization() { }

    public static synchronized void initialize(Context context) {
        if (!loaded) {
            // Um arquivo por idioma; o que faltar ou estiver quebrado cai no português.
            Map<String, Map<String, String>> tables = new HashMap<>();
            for (String supported : TranslationCatalog.LANGUAGES) {
                String path = "localization/" + TranslationCatalog.culture(supported) + ".json";
                try {
                    tables.put(supported, readTable(context, path));
                } catch (Exception error) {
                    Log.e("BunnyLoader", "Could not read " + path, error);
                }
            }
            catalog = new TranslationCatalog(tables);
            loaded = true;
        }
        SharedPreferences prefs = context.getSharedPreferences("bunny_localization", Context.MODE_PRIVATE);
        try { choice = prefs.getString("language", ""); }
        catch (ClassCastException invalidPreference) { choice = ""; }
        language = TranslationCatalog.resolveLanguage(choice, Locale.getDefault());
    }

    private static Map<String, String> readTable(Context context, String path) throws Exception {
        StringBuilder json = new StringBuilder();
        try (BufferedReader reader = new BufferedReader(new InputStreamReader(
                context.getAssets().open(path), StandardCharsets.UTF_8))) {
            String line;
            while ((line = reader.readLine()) != null) json.append(line);
        }
        JSONObject entries = new JSONObject(json.toString());
        Map<String, String> table = new HashMap<>();
        Iterator<String> keys = entries.keys();
        while (keys.hasNext()) {
            String key = keys.next();
            table.put(key, entries.optString(key, ""));
        }
        return table;
    }

    public static String language() { return language; }
    public static synchronized String choice() { return choice; }

    public static synchronized boolean select(Context context, String selected) {
        if (selected == null) selected = "";
        if (!selected.isEmpty()) selected = TranslationCatalog.resolveLanguage(selected, Locale.getDefault());
        // commit garante que o processo :game leia a escolha ao abrir em seguida.
        if (!context.getSharedPreferences("bunny_localization", Context.MODE_PRIVATE)
                .edit().putString("language", selected).commit()) return false;
        choice = selected;
        language = TranslationCatalog.resolveLanguage(selected, Locale.getDefault());
        return true;
    }

    public static String text(String key, Object... args) {
        return catalog.text(language, key, args);
    }
}
