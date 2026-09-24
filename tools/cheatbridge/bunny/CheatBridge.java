package bunny;

import android.app.Activity;
import android.app.AlertDialog;
import android.content.ClipData;
import android.content.ClipboardManager;
import android.content.SharedPreferences;
import android.os.Handler;
import android.os.Looper;
import android.os.Process;
import android.os.SystemClock;
import android.util.Log;
import android.view.ViewConfiguration;
import android.content.res.Resources;
import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.graphics.Canvas;
import android.graphics.Paint;
import android.graphics.Rect;
import android.text.TextPaint;
import android.graphics.Typeface;
import android.graphics.drawable.BitmapDrawable;
import android.graphics.drawable.GradientDrawable;
import android.util.DisplayMetrics;
import android.util.TypedValue;
import android.view.Gravity;
import android.view.View;
import android.widget.Button;
import android.widget.FrameLayout;
import android.text.Editable;
import android.text.TextWatcher;
import android.widget.BaseAdapter;
import android.widget.EditText;
import android.widget.ImageView;
import android.widget.ListView;
import android.widget.LinearLayout;
import android.widget.ScrollView;
import android.view.MotionEvent;
import android.view.ViewGroup;
import java.io.InputStream;
import java.text.Normalizer;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.Collections;
import java.util.HashMap;
import java.util.Iterator;
import java.util.Locale;
import java.util.LinkedHashMap;
import java.util.Map;
import java.util.Set;
import java.util.concurrent.ConcurrentHashMap;
import java.util.concurrent.LinkedBlockingDeque;
import android.widget.TextView;
import android.widget.Toast;

/**
 * Mod Menu do Bunny Loader: as ferramentas de dentro do jogo.
 *
 * Um overlay de Views do Android sobre a Activity — o mesmo mecanismo do botao
 * flutuante, que ja se provou sem root. UI do Android por cima da SurfaceView
 * independe de GLES/Vulkan e o input funciona sozinho.
 *
 * Duas colunas: a da esquerda lista as secoes, a da direita mostra o conteudo
 * da secao escolhida. E a forma que um menu de ferramentas com dez secoes pede
 * — a lista unica anterior ja nao cabia na tela com dezesseis itens, quanto
 * mais com cento e cinquenta.
 *
 * As cores sao as do painel do proprio jogo (#3f5297 com contorno #131625),
 * para o menu parecer parte do Terraria e nao uma janela do Android por cima.
 *
 * Compilada para um dex embutido na libbunny (tools/build-cheatbridge-dex.sh).
 */
public class CheatBridge {

    // Implementados em C++ -> bl::runtime::requestGive / requestSpawn.
    public static native void nOnGive(int type, int stack);
    public static native void nOnSpawn(int type, int count);
    /** Nomes vindos da Localization do jogo. null enquanto ainda moem. */
    public static native String[] nItemNames();
    public static native String[] nNpcNames();
    /** Quadros da tira de cada NPC (Main.npcFrameCount). */
    public static native int[] nNpcFrames();
    /** Secao de cada item, pelos campos do jogo (runtime::ItemClass). */
    public static native byte[] nItemClasses();
    /** NPCID.Count: daqui para cima e NPC de mod. */
    public static native int nVanillaNpcCount();
    /** O PNG de cada NPC de mod, na ordem do tipo. */
    public static native String[] nModNpcTextures();
    /** Teto de pilha de cada item: 1 para arma e equipamento (runtime::itemMaxStacks). */
    public static native int[] nItemStacks();
    /** bl::runtime::setPower — o nivel de um superpoder, 0 = desligado. */
    public static native void nSetPower(int id, int level);
    /** bl::runtime::inWorld — o jogador esta num mundo (nao na tela de titulo). */
    public static native boolean nInWorld();
    /** bl::runtime::setTimeOfDay — 0 amanhecer, 1 meio-dia, 2 anoitecer, 3 meia-noite. */
    public static native void nSetTimeOfDay(int which);
    /** {hardmode 0/1, modo de jogo 0..3 (3 = Jornada)}; -1 fora do mundo. */
    public static native int[] nWorldState();
    /** ItemID.Count: dali para cima, o id e de item de mod. */
    public static native int nVanillaItemCount();
    /** PNG de cada item de mod, indice = tipo - nVanillaItemCount(). */
    public static native String[] nModItemTextures();
    /** Categorias do catalogo de mod, achatadas: nome, icone, nome, icone... */
    public static native String[] nModCategories();
    public static native int[] nModCategoryItems(int category);

    // --- paleta ---
    private static final int PANEL      = 0xFF3F5297;
    private static final int PANEL_DARK = 0xFF2C3A6B;
    private static final int PANEL_LIT  = 0xFF5468B4;
    private static final int OUTLINE    = 0xFF131625;
    private static final int GRASS      = 0xFF22A851;
    private static final int GRASS_LIT  = 0xFF2EED52;
    private static final int INK        = 0xFFFFFFFF;
    private static final int INK_DIM    = 0xFFC3CBEA;
    private static final int SCRIM      = 0xC0000000;

    private static Activity sActivity;
    private static View sOverlay;

    // ------------------------------ catalogo ------------------------------
    //
    // Tudo que as listas leem do jogo, montado UMA vez por partida numa thread
    // de fundo e guardado: nomes, pilhas, os ids de cada secao ja separados e
    // o texto de busca ja sem acento.
    //
    // Antes cada secao montava a sua na UI thread, na hora do toque: uma
    // passada por 6146 itens por secao, ~6800 strings atravessando o JNI e a
    // normalizacao da busca inteira na primeira tecla. Num aparelho fraco,
    // cada uma dessas era um engasgo com o menu na mao.

    // Contrato com runtime::ItemClass (Cheats.h).
    private static final int CL_OTHER = 0, CL_MELEE = 1, CL_RANGED = 2,
        CL_MAGIC = 3, CL_SUMMON = 4, CL_AMMO = 5, CL_TOOL = 6,
        CL_ACCESSORY = 7, CL_ARMOR = 8, CL_POTION = 9, CL_BLOCK = 10;
    private static final int N_CLASSES = 11;
    /** Sem filtro de classe: o catalogo inteiro. */
    private static final int ALL_CLASSES = -1;

    /** O que o menu sabe do jogo. Nao muda depois de montado. */
    private static final class Catalog {
        String[] itemNames, npcNames;
        /** Os mesmos nomes, minusculos e sem acento, para a busca. */
        String[] itemSearch, npcSearch;
        /** Ids de cada secao de item (indice = CL_*), na ordem do jogo. */
        int[][] byClass;
        int[] allItems, allNpcs;
        /** Teto de pilha por item (runtime::itemMaxStacks). */
        int[] maxStacks;
        /** Quadros da tira de cada NPC (Main.npcFrameCount). */
        int[] npcFrames;
    }

    private static volatile Catalog sCatalog;
    /** Quem espera o catalogo. So a UI thread mexe. */
    private static final ArrayList<Runnable> sOnCatalogReady = new ArrayList<Runnable>();
    private static boolean sCatalogLoading;

    /**
     * Garante o catalogo e chama `pronto` na UI thread quando ele existir — na
     * hora, se ja existe. Pedir duas vezes nao monta duas vezes.
     */
    private static void withCatalog(final Activity act, Runnable onReady) {
        if (sCatalog != null) {
            if (onReady != null) onReady.run();
            return;
        }
        if (onReady != null) sOnCatalogReady.add(onReady);
        if (sCatalogLoading) return;
        sCatalogLoading = true;
        Thread t = new Thread(new Runnable() {
            @Override public void run() {
                Process.setThreadPriority(Process.THREAD_PRIORITY_BACKGROUND);
                final Catalog c = buildCatalog();
                act.runOnUiThread(new Runnable() {
                    @Override public void run() {
                        sCatalog = c;
                        sCatalogLoading = false;
                        ArrayList<Runnable> queue = new ArrayList<Runnable>(sOnCatalogReady);
                        sOnCatalogReady.clear();
                        for (Runnable r : queue) r.run();
                    }
                });
            }
        }, "bunny-catalog");
        t.setDaemon(true);
        t.start();
    }

    /**
     * Espera o nativo terminar os nomes e monta tudo de uma vez.
     *
     * Roda fora da UI thread. As chamadas nativas daqui so copiam tabelas ja
     * prontas — nenhuma toca o il2cpp, que e da thread do jogo. O primeiro
     * pedido e o gatilho: o nativo so comeca a ler os nomes quando alguem
     * pergunta, e le num orcamento de tempo por quadro, entao num aparelho
     * lento isto demora mais em vez de engasgar o jogo.
     */
    private static Catalog buildCatalog() {
        final long t0 = SystemClock.elapsedRealtime();
        String[] items, npcs;
        while ((items = nItemNames()) == null) sleepQuietly(80);
        while ((npcs = nNpcNames()) == null) sleepQuietly(80);
        final long t1 = SystemClock.elapsedRealtime();
        Catalog c = new Catalog();
        c.itemNames = items;
        c.npcNames = npcs;
        c.maxStacks = nItemStacks();
        c.npcFrames = nNpcFrames();
        c.allItems = sequence(items.length);
        c.allNpcs = sequence(npcs.length);
        c.byClass = splitByClass(nItemClasses(), items);
        c.itemSearch = normalizeAll(items);
        c.npcSearch = normalizeAll(npcs);
        Log.i("BunnyLoader", "menu: catalogo pronto; esperou o nativo " + (t1 - t0)
            + " ms, montou em " + (SystemClock.elapsedRealtime() - t1) + " ms");
        return c;
    }

    private static void sleepQuietly(long ms) {
        try { Thread.sleep(ms); } catch (InterruptedException e) { /* segue */ }
    }

    /** 1..n-1: o id 0 e "nada" nos dois catalogos (era uma linha "#0" no topo). */
    private static int[] sequence(int n) {
        int[] v = new int[Math.max(0, n - 1)];
        for (int k = 0; k < v.length; k++) v[k] = k + 1;
        return v;
    }

    /**
     * Os ids de cada classe numa passada so: conta, aloca, preenche. Id sem
     * nome e buraco da tabela e fica de fora. Sem classes (o jogo nao as
     * entregou) as secoes saem vazias e "Todos os itens" continua.
     */
    private static int[][] splitByClass(byte[] cls, String[] names) {
        int[] counts = new int[N_CLASSES];
        int end = cls == null ? 0 : Math.min(cls.length, names.length);
        for (int i = 1; i < end; i++) {
            int c = cls[i] & 0xFF;
            if (c < N_CLASSES && names[i] != null) counts[c]++;
        }
        int[][] out = new int[N_CLASSES][];
        for (int c = 0; c < N_CLASSES; c++) out[c] = new int[counts[c]];
        int[] k = new int[N_CLASSES];
        for (int i = 1; i < end; i++) {
            int c = cls[i] & 0xFF;
            if (c < N_CLASSES && names[i] != null) out[c][k[c]++] = i;
        }
        return out;
    }

    private static String[] normalizeAll(String[] names) {
        String[] b = new String[names.length];
        for (int i = 0; i < names.length; i++) b[i] = foldAccents(names[i]);
        return b;
    }

    // ---- itens de mod ----
    //
    // Registrados no boot, antes de o menu existir, entao as tabelas daqui sao
    // lidas uma vez so e na hora: nao tocam o il2cpp, so copiam listas.

    private static int sVanilla = -1, sVanillaNpc = -1;
    private static String[] sModTextures, sModNpcTextures;

    private static void loadModItems() {
        if (sVanilla >= 0) return;
        sVanilla = nVanillaItemCount();
        sModTextures = nModItemTextures();
        sVanillaNpc = nVanillaNpcCount();
        sModNpcTextures = nModNpcTextures();
    }

