package bunny;

import android.content.Context;
import android.content.SharedPreferences;
import android.util.Log;
import org.json.JSONArray;
import org.json.JSONObject;
import java.io.BufferedReader;
import java.io.InputStreamReader;
import java.nio.charset.StandardCharsets;
import java.util.HashMap;
import java.util.Iterator;
import java.util.Locale;
import java.util.Map;

public final class MenuLocalization {
    private static volatile TranslationCatalog catalog = new TranslationCatalog(new HashMap<String, String[]>());
    private static volatile String language = "pt-BR";
    private static String choice = "";
    private static boolean loaded;

    private MenuLocalization() { }

    public static synchronized void initialize(Context context) {
        if (!loaded) {
            Map<String, String[]> rows = new HashMap<>();
            try (BufferedReader reader = new BufferedReader(new InputStreamReader(
                    context.getAssets().open("localization/menu.json"), StandardCharsets.UTF_8))) {
                StringBuilder json = new StringBuilder();
                String line;
                while ((line = reader.readLine()) != null) json.append(line);
                JSONObject entries = new JSONObject(json.toString());
                Iterator<String> keys = entries.keys();
                while (keys.hasNext()) {
                    String key = keys.next();
                    JSONArray values = entries.getJSONArray(key);
                    String[] translations = new String[4];
                    for (int i = 0; i < translations.length; i++) translations[i] = values.optString(i, "");
                    rows.put(key, translations);
                }
                catalog = new TranslationCatalog(rows);
                loaded = true;
            } catch (Exception error) {
                Log.e("BunnyLoader", "Não consegui ler as traduções do menu", error);
            }
        }
        SharedPreferences prefs = context.getSharedPreferences("bunny_localization", Context.MODE_PRIVATE);
        try { choice = prefs.getString("language", ""); }
        catch (ClassCastException invalidPreference) { choice = ""; }
        language = TranslationCatalog.resolveLanguage(choice, Locale.getDefault());
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

    public static String text(String source, Object... args) {
        return catalog.text(language, source, args);
    }
}