    /** O PNG do item (ou NPC) de mod `id`, ou null se o id e do jogo. */
    private static String modTexture(boolean npc, int id) {
        int vanilla = npc ? sVanillaNpc : sVanilla;
        String[] files = npc ? sModNpcTextures : sModTextures;
        if (vanilla < 0 || id < vanilla || files == null) return null;
        int i = id - vanilla;
        return i < files.length ? files[i] : null;
    }

    /** Campos por pasta em nModCategories: uid, nome do mod, icone do mod, pasta, icone, tipo. */
    private static final int FOLDER_FIELDS = 6;

    /**
     * Uma entrada por mod, com as pastas dele dentro ("Itens", "NPCs" e as que
     * o mod criou). O nativo manda as pastas ja agrupadas por mod.
     */
    private static void addModSections(ArrayList<Section> l) {
        String[] f = nModCategories();
        if (f == null) return;
        String mod = null, modName = null, modIcon = null;
        ArrayList<Section> folders = new ArrayList<Section>();
        for (int c = 0; c + FOLDER_FIELDS - 1 < f.length; c += FOLDER_FIELDS) {
            if (mod != null && !mod.equals(f[c])) {
                l.add(Section.modGroup(modName, modIcon, folders.toArray(new Section[0])));
                folders.clear();
            }
            mod = f[c];
            modName = f[c + 1];
            modIcon = f[c + 2];
            int[] ids = nModCategoryItems(c / FOLDER_FIELDS);
            if (ids == null || ids.length == 0) continue;
            folders.add(Section.modFolder(f[c + 3], f[c + 4], "npc".equals(f[c + 5]), ids));
        }
        if (mod != null && !folders.isEmpty()) {
            l.add(Section.modGroup(modName, modIcon, folders.toArray(new Section[0])));
        }
    }

    /** Quanto sai de fato: o nativo corta a pilha no teto do item. */
    private static int actualStack(int id, int requested) {
        Catalog c = sCatalog;
        if (c == null || c.maxStacks == null || id < 0 || id >= c.maxStacks.length) return requested;
        int max = c.maxStacks[id];
        return max > 0 && requested > max ? max : requested;
    }

    // ------------------------------ secoes ------------------------------

    /**
     * Uma secao do menu: lista de item, lista de NPC, a grade de poderes, ou a
     * entrada de um mod (FOLDERS), que abre as pastas dele.
     */
    private static final class Section {
        static final int ITEM = 0, NPC = 1, POWER = 2, FOLDERS = 3;

        final String group, title;
        final int kind;
        /** Item: a classe do jogo que entra aqui, ou ALL_CLASSES. */
        final int itemClass;
        /** Lista escolhida a mao (chefes, moradores). null = vem do catalogo. */
        final String[] fixedNames;
        final int[] fixedIds;
        /** Icone: drawable do app, ou sprite de item quando nao ha drawable. */
        final String icon;
        final int iconItem;
        /** Ou um PNG em disco (o icone que um mod deu a categoria dele). */
        String iconFile;
        /** O icone e o sprite de um NPC (iconItem e o id dele), nao de um item. */
        boolean iconNpc;
        /** Onde o slider comeca. Espada vem uma; bloco vem a pilha. */
        final int defaultQty;
        /** FOLDERS: as pastas do mod. */
        Section[] children;

        /** Os ids que a lista mostra, na ordem. Do catalogo, sem copia. */
        int[] ids;
        private String[] fixedSearch;

        private Section(String group, String title, int kind, int itemClass,
                        String[] fixedNames, int[] fixedIds, String icon, int iconItem,
                        int defaultQty) {
            this.group = group; this.title = title; this.kind = kind;
            this.itemClass = itemClass; this.fixedNames = fixedNames; this.fixedIds = fixedIds;
            this.icon = icon; this.iconItem = iconItem; this.defaultQty = defaultQty;
        }

        static Section items(String title, int itemClass, String icon, int iconItem, int qty) {
            return new Section("ITENS", title, ITEM, itemClass, null, null, icon, iconItem, qty);
        }

        static Section npcs(String title, String[] n, int[] i, String icon) {
            return new Section("MUNDO", title, NPC, ALL_CLASSES, n, i, icon, 0, 1);
        }

        static Section powers(String title, int iconItem) {
            return new Section("PODERES", title, POWER, ALL_CLASSES, null, null, null, iconItem, 0);
        }

        /** Uma pasta de mod: os itens (ou NPCs) que estao nela. */
        static Section modFolder(String title, String icon, boolean npc, int[] ids) {
            Section s = new Section("MODS", title, npc ? NPC : ITEM, ALL_CLASSES, null, ids,
                null, ids[0], 1);
            s.iconFile = icon == null || icon.length() == 0 ? null : icon;
            s.iconNpc = npc;
            return s;
        }

        /** A entrada do mod na coluna: abre as pastas. Icone do mod, ou o da 1a pasta. */
        static Section modGroup(String title, String icon, Section[] folders) {
            Section first = folders[0];
            Section s = new Section("MODS", title, FOLDERS, ALL_CLASSES, null, null, null,
                first.iconItem, 0);
            s.iconFile = icon == null || icon.length() == 0 ? first.iconFile : icon;
            s.iconNpc = first.iconNpc;
            s.children = folders;
            return s;
        }

        boolean npc() { return kind == NPC; }

        /** Posicoes visiveis depois do filtro. null = todas. */
        int[] filtered;

        int total() { return ids == null ? 0 : ids.length; }
        int count() { return filtered != null ? filtered.length : total(); }
        int indexAt(int position) { return filtered != null ? filtered[position] : position; }

        String nameAt(int k) {
            if (fixedNames != null) return fixedNames[k];
            return (npc() ? sCatalog.npcNames : sCatalog.itemNames)[ids[k]];
        }

        String searchText(int k) {
            if (fixedNames != null) {
                if (fixedSearch == null) fixedSearch = normalizeAll(fixedNames);
                return fixedSearch[k];
            }
            return (npc() ? sCatalog.npcSearch : sCatalog.itemSearch)[ids[k]];
        }

        /**
         * Filtra por nome OU por id, e poe o que COMECA com o termo na frente.
         *
         * Procurar "espada" com 6147 itens devolve dezenas; quem digitou quer
         * "Espada Larga" antes de "Suporte de Espada". Duas passadas resolvem,
         * sem ordenar nada. O id e comparado como numero: converter cada id em
         * texto a cada tecla eram 6000 strings jogadas fora.
         */
        void applyFilter(String term) {
            String t = foldAccents(term).trim();
            if (t.length() == 0) { filtered = null; return; }
            int id = -1;
            try { id = Integer.parseInt(t); } catch (NumberFormatException e) { /* nao e id */ }

            final int total = total();
            int[] hits = new int[total];
            int n = 0;
            for (int k = 0; k < total; k++) {
                if (ids[k] == id || searchText(k).startsWith(t)) hits[n++] = k;
            }
            if (t.length() > 1) {
                for (int k = 0; k < total; k++) {
                    String b = searchText(k);
                    if (ids[k] != id && !b.startsWith(t) && b.contains(t)) hits[n++] = k;
                }
            }
            filtered = Arrays.copyOf(hits, n);
        }

        /**
         * Pega a lista no catalogo. false enquanto ele monta — ate as listas a
         * mao esperam, porque o recorte do sprite de NPC vem de la.
         */
        boolean load() {
            if (ids != null) return true;
            Catalog c = sCatalog;
            if (c == null) return false;
            ids = fixedIds != null ? fixedIds
                : npc() ? c.allNpcs
                : itemClass == ALL_CLASSES ? c.allItems
                : c.byClass[itemClass];
            return true;
        }
    }

    /** Minuscula e sem acento: "Poção" acha com "pocao". */
    private static String foldAccents(String s) {
        if (s == null) return "";
        // Sem nada fora do ASCII nao ha acento a tirar, e o Normalizer era a
        // maior parte do custo de montar a busca de ~6800 nomes.
        boolean ascii = true;
        for (int i = 0; i < s.length() && ascii; i++) ascii = s.charAt(i) < 128;
        if (ascii) return s.toLowerCase(Locale.ROOT);
        String d = Normalizer.normalize(s, Normalizer.Form.NFD);
        StringBuilder b = new StringBuilder(d.length());
        for (int i = 0; i < d.length(); i++) {
            char c = d.charAt(i);
            if (Character.getType(c) != Character.NON_SPACING_MARK) b.append(c);
        }
        return b.toString().toLowerCase(Locale.ROOT);
    }

    /**
     * "Todos" fica NO grupo, em cima, e nao num grupo seu no pe da coluna: e a
     * mesma lista das secoes de baixo, sem o filtro de classe.
     */
    private static Section[] sections() {
        ArrayList<Section> l = new ArrayList<Section>();
        // Sigilo Celestial. A Estrela Cadente era a primeira ideia, mas o PNG
        // dela e uma tira de 8 quadros e saia como um risco.
        l.add(Section.powers("Superpoderes", 3601));
        // O que os mods trouxeram vem logo depois dos poderes: e o que a pessoa
        // instalou para ver, e no meio de doze secoes do jogo sumiria.
        addModSections(l);
        l.addAll(Arrays.asList(new Section[] {
            Section.items("Todos os itens", ALL_CLASSES, "ic_sec_tudo_item", 0, 999),
            Section.items("Corpo a corpo", CL_MELEE, "ic_sec_melee", 0, 1),
            Section.items("À distância", CL_RANGED, "ic_sec_ranged", 0, 1),
            Section.items("Magia", CL_MAGIC, "ic_sec_magia", 0, 1),
            Section.items("Invocação", CL_SUMMON, "ic_sec_invoc", 0, 1),
            Section.items("Munição", CL_AMMO, null, 40, 999),         // Flecha de Madeira
            Section.items("Ferramentas", CL_TOOL, null, 3521, 1),  // Picareta de Ouro
            Section.items("Acessórios", CL_ACCESSORY, "ic_sec_acess", 0, 1),
            Section.items("Armaduras", CL_ARMOR, null, 231, 1),       // Elmo Derretido
            Section.items("Poções e comida", CL_POTION, "ic_sec_util", 0, 999),
            Section.items("Blocos e móveis", CL_BLOCK, "ic_sec_blocos", 0, 999),
            Section.items("Outros", CL_OTHER, null, 29, 999),           // Cristal de Vida
            new Section("MUNDO", "Todos os NPCs", Section.NPC, ALL_CLASSES, null, null,
                "ic_sec_tudo_npc", 0, 1),
            Section.npcs("Chefes", CheatData.BOSSES_N, CheatData.BOSSES_I, "ic_sec_chefe"),
            Section.npcs("Monstros", CheatData.MONSTERS_N, CheatData.MONSTERS_I, "ic_sec_monstro"),
            Section.npcs("Moradores", CheatData.TOWN_NPCS_N, CheatData.TOWN_NPCS_I, "ic_sec_morador"),
        }));
        return l.toArray(new Section[0]);
    }

    // ------------------------------ poderes ------------------------------
    //
    // A ordem e o contrato com bl::runtime::Power (Powers.h).

    private static final String[] POWER_NAME = {
        "Dano extra", "Super velocidade", "Super pulo", "Parar o tempo", "Imortal",
        "Mana infinita", "Pulo infinito", "Mineração turbo", "Visão total",
        "Voar", "Raio-X", "Lacaios infinitos", "Chuva", "Vento", "Bestiário",
        "Sem inimigos", "Teleporte no mapa", "Limpar inventário", "Revelar mapa",
        "Hardmode", "Dificuldade",
    };
    private static final String[] POWER_DESC = {
        "Toda arma bate mais forte",
        "Corre muito mais rápido",
        "Pula alto, sem dano de queda",
        "Inimigos, tiros e relógio param",
        "Nada tira sua vida",
        "Magia de graça, sempre cheia",
        "Toque no ar e pule de novo",
        "Picareta e machado 4x",
        "Minério, inimigo e perigo",
        "Voa e atravessa paredes",
        "Ilumina a tela inteira",
        "Invoque quantos quiser",
        "Garoa, forte ou tempestade",
        "Calmo, brisa ou ventania",
        "Libera todas as criaturas",
        "Nenhum nasce, ou some com todos",
        "Segure 2 s no mapa grande",
        "Fica favorito, moeda e munição",
        "O mundo inteiro no mapa",
        "Como vencer a Parede de Carne",
        "Clássico, Expert, Mestre, Jornada",
    };
    /** Rotulo de cada nivel. Um so = liga/desliga; nenhum = acao (ver isAction). */
    private static final String[][] POWER_LEVELS = {
        {"x2", "x5", "x10"}, {"x2", "x3"}, {"x2", "x3"},
        {"Ligado"}, {"Ligado"}, {"Ligado"}, {"Ligado"}, {"Ligado"}, {"Ligado"},
        {"Normal", "Rápido"}, {"Ligado"}, {"Ligado"},
        {"Garoa", "Forte", "Tempestade"}, {"Calmo", "Brisa", "Ventania"},
        {},
        {"Novos", "Todos"}, {"Ligado"}, {}, {},
        {"Ligado"}, {"Clássico", "Expert", "Mestre", "Jornada"},
    };
    /** Sprite de item que representa cada poder. */
    private static final int[] POWER_ICON = {
        1301,   // Emblema do Destruidor
        54,     // Botas de Hermes
        2423,   // Perna de Sapo
        3099,   // Cronometro
        1613,   // Escudo de Ankh
        109,    // Cristal de Mana
        53,     // Nuvem na Garrafa
        1294,   // Picosserra
        296,    // Pocao de Espeleologo
        493,    // Asas de Anjo
        298,    // Pocao de Brilho
        1158,   // Colar Pigmeu
        1244,   // Cetro Nimbus
        4367,   // Pipa Azul
        3095,   // Contador de Abates
        0,      // (a taxa de inimigos da Jornada: POWER_RES)
        2997,   // Pocao de Buraco de Minhoca
        348,    // Lixeira
        1315,   // Mapa do Tesouro
        367,    // Martelo Pwn
        3335,   // (trocado pelo icone do modo: GAME_MODE_ICON)
    };

    // Ids que o Java precisa conhecer, na ordem de bl::runtime::Power.
    private static final int P_NO_SPAWNS = 15;
    private static final int P_HARDMODE = 19;
    private static final int P_DIFFICULTY = 20;

    /** Poder com icone de interface do jogo em vez de sprite de item. */
    private static String powerRes(int id) {
        return id == P_NO_SPAWNS ? "ic_poder_spawn" : null;
    }

    /**
     * Poder que e o ESTADO do mundo (hardmode, dificuldade): o cartao mostra o
     * que o mundo esta agora, e tocar manda mudar. Nao conta como ligado, e o
     * "Desligar tudo" nao mexe — desligar o hardmode e decisao, nao faxina.
     */
    private static boolean isWorldState(int id) {
        return id == P_HARDMODE || id == P_DIFFICULTY;
    }

    private static final String[] GAME_MODE = {"Clássico", "Expert", "Mestre", "Jornada"};
    /** O icone de cada modo, o da criacao de mundo (UI/WorldCreation). */
    private static final String[] GAME_MODE_ICON = {
        "ic_dif_normal", "ic_dif_expert", "ic_dif_master", "ic_dif_creative",
    };
    /** A ultima leitura de nWorldState, corrigida na hora pelo toque. */
    private static int[] sWorld = {-1, -1};

    /**
     * Poder que e uma ACAO: roda uma vez no jogo e acabou, nao fica ligado. O
     * nativo zera o nivel sozinho depois de executar; aqui ele nunca entra em
     * sPowerLevels, entao nao conta como ligado nem no "Desligar tudo".
     */
    private static boolean isAction(int id) {
        return POWER_LEVELS[id].length == 0;
    }

    /** Toque num cartao de estado do mundo: pede a mudanca e ja mostra o resultado. */
    private static void toggleWorldState(Activity act, int id) {
        if (id == P_HARDMODE) {
            int hm = sWorld[0];
            if (hm < 0) { toast(act, "Entre num mundo primeiro"); return; }
            nSetPower(id, hm == 1 ? 2 : 1);
            sWorld[0] = hm == 1 ? 0 : 1;
        } else {
            int mode = sWorld[1];
            if (mode < 0) { toast(act, "Entre num mundo primeiro"); return; }
            int next = (mode + 1) % GAME_MODE.length;
            nSetPower(id, next + 1);
            sWorld[1] = next;
        }
    }

    private static void toast(Activity act, String msg) {
        Toast.makeText(act, msg, Toast.LENGTH_SHORT).show();
    }

    /** Quanto tempo o cartao de uma acao fica verde, dizendo que foi. */
    private static final long ACTION_FLASH_MS = 1500;
    /** Nivel atual. O processo do jogo morre junto com o nativo, entao os dois
     *  nascem desligados e so este lado escreve. */
    private static final int[] sPowerLevels = new int[POWER_NAME.length];

    // ------------------------------ desenho ------------------------------

    /**
     * A unidade de medida do menu, em pixels. Tres regras, nesta ordem:
     *
     * PROPORCIONAL: 1/560 do lado menor da tela. E como o jogo escala a propria
     * interface — o padrao dele e altura/1080 vezes uma constante
     * (Main.TryPickingDefaultUIScale) —, entao o menu ocupa a mesma fracao da
     * tela que a interface do jogo.
     *
     * PISO FISICO: nunca menos que 0,95 dp. So a proporcao encolhia demais num
     * celular: 1080 px numa tela de 6,5" sao ~410 dp, e 1/560 disso da 0,7 dp
     * por unidade — letra de 3 mm e alvo de toque menor que o dedo. Medido em dp
     * puro (1,3 dp) era grande demais; o piso fica entre os dois.
     *
     * TETO: a largura tem de caber 760 unidades, que e o que o menu ocupa de
     * ponta a ponta (coluna, busca, barra, X). Em tela estreita o piso nao pode
     * empurrar metade do menu para fora.
     *
     * No MuMu (900 px a 240 dpi) vale a proporcao; num celular, o piso.
     */
    private static final float UNITS_TALL = 560f;
    private static final float MIN_DP = 0.95f;
    private static final float UNITS_WIDE = 760f;
    /**
     * Texto em relacao as medidas. O texto precisou crescer mais que as caixas
     * para alcancar o do jogo (medido: nome de item com a altura do menor texto
     * que o Terraria desenha); essa proporcao continua valendo.
     */
    private static final float TEXT_SCALE = 1.46f;

    private static float sUnit;

    private static float unit(Activity a) {
        if (sUnit == 0) {
            DisplayMetrics m = a.getResources().getDisplayMetrics();
            float shortSide = Math.min(m.widthPixels, m.heightPixels);
            float longSide = Math.max(m.widthPixels, m.heightPixels);
            float u = Math.max(shortSide / UNITS_TALL, MIN_DP * m.density);
            sUnit = Math.min(u, longSide / UNITS_WIDE);
        }
        return sUnit;
    }

    private static int px(Activity a, float u) {
        return Math.round(u * unit(a));
    }

    // ---- o que cabe lado a lado ----
    //
    // A unidade acerta o TAMANHO; a largura em unidades muda de aparelho para
    // aparelho (o MuMu tem ~1000, um celular 19,5:9 no piso tem ~940, uma tela
    // estreita bem menos). O que fica lado a lado se ajusta a ela.

    /** Largura da tela, em unidades. */
    private static float widthUnits(Activity a) {
        DisplayMetrics m = a.getResources().getDisplayMetrics();
        return Math.max(m.widthPixels, m.heightPixels) / unit(a);
    }

    /**
     * Coluna das secoes. Fixa: mais estreita, "Superpoderes" e "Poções e comida"
     * quebravam em duas linhas.
     */
    private static final float ASIDE = 200;

    /** O que sobra para a coluna da direita: tira coluna, margens e respiros. */
    private static float contentUnits(Activity a) {
        return widthUnits(a) - ASIDE - 58;
    }

    /** Barra de quantidade: encolhe antes de espremer a busca. */
    private static float barUnits(Activity a) {
        return contentUnits(a) < 620 ? 90 : 140;
    }

    /**
     * Colunas da grade de poderes: cada cartao pede ~240 unidades para o nome
     * mais comprido ("Super velocidade x3") caber inteiro.
     */
    private static int powerColumns(Activity a) {
        int n = (int) (contentUnits(a) / 240);
        return n < 2 ? 2 : n > 4 ? 4 : n;
    }

    /**
     * Tamanho de texto em pixels, e nao em sp: sp soma a escala de fonte do
     * sistema, e o jogo nao soma. Com a fonte do aparelho em "grande", o menu
     * crescia e o jogo em volta nao.
     */
    private static void applyTextSize(Activity a, TextView t, float size) {
        t.setTextSize(TypedValue.COMPLEX_UNIT_PX, size * TEXT_SCALE * unit(a));
    }

    /** Marca do item escolhido na coluna: faixa clara, sem moldura preta. */
    private static GradientDrawable highlight(Activity a) {
        GradientDrawable d = new GradientDrawable();
        d.setColor(PANEL_LIT);
        d.setCornerRadius(px(a, 6));
        return d;
    }

    /** Painel de fora: mesma cara, canto mais generoso. */
    private static GradientDrawable panelBig(Activity a, int fill, int stroke) {
        GradientDrawable d = panel(a, fill, stroke);
        d.setCornerRadius(px(a, 12));
        return d;
    }

    /** Painel do Terraria: contorno escuro, corpo azul. */
    private static GradientDrawable panel(Activity a, int fill, int stroke) {
        GradientDrawable d = new GradientDrawable();
        d.setColor(fill);
        d.setStroke(px(a, 2), stroke);
        d.setCornerRadius(px(a, 3));
        return d;
    }

    /**
     * Texto com contorno preto, como o do jogo.
     *
     * Desenha a mesma linha duas vezes — uma em STROKE preto, uma em FILL na
     * cor — em vez de mexer no setTextColor no meio do onDraw, que chama
     * invalidate() e poe a View a se redesenhar para sempre.
     *
     * Quebra de linha cai no desenho normal do TextView: contornar texto
     * multilinha na mao pediria refazer o layout inteiro, e aqui multilinha e
     * so a descricao de poder, que nao precisa.
     */
    private static final class OutlinedText extends TextView {
        private final float strokeWidth;

        OutlinedText(Activity a) {
            super(a);
            strokeWidth = px(a, 2);
            // O TextView mede o texto em FILL; o contorno e STROKE e passa
            // metade da espessura para FORA do glifo, nos quatro lados. Sem
            // esta folga a ultima letra e as descidas (g, p, q) saiam raspadas.
            int inset = (int) Math.ceil(strokeWidth / 2f) + 1;
            setPadding(inset, inset, inset, inset);
            setIncludeFontPadding(true);
        }

        @Override protected void onDraw(Canvas c) {
            if (getLineCount() != 1) { super.onDraw(c); return; }
            final String txt = getText().toString();
            final TextPaint pt = getPaint();
            final int color = getCurrentTextColor();
            // Pelo layout, e nao pelo padding: o layout ja sabe a gravidade, e
            // um rotulo alinhado a direita saia colado a esquerda.
            final float x = getCompoundPaddingLeft()
                + (getLayout() != null ? getLayout().getLineLeft(0) : 0);
            final float y = getBaseline();

            pt.setStyle(Paint.Style.STROKE);
            pt.setStrokeWidth(strokeWidth);
            pt.setStrokeJoin(Paint.Join.ROUND);
            pt.setColor(0xFF000000);
            c.drawText(txt, x, y, pt);

            pt.setStyle(Paint.Style.FILL);
            pt.setColor(color);
            c.drawText(txt, x, y, pt);
        }
    }

    private static TextView text(Activity a, String s, float size, int color) {
        TextView t = new OutlinedText(a);
        t.setText(s);
        applyTextSize(a, t, size);
        t.setTextColor(color);
        t.setTypeface(font(a));
        return t;
    }

    /**
     * A fonte do Terraria, a mesma do launcher.
     *
     * Vem dos assets e nao de res/font: este menu roda de um dex carregado em
     * memoria, sem a classe R do app, e `Resources.getFont` so existe da API 26
     * para cima enquanto o app vai ate a 24.
     */
    private static Typeface sFont;

    private static Typeface font(Activity a) {
        if (sFont == null) {
            try {
                sFont = Typeface.createFromAsset(a.getAssets(), "fonte/bunny.ttf");
            } catch (Throwable t) {
                sFont = Typeface.DEFAULT;
            }
        }
        return sFont;
    }

    /**
     * Sprite do res/drawable do app — o jogo roda no NOSSO processo.
     *
     * Decodificado uma vez e guardado: o + e o invocar eram decodificados de
     * novo a CADA linha que a lista montava ou reciclava.
     */
    private static final HashMap<String, Bitmap> sRes = new HashMap<String, Bitmap>();

    private static Bitmap sprite(Activity a, String name) {
        if (sRes.containsKey(name)) return sRes.get(name);
        Bitmap b = null;
        try {
            Resources r = a.getResources();
            int id = r.getIdentifier(name, "drawable", a.getPackageName());
            if (id != 0) {
                BitmapFactory.Options o = new BitmapFactory.Options();
                o.inScaled = false;   // pixel art nao escala no decode
                b = BitmapFactory.decodeResource(r, id, o);
            }
        } catch (Throwable t) {
            b = null;
        }
        sRes.put(name, b);
        return b;
    }

    /**
     * Sprites do jogo por ID, de assets/sprites/{item,npc}/<id>.png.
     *
     * Vem de arquivo porque nao da para vir do jogo: as texturas dele estao so
     * na GPU (medido — o atlas e 2048x2048 com isReadable = 0, e Blit/
     * ReadPixels/GetNativeTexturePtr sairam deste binario no strip do IL2CPP).
     *
     * O cache e pequeno de proposito: a lista recicla, entao o que importa e
     * nao redecodificar o que esta na tela agora. Segurar 6000 bitmaps seria
     * trocar um engasgo por um estouro de memoria. So a UI thread mexe nele.
     */
    private static final int CACHE_SPRITES = 192;
    private static final Map<String, Bitmap> sCache =
        new LinkedHashMap<String, Bitmap>(64, 0.75f, true) {
            @Override protected boolean removeEldestEntry(Map.Entry<String, Bitmap> e) {
                return size() > CACHE_SPRITES;
            }
        };

    private static String spriteKey(boolean npc, int id) {
        return (npc ? "npc/" : "item/") + id;
    }

    /** Quadros da tira do NPC, do catalogo. 1 enquanto ele nao existe. */
    private static int framesOf(int id) {
        Catalog c = sCatalog;
        if (c == null || c.npcFrames == null || id < 0 || id >= c.npcFrames.length) return 1;
        return c.npcFrames[id] < 1 ? 1 : c.npcFrames[id];
    }

    /** Abre e descomprime um sprite. Pode rodar em qualquer thread. */
    private static Bitmap decode(Activity a, String key) {
        final boolean npc = key.startsWith("npc/");
        Bitmap bmp = null;
        InputStream in = null;
        try {
            // Item e NPC de mod nao tem sprite no APK: vem do PNG do proprio mod.
            String file = modTexture(npc, Integer.parseInt(key.substring(npc ? 4 : 5)));
            BitmapFactory.Options o = new BitmapFactory.Options();
            o.inScaled = false;   // pixel art nao escala no decode
            if (file != null) {
                bmp = BitmapFactory.decodeFile(file, o);
            } else {
                in = a.getAssets().open("sprites/" + key + ".png");
                bmp = BitmapFactory.decodeStream(in, null, o);
            }
            // O PNG de NPC e uma TIRA VERTICAL de quadros. Mostrar a tira
            // inteira espremida num quadradinho deixava a Geleia Azul com duas
            // cabecas. Fica so o primeiro quadro.
            if (bmp != null && npc) {
                int q = framesOf(Integer.parseInt(key.substring(4)));
                int frameH = bmp.getHeight() / q;
                if (q > 1 && frameH > 0) {
                    Bitmap frame = Bitmap.createBitmap(bmp, 0, 0, bmp.getWidth(), frameH);
                    if (frame != bmp) bmp.recycle();
                    bmp = frame;
                }
            }
        } catch (Throwable t) {
            bmp = null;           // id sem sprite: a linha fica so com o nome
        } finally {
            if (in != null) try { in.close(); } catch (Throwable ignored) { }
        }
        return bmp;
    }

    /**
     * Na hora, na UI thread. So para os poucos icones fixos (coluna, cartoes
     * de poder), que aparecem uma vez; a lista usa `requestSprite`.
     */
    private static Bitmap spriteJa(Activity a, boolean npc, int id) {
        String k = spriteKey(npc, id);
        if (sCache.containsKey(k)) return sCache.get(k);
        Bitmap b = decode(a, k);
        sCache.put(k, b);
        return b;
    }

    // ---- sprites da lista, fora da UI thread ----
    //
    // Decodificar PNG na UI thread era o custo de ROLAR: cada linha nova que
    // entrava na tela abria e descomprimia um arquivo antes de desenhar. Numa
    // rolada rapida por "Todos os itens" sao dezenas por segundo.
    //
    // Agora a linha aparece na hora, sem sprite, e uma thread de fundo o
    // decodifica. A fila sai pelo pedido MAIS NOVO (o que acabou de entrar na
    // tela), e a cada pedido o que ninguem mais esta mostrando e jogado fora —
    // numa rolada longa, a fila nao acumula as centenas de linhas que so
    // passaram.

    private static final LinkedBlockingDeque<String> sQueue = new LinkedBlockingDeque<String>();
    private static final Set<String> sQueued =
        Collections.newSetFromMap(new ConcurrentHashMap<String, Boolean>());
    /** Quem espera cada sprite. A ImageView guarda na tag o que quer AGORA. */
    private static final HashMap<String, ArrayList<ImageView>> sWaiting =
        new HashMap<String, ArrayList<ImageView>>();
    private static Thread sDecoder;

    /** Pinta `v` com o sprite, agora se ja esta no cache, senao quando chegar. */
    private static void requestSprite(Activity a, ImageView v, boolean npc, int id) {
        final String k = spriteKey(npc, id);
        v.setTag(k);
        if (sCache.containsKey(k)) { setBitmap(a, v, sCache.get(k)); return; }
        setBitmap(a, v, null);
        ArrayList<ImageView> vs = sWaiting.get(k);
        if (vs == null) { vs = new ArrayList<ImageView>(2); sWaiting.put(k, vs); }
        if (!vs.contains(v)) vs.add(v);
        dropOrphans();
        if (sQueued.add(k)) sQueue.offerLast(k);
        startDecoder(a);
    }

    private static boolean isWanted(String k) {
        ArrayList<ImageView> vs = sWaiting.get(k);
        if (vs == null) return false;
        for (ImageView v : vs) if (k.equals(v.getTag())) return true;
        return false;
    }

    /** Tira da fila o que nenhuma linha visivel quer mais. */
    private static void dropOrphans() {
        Iterator<String> it = sQueue.iterator();
        while (it.hasNext()) {
            String k = it.next();
            if (isWanted(k)) continue;
            it.remove();
            sQueued.remove(k);
            sWaiting.remove(k);
        }
    }

    private static void startDecoder(final Activity act) {
        if (sDecoder != null) return;
        sDecoder = new Thread(new Runnable() {
            @Override public void run() {
                Process.setThreadPriority(Process.THREAD_PRIORITY_BACKGROUND);
                while (true) {
                    final String k;
                    try { k = sQueue.takeLast(); } catch (InterruptedException e) { return; }
                    final Bitmap b = decode(act, k);
                    act.runOnUiThread(new Runnable() {
                        @Override public void run() { deliver(act, k, b); }
                    });
                }
            }
        }, "bunny-sprites");
        sDecoder.setDaemon(true);
        sDecoder.start();
    }

    private static void deliver(Activity a, String k, Bitmap b) {
        sQueued.remove(k);
        sCache.put(k, b);
        ArrayList<ImageView> vs = sWaiting.remove(k);
        if (vs == null) return;
        for (ImageView v : vs) if (k.equals(v.getTag())) setBitmap(a, v, b);
    }

    private static ImageView icon(Activity a, Bitmap bmp, float u) {
        ImageView v = new ImageView(a);
        v.setScaleType(ImageView.ScaleType.FIT_CENTER);
        setBitmap(a, v, bmp);
        v.setLayoutParams(new LinearLayout.LayoutParams(px(a, u), px(a, u)));
        return v;
    }

    /** Pixel art sem borrar: vizinho-mais-proximo. */
    private static void setBitmap(Activity a, ImageView v, Bitmap bmp) {
        if (bmp == null) { v.setImageDrawable(null); return; }
        BitmapDrawable d = new BitmapDrawable(a.getResources(), bmp);
        d.getPaint().setFilterBitmap(false);
        v.setImageDrawable(d);
    }

    private static ImageView sectionIcon(Activity a, Section s, float u) {
        if (s.iconFile != null) {
            BitmapFactory.Options o = new BitmapFactory.Options();
            o.inScaled = false;
            Bitmap b = BitmapFactory.decodeFile(s.iconFile, o);
            if (b != null) return icon(a, b, u);
        }
        return icon(a, s.icon != null ? sprite(a, s.icon)
                                       : spriteJa(a, s.iconNpc, s.iconItem), u);
    }

    /**
     * O girassol do jogo abrindo e fechando, enquanto o catalogo monta.
     *
     * spr_loading.png e uma tira vertical de 19 quadros de 52x54: o botao
     * abre, as petalas crescem, murcham e sobra a semente — e volta. Desenhado
     * a mao, quadro a quadro, porque AnimationDrawable pede um drawable por
     * quadro e este menu roda sem a classe R do app.
     */
    private static final class Sunflower extends View {
        private static final int FRAMES = 19, FRAME_W = 52, FRAME_H = 54, FRAME_MS = 60;
        private final Bitmap strip;
        private final Paint paint = new Paint();
        private final Rect src = new Rect(), dst = new Rect();
        private int frame;

        Sunflower(Activity a) {
            super(a);
            strip = sprite(a, "spr_loading");
            paint.setFilterBitmap(false);   // pixel art: vizinho-mais-proximo
        }

        @Override protected void onMeasure(int wSpec, int hSpec) {
            Activity a = (Activity) getContext();
            setMeasuredDimension(px(a, FRAME_W), px(a, FRAME_H));
        }

        @Override protected void onDraw(Canvas c) {
            if (strip == null) return;
            int y = frame * FRAME_H;
            src.set(0, y, FRAME_W, Math.min(strip.getHeight(), y + FRAME_H));
            dst.set(0, 0, getWidth(), getHeight() * src.height() / FRAME_H);
            c.drawBitmap(strip, src, dst, paint);
            frame = (frame + 1) % FRAMES;
            // Some da tela (secao trocada) e a View e solta: o invalidate cai
            // no vazio e a animacao para sozinha.
            postInvalidateDelayed(FRAME_MS);
        }
    }

    /** O girassol com o recado embaixo, no meio da coluna. */
    private static View loadingView(Activity act, String message) {
        LinearLayout box = new LinearLayout(act);
        box.setOrientation(LinearLayout.VERTICAL);
        box.setGravity(Gravity.CENTER);
        box.addView(new Sunflower(act));
        TextView t = text(act, message, 11, INK_DIM);
        t.setGravity(Gravity.CENTER);
        t.setPadding(0, px(act, 8), 0, 0);
        box.addView(t);
        return box;
    }

    // ------------------------------ menu ------------------------------

    public static void install(final Activity act) {
        sActivity = act;
        act.runOnUiThread(new Runnable() {
            @Override public void run() {
                try { buildToggle(act); } catch (Throwable t) { /* nunca derruba o jogo */ }
            }
        });
    }

    /** Lado do botao, em unidades. Era 52: cobria meia coluna do inventario. */
    private static final float BUTTON_SIZE = 40;
    /** Onde o botao foi deixado. So este processo usa o arquivo. */
    private static final String PREFS = "bunny_menu";
    /** De quanto em quanto tempo o botao pergunta se o jogador esta num mundo. */
    private static final int WORLD_POLL_MS = 400;

    /**
     * Botao flutuante que abre o menu.
     *
     * Nasce escondido: na tela de titulo nao ha o que dar nem em quem, e ele
     * ficava por cima do menu do jogo. Aparece quando o jogador entra num mundo.
     */
    private static void buildToggle(final Activity act) {
        ImageView b = icon(act, sprite(act, "ic_bunny_head"), BUTTON_SIZE);
        b.setPadding(px(act, 4), px(act, 4), px(act, 4), px(act, 4));
        b.setBackground(panel(act, PANEL, OUTLINE));
        b.setContentDescription("Mod Menu");
        b.setOnTouchListener(new DragHandler(act));
        b.setVisibility(View.GONE);
        FrameLayout.LayoutParams lp = new FrameLayout.LayoutParams(
            px(act, BUTTON_SIZE), px(act, BUTTON_SIZE));
        lp.gravity = Gravity.TOP | Gravity.START;
        restorePosition(act, lp);
        act.addContentView(b, lp);
        watchWorld(act, b);
    }

    private static int clamp(float v, int min, int max) {
        if (max < min) max = min;
        return Math.round(v < min ? min : v > max ? max : v);
    }

    /**
     * Onde o botao ficou da ultima vez; na primeira, no alto e no meio, que e o
     * trecho da tela que o jogo deixa livre (o inventario fica a esquerda, a
     * vida e o mapa a direita).
     */
    private static void restorePosition(Activity act, FrameLayout.LayoutParams lp) {
        DisplayMetrics m = act.getResources().getDisplayMetrics();
        int x = (m.widthPixels - lp.width) / 2, y = px(act, 6);
        try {
            SharedPreferences p = act.getSharedPreferences(PREFS, Activity.MODE_PRIVATE);
            x = p.getInt("x", x);
            y = p.getInt("y", y);
        } catch (Throwable t) { /* fica no padrao */ }
        // Outra tela (outro aparelho, outra resolucao): nunca fora dela.
        lp.leftMargin = clamp(x, 0, m.widthPixels - lp.width);
        lp.topMargin = clamp(y, 0, m.heightPixels - lp.height);
    }

    /**
     * Toque curto abre o menu; arrastar leva o botao junto.
     *
     * Os dois no MESMO ouvinte: com um OnClickListener a parte, soltar depois de
     * arrastar ainda contava como clique e o menu abria no fim de todo arrasto.
     * So vira arrasto depois de passar da folga de toque do sistema, senao o
     * tremor do dedo num toque ja deslocava o botao.
     *
     * Move pelas MARGENS, e nao por setX/setY. A Activity do jogo desenha por
     * software (hardwareAccelerated=false, como a Unity pede), e ali a View
     * transladada so era redesenhada dentro do quadrado onde estava: no
     * celular o botao sumia assim que saia dele. Mudar o layout move o quadro
     * de verdade, e o Android redesenha o lugar velho e o novo. Custa um layout
     * por movimento, so enquanto o dedo arrasta.
     */
    private static final class DragHandler implements View.OnTouchListener {
        private final Activity act;
        private final int touchSlop;
        private float x0, y0;
        private int m0x, m0y;
        private boolean dragging;

        DragHandler(Activity a) {
            act = a;
            touchSlop = ViewConfiguration.get(a).getScaledTouchSlop();
        }

        @Override public boolean onTouch(View v, MotionEvent e) {
            FrameLayout.LayoutParams lp = (FrameLayout.LayoutParams) v.getLayoutParams();
            switch (e.getActionMasked()) {
                case MotionEvent.ACTION_DOWN:
                    x0 = e.getRawX();
                    y0 = e.getRawY();
                    m0x = lp.leftMargin;
                    m0y = lp.topMargin;
                    dragging = false;
                    return true;
                case MotionEvent.ACTION_MOVE:
                    float dx = e.getRawX() - x0, dy = e.getRawY() - y0;
                    if (!dragging && Math.abs(dx) < touchSlop && Math.abs(dy) < touchSlop) return true;
                    dragging = true;
                    View parent = (View) v.getParent();
                    lp.leftMargin = clamp(m0x + dx, 0, parent.getWidth() - v.getWidth());
                    lp.topMargin = clamp(m0y + dy, 0, parent.getHeight() - v.getHeight());
                    v.setLayoutParams(lp);
                    return true;
                case MotionEvent.ACTION_UP:
                    if (dragging) savePosition(act, lp); else toggleMenu(act);
                    return true;
                case MotionEvent.ACTION_CANCEL:
                    if (dragging) savePosition(act, lp);
                    return true;
                default:
                    return false;
            }
        }
    }

    /** Lembra onde o botao ficou, para a proxima partida. */
    private static void savePosition(Activity act, FrameLayout.LayoutParams lp) {
        try {
            act.getSharedPreferences(PREFS, Activity.MODE_PRIVATE).edit()
                .putInt("x", lp.leftMargin).putInt("y", lp.topMargin).apply();
        } catch (Throwable t) { /* so nao lembra na proxima vez */ }
    }

    /**
     * Mostra o botao so dentro de um mundo, perguntando ao nativo.
     *
     * Perguntar, e nao ser avisado: quem sabe e a thread do jogo, e chamar a UI
     * de la pediria anexar a thread a JVM a cada troca. O nativo ja le o
     * gameMenu todo quadro; aqui e so um booleano a cada 400 ms.
     */
    private static void watchWorld(final Activity act, final View b) {
        final Handler h = new Handler(Looper.getMainLooper());
        h.post(new Runnable() {
            @Override public void run() {
                if (act.isFinishing() || act.isDestroyed()) return;
                boolean inWorld;
                try { inWorld = nInWorld(); } catch (Throwable t) { inWorld = true; }
                int vis = inWorld ? View.VISIBLE : View.GONE;
                if (b.getVisibility() != vis) b.setVisibility(vis);
                // Saiu do mundo com o menu aberto: fecha junto, senao ele
                // ficava por cima da tela de titulo sem botao para fechar.
                if (!inWorld && sOverlay != null && sOverlay.getVisibility() == View.VISIBLE) {
                    sOverlay.setVisibility(View.GONE);
                }
                h.postDelayed(this, WORLD_POLL_MS);
            }
        });
    }

    private static void toggleMenu(Activity act) {
        if (sOverlay != null) {
            sOverlay.setVisibility(
                sOverlay.getVisibility() == View.VISIBLE ? View.GONE : View.VISIBLE);
            return;
        }
        sOverlay = buildMenu(act);
        FrameLayout.LayoutParams lp = new FrameLayout.LayoutParams(
            FrameLayout.LayoutParams.MATCH_PARENT, FrameLayout.LayoutParams.MATCH_PARENT);
        act.addContentView(sOverlay, lp);
    }

    private static View buildMenu(final Activity act) {
        loadModItems();
        final Section[] all = sections();
        // O menu abre nos poderes, que nao dependem de nome nenhum. Pedir o
        // catalogo ja aqui o monta em segundo plano enquanto a pessoa olha a
        // grade: quando ela for aos itens, a lista ja esta pronta.
        withCatalog(act, null);

        FrameLayout root = new FrameLayout(act);
        root.setBackgroundColor(SCRIM);
        // Clique no escuro nao atravessa para o jogo.
        root.setOnClickListener(new View.OnClickListener() {
            @Override public void onClick(View v) { }
        });

        LinearLayout body = new LinearLayout(act);
        body.setOrientation(LinearLayout.HORIZONTAL);
        FrameLayout.LayoutParams blp = new FrameLayout.LayoutParams(
            FrameLayout.LayoutParams.MATCH_PARENT, FrameLayout.LayoutParams.MATCH_PARENT);
        blp.setMargins(px(act, 14), px(act, 14), px(act, 14), px(act, 14));
        body.setLayoutParams(blp);
        root.addView(body);

        // ---- coluna da direita (criada antes: o aside precisa preenche-la) ----
        final LinearLayout content = new LinearLayout(act);
        content.setOrientation(LinearLayout.VERTICAL);
        content.setBackground(panelBig(act, PANEL, OUTLINE));
        content.setPadding(px(act, 10), px(act, 8), px(act, 10), px(act, 8));

        // ---- coluna da esquerda ----
        LinearLayout aside = new LinearLayout(act);
        aside.setOrientation(LinearLayout.VERTICAL);
        aside.setBackground(panelBig(act, PANEL, OUTLINE));
        aside.setPadding(px(act, 10), px(act, 10), px(act, 10), px(act, 10));

        LinearLayout head = new LinearLayout(act);
        head.setOrientation(LinearLayout.HORIZONTAL);
        head.setGravity(Gravity.CENTER_VERTICAL);
        head.addView(icon(act, sprite(act, "ic_bunny_head"), 32));
        LinearLayout titles = new LinearLayout(act);
        titles.setOrientation(LinearLayout.VERTICAL);
        titles.setPadding(px(act, 8), 0, 0, 0);
        titles.addView(text(act, "Mod Menu", 17, INK));
        titles.addView(text(act, "BUNNY LOADER", 9, INK_DIM));
        head.addView(titles);
        aside.addView(head);
        aside.addView(timeButtons(act));

        final ScrollView asideScroll = new ScrollView(act);
        final LinearLayout asideList = new LinearLayout(act);
        asideList.setOrientation(LinearLayout.VERTICAL);
        asideScroll.addView(asideList);
        aside.addView(asideScroll, new LinearLayout.LayoutParams(
            LinearLayout.LayoutParams.MATCH_PARENT, 0, 1f));

        final View[] buttons = new View[all.length];
        String group = null;
        for (int i = 0; i < all.length; i++) {
            final Section s = all[i];
            if (!s.group.equals(group)) {
                group = s.group;
                TextView g = text(act, group, 10, INK_DIM);
                g.setPadding(px(act, 4), px(act, 10), 0, px(act, 4));
                asideList.addView(g);
            }
            final int index = i;
            LinearLayout b = new LinearLayout(act);
            b.setOrientation(LinearLayout.HORIZONTAL);
            b.setGravity(Gravity.CENTER_VERTICAL);
            b.setPadding(px(act, 8), px(act, 6), px(act, 10), px(act, 6));
            b.addView(sectionIcon(act, s, 22));
            TextView label = text(act, s.title, 14, INK);
            label.setSingleLine(true);
            label.setPadding(px(act, 8), 0, 0, 0);
            b.addView(label);
            b.setOnClickListener(new View.OnClickListener() {
                @Override public void onClick(View v) {
                    select(act, content, all, buttons, index);
                }
            });
            LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT,
                LinearLayout.LayoutParams.WRAP_CONTENT);
            lp.bottomMargin = px(act, 1);
            asideList.addView(b, lp);
            buttons[i] = b;
        }

        LinearLayout.LayoutParams alp = new LinearLayout.LayoutParams(
            px(act, ASIDE), LinearLayout.LayoutParams.MATCH_PARENT);
        alp.rightMargin = px(act, 10);
        body.addView(aside, alp);
        body.addView(content, new LinearLayout.LayoutParams(
            0, LinearLayout.LayoutParams.MATCH_PARENT, 1f));

        // Fechar: icone no canto superior direito, sobre tudo. Era uma barra
        // vermelha no pe da coluna, que comia altura de lista e ficava longe do
        // polegar de quem segura o aparelho deitado.
        ImageView closeButton = icon(act, sprite(act, "ic_fechar"), 36);
        closeButton.setPadding(px(act, 3), px(act, 3), px(act, 3), px(act, 3));
        closeButton.setOnClickListener(new View.OnClickListener() {
            @Override public void onClick(View v) {
                if (sOverlay != null) sOverlay.setVisibility(View.GONE);
            }
        });
        // Na altura do topo da coluna, que reserva o canto para ele: mais baixo,
        // cobria o + da primeira linha da lista.
        FrameLayout.LayoutParams flp = new FrameLayout.LayoutParams(px(act, 36), px(act, 36));
        flp.gravity = Gravity.TOP | Gravity.END;
        flp.topMargin = px(act, 16);
        flp.rightMargin = px(act, 18);
        root.addView(closeButton, flp);

        select(act, content, all, buttons, 0);
        return root;
    }

    /** Os quatro botoes de hora da Jornada, com os icones dela (UI/Creative/Infinite_Powers). */
    private static final String[] TIME_ICON = {
        "ic_hora_amanhecer", "ic_hora_meiodia", "ic_hora_anoitecer", "ic_hora_meianoite",
    };
    private static final String[] TIME_NAME = {"Amanhecer", "Meio-dia", "Anoitecer", "Meia-noite"};

    /**
     * Onde ficava a linha embaixo do titulo: a hora do dia a um toque, em
     * qualquer secao. Tocar pisca o botao de verde; fora do mundo, avisa.
     */
    private static View timeButtons(final Activity act) {
        LinearLayout row = new LinearLayout(act);
        row.setOrientation(LinearLayout.HORIZONTAL);
        for (int i = 0; i < TIME_ICON.length; i++) {
            final int which = i;
            final ImageView b = icon(act, sprite(act, TIME_ICON[i]), 34);
            b.setContentDescription(TIME_NAME[i]);
            b.setPadding(px(act, 4), px(act, 4), px(act, 4), px(act, 4));
            b.setBackground(panel(act, PANEL_DARK, OUTLINE));
            b.setOnClickListener(new View.OnClickListener() {
                @Override public void onClick(View v) {
                    if (!nInWorld()) { toast(act, "Entre num mundo primeiro"); return; }
                    nSetTimeOfDay(which);
                    b.setBackground(panel(act, GRASS, OUTLINE));
                    b.postDelayed(new Runnable() {
                        @Override public void run() { b.setBackground(panel(act, PANEL_DARK, OUTLINE)); }
                    }, TIME_FLASH_MS);
                    toast(act, TIME_NAME[which]);
                }
            });
            LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(0, px(act, 34), 1f);
            lp.setMargins(i == 0 ? 0 : px(act, 2), 0, i == TIME_ICON.length - 1 ? 0 : px(act, 2), 0);
            row.addView(b, lp);
        }
        LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(
            LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.WRAP_CONTENT);
        lp.topMargin = px(act, 8);
        lp.bottomMargin = px(act, 2);
        row.setLayoutParams(lp);
        return row;
    }

    private static final long TIME_FLASH_MS = 400;

    /**
     * Barra de quantidade desenhada a mao, com a cara do jogo.
     *
     * A SeekBar do Android traz o tema do aparelho junto — pilula cinza, bolinha
     * com halo de toque, a cor de destaque do sistema — e destoa de tudo em
     * volta. Aqui e um sulco escuro, o preenchido em verde e uma alca quadrada
     * com contorno, que e como o Terraria desenha barra.
     */
    private static final class Range extends View {
        private final int max;
        private final Paint paint = new Paint(Paint.ANTI_ALIAS_FLAG);
        private final float border, handle;
        private int value;
        Runnable onChange;

        Range(Activity a, int max, int initial) {
            super(a);
            this.max = max < 1 ? 1 : max;
            this.value = initial;
            this.border = px(a, 2);
            this.handle = px(a, 13);
        }

        int value() { return value; }

        @Override protected void onMeasure(int wSpec, int hSpec) {
            setMeasuredDimension(resolveSize(px((Activity) getContext(), barUnits((Activity) getContext())), wSpec),
                                 resolveSize((int) (handle * 1.7f + px((Activity) getContext(), 4)), hSpec));
        }

        @Override protected void onDraw(Canvas c) {
            final float mid = getHeight() / 2f;
            final float groove = px((Activity) getContext(), 10);
            final float x0 = handle / 2f, x1 = getWidth() - handle / 2f;
            final float top = mid - groove / 2f, base = mid + groove / 2f;
            final float radius = groove / 2f;

            paint.setStyle(Paint.Style.FILL);
            paint.setColor(OUTLINE);
            c.drawRoundRect(x0 - border, top - border, x1 + border, base + border, radius, radius, paint);
            paint.setColor(PANEL_DARK);
            c.drawRoundRect(x0, top, x1, base, radius, radius, paint);

            final float t = (value - 1) / (float) (max - 1 == 0 ? 1 : max - 1);
            final float cx = x0 + (x1 - x0) * t;
            if (cx > x0) {
                paint.setColor(GRASS);
                c.drawRoundRect(x0, top, cx, base, radius, radius, paint);
            }

            // A alca e MAIS ALTA que o sulco e e BRANCA: dentro do verde do
            // preenchido, uma alca verde sumia — virava uma listra e ninguem
            // via onde pegar.
            final float handleH = handle * 1.7f;
            final float ax = cx - handle / 2f, ay = mid - handleH / 2f;
            final float handleRadius = px((Activity) getContext(), 3);
            paint.setColor(OUTLINE);
            c.drawRoundRect(ax, ay, ax + handle, ay + handleH, handleRadius, handleRadius, paint);
            paint.setColor(0xFFFFFFFF);
            c.drawRoundRect(ax + border, ay + border, ax + handle - border, ay + handleH - border,
                            handleRadius, handleRadius, paint);
            // Meia sombra embaixo: e assim que o jogo da volume a um botao.
            paint.setColor(0xFFB9C0D4);
            c.drawRect(ax + border, mid + handleH / 6f, ax + handle - border, ay + handleH - border, paint);
        }

        @Override public boolean onTouchEvent(MotionEvent e) {
            switch (e.getAction()) {
                case MotionEvent.ACTION_DOWN:
                case MotionEvent.ACTION_MOVE:
                case MotionEvent.ACTION_UP:
                    final float x0 = handle / 2f, x1 = getWidth() - handle / 2f;
                    float t = (e.getX() - x0) / Math.max(1f, x1 - x0);
                    if (t < 0) t = 0; else if (t > 1) t = 1;
                    int next = 1 + Math.round(t * (max - 1));
                    if (next != value) {
                        value = next;
                        invalidate();
                        if (onChange != null) onChange.run();
                    }
                    // Segura o gesto: sem isto a ListView rouba o arrasto.
                    getParent().requestDisallowInterceptTouchEvent(true);
                    return true;
                default:
                    return super.onTouchEvent(e);
            }
        }
    }

    /** Teto do slider. NPC e baixo de proposito: 10 chefes de uma vez trava. */
    private static final int MAX_ITEM = 999;
    private static final int MAX_NPC = 10;

    /**
     * O topo da coluna, numa linha so: icone, titulo e contagem a esquerda.
     *
     * Eram tres faixas empilhadas de ponta a ponta — titulo com subtitulo, a
     * barra de quantidade esticada na largura inteira, a busca tambem. Nenhuma
     * das tres precisa da largura toda, e cada faixa era uma linha de lista a
     * menos na tela. Lado a lado, o que e fixo fica do tamanho que tem.
     */
    private static LinearLayout buildHeader(Activity act, Section s, TextView countLabel) {
        return buildHeader(act, s, countLabel, null);
    }

    /** Com `back`, uma seta de voltar na frente (dentro das pastas de um mod). */
    private static LinearLayout buildHeader(Activity act, Section s, TextView countLabel,
                                            final Runnable back) {
        LinearLayout t = new LinearLayout(act);
        t.setOrientation(LinearLayout.HORIZONTAL);
        t.setGravity(Gravity.CENTER_VERTICAL);
        // O X de fechar fica por cima deste canto; a linha para antes dele.
        t.setPadding(0, 0, px(act, 40), 0);
        if (back != null) {
            // A seta do jogo (UI/TexturePackButtons), a mesma do pacote de texturas.
            ImageView arrow = icon(act, sprite(act, "ic_seta_esq"), 34);
            arrow.setContentDescription("Voltar");
            // Area de toque maior que o desenho: a seta fica no canto.
            arrow.setPadding(px(act, 6), px(act, 6), px(act, 10), px(act, 6));
            arrow.setOnClickListener(new View.OnClickListener() {
                @Override public void onClick(View v) { back.run(); }
            });
            t.addView(arrow);
        }
        t.addView(sectionIcon(act, s, 26));
        TextView titleView = text(act, s.title, 17, INK);
        titleView.setPadding(px(act, 8), 0, 0, 0);
        titleView.setSingleLine(true);
        t.addView(titleView);
        if (countLabel != null) {
            countLabel.setPadding(px(act, 8), 0, 0, 0);
            t.addView(countLabel);
        }
        return t;
    }

    /** A secao na tela agora: quem espera o catalogo so redesenha se for ela. */
    private static int sCurrentSection = -1;
    /** Espera depois da ultima tecla antes de filtrar. */
    private static final int SEARCH_DELAY_MS = 150;

    /** Troca a secao mostrada e marca o botao escolhido. */
    private static void select(final Activity act, final LinearLayout content,
                               final Section[] all, final View[] buttons, final int index) {
        sCurrentSection = index;
        for (int i = 0; i < buttons.length; i++) {
            // So o escolhido tem fundo. Antes cada item da coluna era um
            // retangulo azul-escuro com contorno preto, e onze deles empilhados
            // viravam uma parede de caixas — o que se via era a moldura, nao a
            // lista.
            buttons[i].setBackground(i == index ? highlight(act) : null);
        }
        final Section s = all[index];
        final Runnable redraw = new Runnable() {
            @Override public void run() {
                if (sCurrentSection == index) select(act, content, all, buttons, index);
            }
        };
        if (s.kind == Section.POWER) { content.removeAllViews(); showPowers(act, content, s); return; }
        if (s.kind == Section.FOLDERS) { showFolders(act, content, s, index); return; }
        showList(act, content, s, redraw, null);
    }

    /**
     * A entrada de um mod: as pastas dele, uma por linha. Tocar abre a lista
     * da pasta, que tem a seta de voltar para ca.
     */
    private static void showFolders(final Activity act, final LinearLayout content,
                                    final Section mod, final int index) {
        content.removeAllViews();
        LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(
            LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.WRAP_CONTENT);
        lp.bottomMargin = px(act, 6);
        content.addView(buildHeader(act, mod, text(act, mod.children.length == 1 ? "1 pasta"
            : mod.children.length + " pastas", 10, INK_DIM)), lp);

        final Runnable back = new Runnable() {
            @Override public void run() {
                if (sCurrentSection == index) showFolders(act, content, mod, index);
            }
        };
        LinearLayout rows = new LinearLayout(act);
        rows.setOrientation(LinearLayout.VERTICAL);
        for (final Section folder : mod.children) {
            LinearLayout r = new LinearLayout(act);
            r.setOrientation(LinearLayout.HORIZONTAL);
            r.setGravity(Gravity.CENTER_VERTICAL);
            r.setPadding(px(act, 6), px(act, 6), px(act, 6), px(act, 6));
            r.addView(sectionIcon(act, folder, 30));
            TextView name = text(act, folder.title, 14, INK);
            name.setPadding(px(act, 8), 0, 0, 0);
            r.addView(name);
            final int n = folder.fixedIds.length;
            TextView count = text(act, n + (folder.npc() ? (n == 1 ? " NPC" : " NPCs")
                                                         : (n == 1 ? " item" : " itens")), 10, INK_DIM);
            count.setPadding(px(act, 8), 0, 0, px(act, 2));
            r.addView(count, new LinearLayout.LayoutParams(
                0, LinearLayout.LayoutParams.WRAP_CONTENT, 1f));
            ImageView enter = icon(act, sprite(act, "ic_seta_dir"), 20);   // seta de entrar
            r.addView(enter);
            r.setBackground(panel(act, PANEL_DARK, OUTLINE));
            r.setOnClickListener(new View.OnClickListener() {
                @Override public void onClick(View v) {
                    final Runnable[] redraw = { null };
                    redraw[0] = new Runnable() {
                        @Override public void run() {
                            if (sCurrentSection == index) showList(act, content, folder, redraw[0], back);
                        }
                    };
                    showList(act, content, folder, redraw[0], back);
                }
            });
            LinearLayout.LayoutParams rp = new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.WRAP_CONTENT);
            rp.bottomMargin = px(act, 4);
            rows.addView(r, rp);
        }
        ScrollView scroll = new ScrollView(act);
        scroll.addView(rows);
        content.addView(scroll, new LinearLayout.LayoutParams(
            LinearLayout.LayoutParams.MATCH_PARENT, 0, 1f));
    }

    /**
     * A lista de uma secao (ou pasta): busca, quantidade, linhas. `redraw`
     * mostra de novo quando o catalogo fica pronto; `back`, se houver, e a seta
     * de voltar (pasta de mod).
     */
    private static void showList(final Activity act, final LinearLayout content, final Section s,
                                 final Runnable redraw, final Runnable back) {
        s.filtered = null;   // filtro e da visita, nao da secao
        content.removeAllViews();

        if (!s.load()) {
            // O girassol gira enquanto o catalogo monta em segundo plano; quando
            // ele fica pronto, a secao se redesenha sozinha — se a pessoa ainda
            // estiver nela.
            content.addView(buildHeader(act, s, null, back));
            content.addView(loadingView(act, "Lendo o catálogo do jogo, no seu idioma..."),
                new LinearLayout.LayoutParams(LinearLayout.LayoutParams.MATCH_PARENT, 0, 1f));
            withCatalog(act, redraw);
            return;
        }

        // ---- topo: titulo | busca | quantidade ----
        final TextView countLabel = text(act, String.valueOf(s.total()), 10, INK_DIM);
        LinearLayout headerRow = buildHeader(act, s, countLabel, back);

        LinearLayout searchBox = new LinearLayout(act);
        searchBox.setOrientation(LinearLayout.HORIZONTAL);
        searchBox.setGravity(Gravity.CENTER_VERTICAL);
        searchBox.setBackground(panel(act, PANEL_DARK, OUTLINE));
        searchBox.setPadding(px(act, 6), 0, px(act, 6), 0);
        searchBox.addView(icon(act, sprite(act, "ic_lupa"), 16));

        final EditText searchField = new EditText(act);
        searchField.setSingleLine(true);
        searchField.setBackground(null);
        searchField.setTextColor(INK);
        searchField.setHintTextColor(INK_DIM);
        searchField.setHint("Nome ou id");
        applyTextSize(act, searchField, 11);
        searchField.setTypeface(font(act));
        searchField.setPadding(px(act, 6), px(act, 3), 0, px(act, 3));
        searchBox.addView(searchField, new LinearLayout.LayoutParams(
            0, LinearLayout.LayoutParams.WRAP_CONTENT, 1f));

        LinearLayout.LayoutParams bp = new LinearLayout.LayoutParams(
            0, LinearLayout.LayoutParams.WRAP_CONTENT, 1f);
        bp.leftMargin = px(act, 12);
        bp.rightMargin = px(act, 12);
        headerRow.addView(searchBox, bp);

        final int max = s.npc() ? MAX_NPC : MAX_ITEM;
        final int initial = Math.min(max, Math.max(1, s.defaultQty));
        final int[] qty = { initial };
        final TextView qtyLabel = text(act, "x" + initial, 12, INK);
        // Largura de "x999" fixa: sem isto a barra pulava para o lado a cada
        // digito que o numero ganhava ou perdia.
        qtyLabel.setWidth(px(act, 40));
        qtyLabel.setGravity(Gravity.END);
        final Range qtyBar = new Range(act, max, initial);
        qtyBar.onChange = new Runnable() {
            @Override public void run() {
                qty[0] = qtyBar.value();
                qtyLabel.setText("x" + qty[0]);
            }
        };
        headerRow.addView(qtyLabel);
        LinearLayout.LayoutParams rp = new LinearLayout.LayoutParams(
            px(act, barUnits(act)), LinearLayout.LayoutParams.WRAP_CONTENT);
        rp.leftMargin = px(act, 6);
        headerRow.addView(qtyBar, rp);

        LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(
            LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.WRAP_CONTENT);
        lp.bottomMargin = px(act, 6);
        content.addView(headerRow, lp);

        if (s.total() == 0) {
            content.addView(text(act, s.npc() ? "Nenhum NPC aqui."
                : "O jogo não entregou a classe dos itens. \"Todos os itens\" "
                + "continua com a lista inteira.", 12, INK_DIM));
            return;
        }

        // ---- lista ----
        //
        // ListView, e nao ScrollView com tudo dentro: "Todos os itens" tem 6147
        // linhas, e montar 6147 Views de uma vez trava o jogo por segundos. A
        // ListView so monta o que cabe na tela e reaproveita ao rolar.
        final ListView list = new ListView(act);
        // Uma linha quase invisivel entre as linhas, no lugar da borda preta
        // arredondada que cada card tinha: com 6147 deles, a tela virava uma
        // grade de caixinhas em vez de uma lista.
        list.setDivider(new android.graphics.drawable.ColorDrawable(0x22FFFFFF));
        list.setDividerHeight(Math.max(1, px(act, 1)));
        list.setCacheColorHint(0);
        final BaseAdapter adapter = new BaseAdapter() {
            @Override public int getCount() { return s.count(); }
            @Override public Object getItem(int i) { return null; }
            @Override public long getItemId(int i) { return i; }
            @Override public View getView(int i, View recycled, ViewGroup parent) {
                return row(act, s, s.indexAt(i), qty, recycled);
            }
        };
        list.setAdapter(adapter);
        // Filtra quando a pessoa PARA de digitar, e nao a cada letra: "espada"
        // eram seis passadas por 6146 nomes, cinco delas jogadas fora.
        final Runnable[] pending = { null };
        searchField.addTextChangedListener(new TextWatcher() {
            @Override public void onTextChanged(CharSequence t, int a1, int b1, int c1) {
                if (pending[0] != null) list.removeCallbacks(pending[0]);
                final String term = t.toString();
                pending[0] = new Runnable() {
                    @Override public void run() {
                        s.applyFilter(term);
                        countLabel.setText(s.filtered == null ? String.valueOf(s.total())
                                                          : s.count() + " de " + s.total());
                        adapter.notifyDataSetChanged();
                        list.setSelection(0);
                    }
                };
                list.postDelayed(pending[0], SEARCH_DELAY_MS);
            }
            @Override public void beforeTextChanged(CharSequence t, int a1, int b1, int c1) { }
            @Override public void afterTextChanged(Editable e) { }
        });
        content.addView(list, new LinearLayout.LayoutParams(
            LinearLayout.LayoutParams.MATCH_PARENT, 0, 1f));
    }

    /** As partes de uma linha, para trocar o conteudo em vez de remontar. */
    private static final class RowViews {
        ImageView sprite;
        TextView name, detail;
        ImageView action;
    }

    /** Uma linha: sprite, nome, id e o botao de acao. */
    private static View row(final Activity act, final Section s, final int i,
                            final int[] qty, View recycled) {
        final RowViews L;
        LinearLayout r;
        if (recycled instanceof LinearLayout && recycled.getTag() instanceof RowViews) {
            r = (LinearLayout) recycled;
            L = (RowViews) recycled.getTag();
        } else {
            L = new RowViews();
            r = new LinearLayout(act);
            r.setOrientation(LinearLayout.HORIZONTAL);
            r.setGravity(Gravity.CENTER_VERTICAL);
            r.setPadding(px(act, 6), px(act, 4), px(act, 6), px(act, 4));

            L.sprite = icon(act, null, 30);
            r.addView(L.sprite);

            // Nome e id na MESMA linha: empilhados, cada linha da lista gastava
            // uma altura de texto a mais, e na escala do jogo isso e uma linha
            // inteira de lista a menos por tela.
            LinearLayout labels = new LinearLayout(act);
            labels.setOrientation(LinearLayout.HORIZONTAL);
            labels.setGravity(Gravity.BOTTOM);
            labels.setPadding(px(act, 8), 0, 0, 0);
            L.name = text(act, "", 14, INK);
            L.detail = text(act, "", 10, INK_DIM);
            L.detail.setPadding(px(act, 8), 0, 0, px(act, 2));
            labels.addView(L.name);
            labels.addView(L.detail);
            r.addView(labels, new LinearLayout.LayoutParams(
                0, LinearLayout.LayoutParams.WRAP_CONTENT, 1f));

            // So o sinal, sem palavra e sem caixa atras: a acao ja esta dita
            // pela secao, e a moldura competia com o sprite do item.
            L.action = new ImageView(act);
            L.action.setScaleType(ImageView.ScaleType.FIT_CENTER);
            L.action.setPadding(px(act, 4), px(act, 4), px(act, 4), px(act, 4));
            r.addView(L.action, new LinearLayout.LayoutParams(px(act, 36), px(act, 30)));

            r.setLayoutParams(new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT,
                LinearLayout.LayoutParams.WRAP_CONTENT));
            r.setTag(L);
        }

        final int id = s.ids[i];
        final String rawName = s.nameAt(i);
        // Id sem nome na Localization (buraco na tabela): mostra o id, que e o
        // que o jogador precisa para saber o que pediu.
        final String name = (rawName == null || rawName.length() == 0) ? ("#" + id) : rawName;

        requestSprite(act, L.sprite, s.npc(), id);
        L.name.setText(name);
        L.detail.setText(s.npc() ? ("NPC " + id) : ("id " + id));
        L.action.setContentDescription(s.npc() ? "Invocar" : "Pegar");
        setBitmap(act, L.action, sprite(act, s.npc() ? "ic_invocar" : "ic_pegar"));
        L.action.setOnClickListener(new View.OnClickListener() {
            @Override public void onClick(View v) {
                int n = qty[0] < 1 ? 1 : qty[0];
                if (s.npc()) {
                    // A contagem vai JUNTO: o nativo guarda o pedido num slot
                    // so, consumido uma vez por quadro, entao dez chamadas
                    // seguidas viravam um NPC.
                    nOnSpawn(id, n);
                    Toast.makeText(act, name + (n > 1 ? " x" + n : "") + " invocado",
                        Toast.LENGTH_SHORT).show();
                } else {
                    nOnGive(id, n);
                    int given = actualStack(id, n);
                    Toast.makeText(act, name + (given > 1 ? " x" + given : ""),
                        Toast.LENGTH_SHORT).show();
                }
            }
        });
        return r;
    }

    // ------------------------------ grade de poderes ------------------------------

    /**
     * Cartoes de liga/desliga. Tocar avanca o nivel: desligado, x2, x5, x10,
     * desligado. Um cartao so por poder, em vez de um botao por nivel, porque a
     * grade inteira tem de caber sem rolar.
     */
    private static void showPowers(final Activity act, LinearLayout content, Section s) {
        int[] w = nWorldState();
        if (w != null && w.length == 2) sWorld = w;
        final TextView activeLabel = text(act, "", 10, INK_DIM);
        LinearLayout headerRow = buildHeader(act, s, activeLabel);

        final View[] cards = new View[POWER_NAME.length];
        View spacer = new View(act);
        headerRow.addView(spacer, new LinearLayout.LayoutParams(0, 1, 1f));
        TextView offButton = text(act, "Desligar tudo", 11, INK);
        offButton.setBackground(panel(act, PANEL_DARK, OUTLINE));
        offButton.setPadding(px(act, 10), px(act, 5), px(act, 10), px(act, 5));
        offButton.setOnClickListener(new View.OnClickListener() {
            @Override public void onClick(View v) {
                for (int i = 0; i < sPowerLevels.length; i++) {
                    if (sPowerLevels[i] == 0) continue;
                    sPowerLevels[i] = 0;
                    nSetPower(i, 0);
                    paintPowerCard(act, cards[i], i);
                }
                updateActiveCount(activeLabel);
            }
        });
        headerRow.addView(offButton);

        LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(
            LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.WRAP_CONTENT);
        lp.bottomMargin = px(act, 6);
        content.addView(headerRow, lp);

        final int columns = powerColumns(act);
        LinearLayout grid = new LinearLayout(act);
        grid.setOrientation(LinearLayout.VERTICAL);
        LinearLayout queue = null;
        for (int i = 0; i < POWER_NAME.length; i++) {
            if (i % columns == 0) {
                queue = new LinearLayout(act);
                queue.setOrientation(LinearLayout.HORIZONTAL);
                grid.addView(queue, new LinearLayout.LayoutParams(
                    LinearLayout.LayoutParams.MATCH_PARENT,
                    LinearLayout.LayoutParams.WRAP_CONTENT));
            }
            final int id = i;
            final View c = powerCard(act, i);
            c.setOnClickListener(new View.OnClickListener() {
                @Override public void onClick(View v) {
                    if (isAction(id)) {
                        nSetPower(id, 1);
                        flashActionCard(act, c, id);
                        return;
                    }
                    if (isWorldState(id)) {
                        toggleWorldState(act, id);
                        paintPowerCard(act, c, id);
                        return;
                    }
                    sPowerLevels[id] = (sPowerLevels[id] + 1) % (POWER_LEVELS[id].length + 1);
                    nSetPower(id, sPowerLevels[id]);
                    paintPowerCard(act, c, id);
                    updateActiveCount(activeLabel);
                }
            });
            cards[i] = c;
            LinearLayout.LayoutParams cp = new LinearLayout.LayoutParams(
                0, LinearLayout.LayoutParams.MATCH_PARENT, 1f);
            cp.setMargins(px(act, 3), px(act, 3), px(act, 3), px(act, 3));
            queue.addView(c, cp);
        }
        // Fila incompleta: o que falta vira espaco, para os cartoes da ultima
        // fila terem a largura dos de cima.
        int remainder = (columns - POWER_NAME.length % columns) % columns;
        for (int i = 0; i < remainder; i++) {
            queue.addView(new View(act), new LinearLayout.LayoutParams(0, 1, 1f));
        }

        ScrollView scroll = new ScrollView(act);
        scroll.addView(grid);
        content.addView(scroll, new LinearLayout.LayoutParams(
            LinearLayout.LayoutParams.MATCH_PARENT, 0, 1f));
        updateActiveCount(activeLabel);
    }

    /** As partes de um cartao de poder, para repintar sem remontar. */
    private static final class PowerCardViews {
        TextView nameLabel, levelLabel, desc;
        ImageView icon;
    }

    private static View powerCard(Activity act, int id) {
        PowerCardViews k = new PowerCardViews();
        LinearLayout c = new LinearLayout(act);
        c.setOrientation(LinearLayout.HORIZONTAL);
        c.setGravity(Gravity.CENTER_VERTICAL);
        c.setPadding(px(act, 8), px(act, 6), px(act, 8), px(act, 6));
        String res = powerRes(id);
        k.icon = icon(act, res != null ? sprite(act, res) : spriteJa(act, false, POWER_ICON[id]), 30);
        c.addView(k.icon);

        LinearLayout texts = new LinearLayout(act);
        texts.setOrientation(LinearLayout.VERTICAL);
        texts.setPadding(px(act, 8), 0, 0, 0);
        LinearLayout topRow = new LinearLayout(act);
        topRow.setOrientation(LinearLayout.HORIZONTAL);
        topRow.setGravity(Gravity.BOTTOM);
        k.nameLabel = text(act, POWER_NAME[id], 13, INK);
        k.nameLabel.setSingleLine(true);
        k.levelLabel = text(act, "", 11, GRASS_LIT);
        k.levelLabel.setPadding(px(act, 6), 0, 0, 0);
        topRow.addView(k.nameLabel);
        topRow.addView(k.levelLabel);
        texts.addView(topRow);
        k.desc = text(act, POWER_DESC[id], 9, INK_DIM);
        texts.addView(k.desc);
        c.addView(texts, new LinearLayout.LayoutParams(
            0, LinearLayout.LayoutParams.WRAP_CONTENT, 1f));

        c.setTag(k);
        paintPowerCard(act, c, id);
        return c;
    }

    /** Desligado e painel escuro; ligado e verde, como o jogo marca o que esta ativo. */
    private static void paintPowerCard(Activity act, View c, int id) {
        PowerCardViews k = (PowerCardViews) c.getTag();
        if (isWorldState(id)) { paintWorldCard(act, c, k, id); return; }
        int n = sPowerLevels[id];
        boolean on = n > 0;
        c.setBackground(panel(act, on ? GRASS : PANEL_DARK, OUTLINE));
        k.levelLabel.setText(on && POWER_LEVELS[id].length > 1 ? POWER_LEVELS[id][n - 1] : "");
        k.levelLabel.setTextColor(on ? 0xFFFFF36B : GRASS_LIT);
        k.desc.setTextColor(on ? INK : INK_DIM);
    }

    /**
     * Hardmode verde quando ligado; dificuldade verde acima do Classico, com o
     * icone do modo em que o mundo esta.
     */
    private static void paintWorldCard(Activity act, View c, PowerCardViews k, int id) {
        int v = sWorld[id == P_HARDMODE ? 0 : 1];
        boolean on = v > 0;
        if (id == P_DIFFICULTY) {
            int m = v < 0 ? 0 : Math.min(v, GAME_MODE_ICON.length - 1);
            setBitmap(act, k.icon, sprite(act, GAME_MODE_ICON[m]));
        }
        c.setBackground(panel(act, on ? GRASS : PANEL_DARK, OUTLINE));
        String label = v < 0 ? "" : id == P_HARDMODE ? (v == 1 ? "Ligado" : "")
                                                     : GAME_MODE[Math.min(v, GAME_MODE.length - 1)];
        k.levelLabel.setText(label);
        k.levelLabel.setTextColor(on ? 0xFFFFF36B : GRASS_LIT);
        k.desc.setText(v < 0 ? "Entre num mundo" : POWER_DESC[id]);
        k.desc.setTextColor(on ? INK : INK_DIM);
    }

    /**
     * Acao executada: o cartao fica verde com "Feito!" e volta sozinho. O jogo
     * roda o pedido no proximo quadro; se falhar, o painel de erro abre.
     */
    private static void flashActionCard(final Activity act, final View c, final int id) {
        PowerCardViews k = (PowerCardViews) c.getTag();
        c.setBackground(panel(act, GRASS, OUTLINE));
        k.levelLabel.setText("Feito!");
        k.levelLabel.setTextColor(0xFFFFF36B);
        k.desc.setTextColor(INK);
        c.postDelayed(new Runnable() {
            @Override public void run() {
                paintPowerCard(act, c, id);
            }
        }, ACTION_FLASH_MS);
    }

    private static void updateActiveCount(TextView t) {
        int n = 0;
        for (int v : sPowerLevels) if (v > 0) n++;
        t.setText(n == 0 ? "" : n == 1 ? "1 ligado" : n + " ligados");
    }

    // --- painel de erro ------------------------------------------------------
    //
    // Chamado do nativo quando aparece o primeiro BL_ERROR. Quem joga no
    // celular nao tem logcat: sem isto, um mod que quebra vira "nao funcionou"
    // sem texto nenhum. Mostra o log, com OK e Copiar.

    public static void showError(final String text) {
        final Activity act = sActivity;
        if (act == null) return;
        act.runOnUiThread(new Runnable() {
            @Override public void run() {
                try { buildError(act, text); } catch (Throwable t) { /* nunca derruba o jogo */ }
            }
        });
    }

    /**
     * O erro vai num AlertDialog, nao numa caixa nossa.
     *
     * O menu de cheats e desenhado a mao porque tem de parecer o Terraria. Um
     * erro nao: quem esta lendo quer ler, e o dialogo do sistema ja traz rolagem
     * que se ajusta a tela, botao Voltar, tema claro/escuro e o desenho que a
     * pessoa reconhece. A caixa que havia aqui era 300x200 dp fixos — em tela
     * de tablet sobrava borda, em tela pequena deitada cortava —, o OK so fazia
     * setVisibility(GONE) e deixava a View pendurada na hierarquia para sempre,
     * e Voltar nao fechava nada.
     *
     * Nao existe "diálogo de erro do sistema" para chamar: aquele "o app parou"
     * e gerado pelo system server para excecao nao tratada, e nenhum app o
     * invoca. AlertDialog e o componente padrao equivalente.
     */
    private static void buildError(final Activity act, String text) {
        if (act.isFinishing()) return;
        final String body = text;

        final AlertDialog dlg = new AlertDialog.Builder(act)
            .setTitle("Bunny Loader — erro")
            .setMessage(body)
            .setPositiveButton("OK", null)
            .setNeutralButton("Copiar", null)
            .create();
        dlg.show();

        // Todo botao de AlertDialog fecha o dialogo ao ser tocado, e Copiar nao
        // pode: quem copia o log quase sempre quer continuar lendo. Trocar o
        // ouvinte DEPOIS do show() e a forma de ter um botao que nao fecha.
        Button copyButton = dlg.getButton(AlertDialog.BUTTON_NEUTRAL);
        if (copyButton == null) return;
        copyButton.setOnClickListener(new View.OnClickListener() {
            @Override public void onClick(View v) {
                ClipboardManager cm =
                    (ClipboardManager) act.getSystemService(Activity.CLIPBOARD_SERVICE);
                if (cm != null) cm.setPrimaryClip(ClipData.newPlainText("bunny", body));
                Toast.makeText(act, "Log copiado", Toast.LENGTH_SHORT).show();
            }
        });
    }
}
