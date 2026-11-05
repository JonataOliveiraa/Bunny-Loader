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
import android.graphics.LinearGradient;
import android.graphics.Shader;
import android.graphics.Paint;
import android.graphics.Rect;
import android.text.TextPaint;
import android.text.Layout;
import android.text.SpannableStringBuilder;
import android.text.Spanned;
import android.text.style.TypefaceSpan;
import android.graphics.Typeface;
import android.graphics.drawable.BitmapDrawable;
import android.graphics.drawable.Drawable;
import android.graphics.ColorFilter;
import android.graphics.Path;
import android.graphics.PixelFormat;
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
import android.widget.GridView;
import android.widget.HorizontalScrollView;
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

    static String tr(String key, Object... args) { return MenuLocalization.text(key, args); }

    // Implementados em C++ -> bl::runtime::requestGive / requestSpawn.
    public static native void nOnGive(int type, int stack);
    public static native void nOnSpawn(int type, int count);
    /** Nomes vindos da Localization do jogo. null enquanto ainda moem. */
    public static native String[] nItemNames();
    public static native String[] nNpcNames();
    /** Sobe quando a troca de idioma do jogo rele os nomes; negativa enquanto rele. */
    public static native int nNamesGeneration();
    /** Quadros da tira de cada NPC (Main.npcFrameCount). */
    public static native int[] nNpcFrames();
    /** Secao de cada item, pelos campos do jogo (runtime::ItemClass). */
    public static native byte[] nItemClasses();
    /** NPCID.Count: daqui para cima e NPC de mod. */
    public static native int nVanillaNpcCount();
    /** O PNG de cada NPC de mod, na ordem do tipo. */
    public static native String[] nModNpcTextures();
    /** Teto de pilha de cada item: 1 para arma e equipamento (runtime::itemMaxStacks). */
    /** bl::runtime::setPower — o nivel de um superpoder, 0 = desligado. */
    public static native void nSetPower(int id, int level);
    /** bl::runtime::inWorld — o jogador esta num mundo (nao na tela de titulo). */
    public static native boolean nInWorld();
    public static native boolean nCheatsBlocked();
    /** bl::runtime::setTimeOfDay — 0 amanhecer, 1 meio-dia, 2 anoitecer, 3 meia-noite. */
    public static native void nSetTimeOfDay(int which);
    /** {hardmode, modo, minuto 0..1439, chuva 0..100, vento 0..100}; -1 fora do mundo. */
    public static native int[] nWorldState();
    /** ItemID.Count: dali para cima, o id e de item de mod. */
    public static native int nVanillaItemCount();
    /** PNG de cada item de mod, indice = tipo - nVanillaItemCount(). */
    public static native String[] nModItemTextures();
    /** Categorias do catalogo de mod, achatadas: nome, icone, nome, icone... */
    public static native String[] nModCategories();
    public static native int[] nModCategoryItems(int category);
    /** Subcategoria de cada item dentro da secao (runtime::subOf; 0 = Outros). */
    public static native byte[] nItemSubClasses();
    /** Classe de cada NPC (runtime::NpcClass). */
    public static native byte[] nNpcClasses();
    /** Nome de cada buff (Lang.GetBuffName); [0] vazio. */
    public static native String[] nBuffNames();
    /** Classe de cada buff (runtime::BuffClass). */
    public static native byte[] nBuffClasses();
    /** O buff `type` no jogador local por `seconds`. */
    public static native void nOnBuff(int type, int seconds);
    /** BuffID.Count: dali para cima, buff de mod. */
    public static native int nVanillaBuffCount();
    /** PNG de cada buff de mod, indice = tipo - nVanillaBuffCount(). */
    public static native String[] nModBuffTextures();
    /** Console JS: o codigo roda no proximo quadro, na thread do jogo (script/api/Console.h). */
    public static native void nConsoleRun(String code);
    /** A saida do console desde a ultima vez; a especie no primeiro caractere (Console.h). */
    public static native String[] nConsoleTake();
    /** Reiniciar o jogo: pede (salvando antes, se `save`) e pergunta se ja pode fechar. */
    public static native void nRequestRestart(boolean save);
    /** Editor em tela cheia: o mundo para (Main.CanPauseGame, menu/DevTools.h). */
    public static native void nSetGameFrozen(boolean on);
    public static native boolean nRestartReady();

    // --- paleta ---
    static final int PANEL      = 0xFF3F5297;
    static final int PANEL_DARK = 0xFF2C3A6B;
    static final int PANEL_LIT  = 0xFF5468B4;
    static final int OUTLINE    = 0xFF131625;
    static final int GRASS      = 0xFF22A851;
    static final int GRASS_LIT  = 0xFF2EED52;
    static final int INK        = 0xFFFFFFFF;
    static final int INK_DIM    = 0xFFC3CBEA;
    static final int SCRIM      = 0xC0000000;
    // Botao de acao (o +, a bandeira, os da coluna do X): um tom acima do
    // painel, com borda clara. No escuro de antes ele sumia no fundo.
    static final int BUTTON      = 0xFF5268B6;
    static final int WELL = 0xFF2C3A6B;
    static final int GOLD = 0xFFE9B43A, GOLD_OUTLINE = 0xFF3A2608;
    static final int SELECTED = 0xFF4A5FB0, SELECTED_OUTLINE = 0xFFEBE27F;

    private static Activity sActivity;
    static View sOverlay;

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

    /**
     * O que o menu sabe do jogo. Nao muda depois de montado: a troca de idioma
     * monta outro (ver isStale).
     */
    private static final class Catalog {
        /** nNamesGeneration de quando os nomes foram copiados. */
        int generation;
        String[] itemNames, npcNames;
        /** Os mesmos nomes, minusculos e sem acento, para a busca. */
        String[] itemSearch, npcSearch;
        /** Ids de cada secao de item (indice = CL_*), na ordem do jogo. */
        int[][] byClass;
        int[] allItems, allNpcs;
        /** Quadros da tira de cada NPC (Main.npcFrameCount). */
        int[] npcFrames;
        /** [classe][subcategoria] -> ids (runtime::subOf). */
        int[][][] bySub;
        /** [classe de NPC] -> ids (runtime::NpcClass). */
        int[][] npcByClass;
        String[] buffNames, buffSearch;
        int[] allBuffs;
        /** [classe de buff] -> ids (runtime::BuffClass). */
        int[][] buffByClass;
    }

    // Contrato com runtime::NpcClass e runtime::BuffClass (MenuCatalog.h).
    private static final int NPC_OTHER = 0, NPC_BOSS = 1, NPC_MONSTER = 2, NPC_TOWN = 3,
        NPC_CRITTER = 4, N_NPC_CLASSES = 5;
    private static final int N_BUFF_CLASSES = 7;

    /**
     * O nome de cada subcategoria, por secao (indice = o numero que o nativo
     * da, runtime::subOf). A [0] e sempre "Outros" e vai por ultimo na tela.
     */
    private static final String[][] SUB_NAMES = new String[N_CLASSES][];
    static {
        SUB_NAMES[CL_OTHER] = new String[] {"Other", "Materials", "PaintsAndDyes", "Bait",
            "Mounts", "BagsAndCrates"};
        SUB_NAMES[CL_MELEE] = new String[] {"Other", "Swords", "Shortswords", "Spears",
            "FlailsAndMaces", "Yoyos", "Boomerangs"};
        SUB_NAMES[CL_RANGED] = new String[] {"Other", "Bows", "Guns", "RocketLaunchers",
            "Throwables"};
        SUB_NAMES[CL_MAGIC] = new String[] {"Other", "Staves"};
        SUB_NAMES[CL_SUMMON] = new String[] {"Other", "Minions", "Sentries", "Whips"};
        SUB_NAMES[CL_AMMO] = new String[] {"OtherFeminine", "Arrows", "Bullets", "Rockets"};
        SUB_NAMES[CL_TOOL] = new String[] {"OtherFeminine", "Pickaxes", "Axes", "Hammers",
            "DrillsAndChainsaws", "FishingRods", "Hooks"};
        SUB_NAMES[CL_ACCESSORY] = new String[] {"Other", "Wings", "Footwear", "Shields"};
        SUB_NAMES[CL_ARMOR] = new String[] {"OtherFeminine", "Helmets", "Chestplates", "Leggings", "Vanity"};
        SUB_NAMES[CL_POTION] = new String[] {"OtherFeminine", "Healing", "Mana", "Food", "Buffs"};
        SUB_NAMES[CL_BLOCK] = new String[] {"Other", "Blocks", "Walls", "Torches", "FurnitureAndObjects"};
    }
    private static final String[] NPC_CLASS_NAMES = {"Other", "Bosses", "Monsters", "TownNPCs",
        "Critters"};
    private static final String[] BUFF_CLASS_NAMES = {"Other", "Buffs", "Debuffs", "Food",
        "Flasks", "PetsAndLights", "SummonsAndMounts"};

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
        String[] items, npcs, buffs;
        int generation;
        // Uma releitura (troca de idioma) no meio da copia misturaria idiomas:
        // copia de novo ate a geracao ser a mesma antes e depois.
        do {
            while ((generation = nNamesGeneration()) < 0) sleepQuietly(80);
            while ((items = nItemNames()) == null) sleepQuietly(80);
            while ((npcs = nNpcNames()) == null) sleepQuietly(80);
            buffs = nBuffNames();
        } while (nNamesGeneration() != generation);
        final long t1 = SystemClock.elapsedRealtime();
        Catalog c = new Catalog();
        c.generation = generation;
        c.itemNames = items;
        c.npcNames = npcs;
        c.npcFrames = nNpcFrames();
        c.allItems = sequence(items.length);
        c.allNpcs = sequence(npcs.length);
        c.byClass = splitByClass(nItemClasses(), items);
        c.bySub = splitBySub(nItemClasses(), nItemSubClasses(), items);
        c.npcByClass = splitGroups(nNpcClasses(), npcs, N_NPC_CLASSES);
        c.itemSearch = normalizeAll(items);
        c.npcSearch = normalizeAll(npcs);
        c.buffNames = buffs != null ? buffs : new String[0];
        c.buffSearch = normalizeAll(c.buffNames);
        c.allBuffs = named(c.buffNames);
        c.buffByClass = splitGroups(nBuffClasses(), c.buffNames, N_BUFF_CLASSES);
        Log.i("BunnyLoader", "menu: catalogo pronto; esperou o nativo " + (t1 - t0)
            + " ms, montou em " + (SystemClock.elapsedRealtime() - t1) + " ms");
        return c;
    }

    /** O idioma do jogo mudou depois que o catalogo foi montado. */
    private static boolean isStale() {
        Catalog c = sCatalog;
        return c != null && c.generation != nNamesGeneration();
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

    /** [classe][sub] -> ids, numa passada por classe. Sem subcategorias, tudo vazio. */
    private static int[][][] splitBySub(byte[] cls, byte[] sub, String[] names) {
        int[][][] out = new int[N_CLASSES][][];
        for (int c = 0; c < N_CLASSES; c++) {
            int subs = SUB_NAMES[c].length;
            int[] counts = new int[subs];
            int end = cls == null || sub == null ? 0 : Math.min(Math.min(cls.length, sub.length), names.length);
            for (int i = 1; i < end; i++) {
                if ((cls[i] & 0xFF) != c || names[i] == null) continue;
                int k = sub[i] & 0xFF;
                counts[k < subs ? k : 0]++;
            }
            out[c] = new int[subs][];
            for (int k = 0; k < subs; k++) out[c][k] = new int[counts[k]];
            int[] at = new int[subs];
            for (int i = 1; i < end; i++) {
                if ((cls[i] & 0xFF) != c || names[i] == null) continue;
                int k = sub[i] & 0xFF;
                if (k >= subs) k = 0;
                out[c][k][at[k]++] = i;
            }
        }
        return out;
    }

    /** ids com nome, separados pela classe de cada um (NPC, buff). */
    private static int[][] splitGroups(byte[] cls, String[] names, int n) {
        int[] counts = new int[n];
        int end = cls == null ? 0 : Math.min(cls.length, names.length);
        for (int i = 1; i < end; i++) {
            int c = cls[i] & 0xFF;
            if (c < n && names[i] != null) counts[c]++;
        }
        int[][] out = new int[n][];
        for (int c = 0; c < n; c++) out[c] = new int[counts[c]];
        int[] k = new int[n];
        for (int i = 1; i < end; i++) {
            int c = cls[i] & 0xFF;
            if (c < n && names[i] != null) out[c][k[c]++] = i;
        }
        return out;
    }

    /** Os ids 1..n-1 que tem nome. */
    private static int[] named(String[] names) {
        int n = 0;
        for (int i = 1; i < names.length; i++) if (names[i] != null) n++;
        int[] v = new int[n];
        int k = 0;
        for (int i = 1; i < names.length; i++) if (names[i] != null) v[k++] = i;
        return v;
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

    private static int sVanilla = -1, sVanillaNpc = -1, sVanillaBuff = -1;
    private static String[] sModTextures, sModNpcTextures, sModBuffTextures;

    private static void loadModItems() {
        if (sVanilla >= 0) return;
        sVanilla = nVanillaItemCount();
        sModTextures = nModItemTextures();
        sVanillaNpc = nVanillaNpcCount();
        sModNpcTextures = nModNpcTextures();
        sVanillaBuff = nVanillaBuffCount();
        sModBuffTextures = nModBuffTextures();
    }

    // O que um sprite mostra: define a pasta do PNG e a tabela de mod.
    private static final int SPR_ITEM = 0, SPR_NPC = 1, SPR_BUFF = 2;
    private static final String[] SPR_DIR = {"item/", "npc/", "buff/"};

    /** O PNG do item, NPC ou buff de mod `id`, ou null se o id e do jogo. */
    private static String modTexture(int kind, int id) {
        int vanilla = kind == SPR_NPC ? sVanillaNpc : kind == SPR_BUFF ? sVanillaBuff : sVanilla;
        String[] files = kind == SPR_NPC ? sModNpcTextures : kind == SPR_BUFF ? sModBuffTextures : sModTextures;
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
            int kind = "npc".equals(f[c + 5]) ? Section.NPC : "buff".equals(f[c + 5]) ? Section.BUFF : Section.ITEM;
            folders.add(Section.modFolder(f[c + 3], f[c + 4], kind, ids));
        }
        if (mod != null && !folders.isEmpty()) {
            l.add(Section.modGroup(modName, modIcon, folders.toArray(new Section[0])));
        }
    }

    // ------------------------------ secoes ------------------------------

    /**
     * Uma secao do menu: lista de item, lista de NPC, a grade de poderes, ou a
     * entrada de um mod (FOLDERS), que abre as pastas dele.
     */
    private static final class Section {
        static final int ITEM = 0, NPC = 1, POWER = 2, FOLDERS = 3, BUFF = 4;

        final String title;
        final int kind;
        /** Item: a classe do jogo que entra aqui, ou ALL_CLASSES. */
        final int itemClass;
        /** Lista escolhida a mao (chefes, moradores). null = vem do catalogo. */
        final String[] fixedNames;
        final int[] fixedIds;
        /** Icone: drawable do app, ou sprite de item quando nao ha drawable. */
        final String icon;
        int iconItem;
        /** Ou um PNG em disco (o icone que um mod deu a categoria dele). */
        String iconFile;
        /** De que e o sprite do icone (SPR_*; iconItem e o id). */
        int iconKind = SPR_ITEM;
        /** Item: a subcategoria (-1 = a classe toda). NPC e buff: a classe (-1 = todos). */
        int subClass = -1;
        /** Onde o slider comeca. Espada vem uma; bloco vem a pilha. */
        final int defaultQty;
        /** FOLDERS: as pastas do mod. */
        Section[] children;
        int tab;
        boolean iconTabs, modsRoot;

        /** Os ids que a lista mostra, na ordem. Do catalogo, sem copia. */
        int[] ids;
        private String[] fixedSearch;

        private Section(String title, int kind, int itemClass,
                        String[] fixedNames, int[] fixedIds, String icon, int iconItem,
                        int defaultQty) {
            this.title = title; this.kind = kind;
            this.itemClass = itemClass; this.fixedNames = fixedNames; this.fixedIds = fixedIds;
            this.icon = icon; this.iconItem = iconItem; this.defaultQty = defaultQty;
        }

        static Section items(String title, int itemClass, String icon, int iconItem, int qty) {
            return new Section(title, ITEM, itemClass, null, null, icon, iconItem, qty);
        }

        /** Uma subcategoria de item (sub = -1: a classe inteira, "Todos"). */
        static Section itemSub(String title, int itemClass, int sub, int qty) {
            Section s = new Section(title, ITEM, itemClass, null, null, null, 0, qty);
            s.subClass = sub;
            return s;
        }

        /** Classe de NPC (-1 = todos). */
        static Section npcClass(String title, int cls) {
            Section s = new Section(title, NPC, ALL_CLASSES, null, null, null, 0, 1);
            s.subClass = cls;
            s.iconKind = SPR_NPC;
            return s;
        }

        /** Classe de buff (-1 = todos). */
        static Section buffClass(String title, int cls) {
            Section s = new Section(title, BUFF, ALL_CLASSES, null, null, null, 0, 5);
            s.subClass = cls;
            s.iconKind = SPR_BUFF;
            return s;
        }

        /**
         * Uma entrada da coluna que abre subcategorias (Corpo a corpo -> Todos,
         * Espadas...). As listas vem do catalogo: as vazias somem quando ele fica
         * pronto.
         */
        static Section group(String title, String icon, int iconItem, int iconKind,
                             Section[] children) {
            Section s = new Section(title, FOLDERS, ALL_CLASSES, null, null, icon, iconItem, 0);
            s.iconKind = iconKind;
            s.children = children;
            s.fromCatalog = true;
            s.childOne = "CategoryCountOne";
            s.childMany = "CategoryCountOther";
            return s;
        }

        /** FOLDERS cujas pastas vem do catalogo (e nao de um mod). */
        boolean fromCatalog;
        /** FOLDERS: as chaves que contam o que tem dentro ("1 pasta", "3 pastas"). */
        String childOne = "FolderCountOne", childMany = "FolderCountOther";

        /** A entrada "Mods": um por linha. Icone do primeiro mod. */
        static Section mods(Section[] mods) {
            Section first = mods[0];
            Section s = new Section(tr("Mods"), FOLDERS, ALL_CLASSES, null, null, "ic_folder", first.iconItem, 0);
            s.children = mods;
            s.modsRoot = true;
            s.childOne = "ModCountOne";
            s.childMany = "ModCountOther";
            return s;
        }

        static Section npcs(String title, String[] n, int[] i, String icon) {
            return new Section(title, NPC, ALL_CLASSES, n, i, icon, 0, 1);
        }

        /** POWER: os grupos de POWER_GROUPS que a pagina mostra. */
        int[] powerGroups;

        static Section powers(String title, int iconItem, int... groups) {
            Section s = new Section(title, POWER, ALL_CLASSES, null, null, null, iconItem, 0);
            s.powerGroups = groups;
            return s;
        }

        /** Uma pasta de mod: os itens (NPCs, buffs) que estao nela. */
        static Section modFolder(String title, String icon, int kind, int[] ids) {
            Section s = new Section(title, kind, ALL_CLASSES, null, ids,
                null, ids[0], kind == BUFF ? 5 : 1);
            s.iconFile = icon == null || icon.length() == 0 ? null : icon;
            s.iconKind = kind == NPC ? SPR_NPC : kind == BUFF ? SPR_BUFF : SPR_ITEM;
            return s;
        }

        /** A entrada do mod na coluna: abre as pastas. Icone do mod, ou o da 1a pasta. */
        static Section modGroup(String title, String icon, Section[] folders) {
            Section first = folders[0];
            Section s = new Section(title, FOLDERS, ALL_CLASSES, null, null, null,
                first.iconItem, 0);
            s.iconFile = icon == null || icon.length() == 0 ? first.iconFile : icon;
            s.iconKind = first.iconKind;
            s.children = folders;
            return s;
        }

        boolean npc() { return kind == NPC; }
        boolean buff() { return kind == BUFF; }
        /** De que e o sprite de cada linha. */
        int spriteKind() { return npc() ? SPR_NPC : buff() ? SPR_BUFF : SPR_ITEM; }

        /** Posicoes visiveis depois do filtro. null = todas. */
        int[] filtered;

        int total() { return ids == null ? 0 : ids.length; }
        int count() { return filtered != null ? filtered.length : total(); }
        int indexAt(int position) { return filtered != null ? filtered[position] : position; }

        String nameAt(int k) {
            if (fixedNames != null) return fixedNames[k];
            return (npc() ? sCatalog.npcNames : buff() ? sCatalog.buffNames : sCatalog.itemNames)[ids[k]];
        }

        String searchText(int k) {
            if (fixedNames != null) {
                if (fixedSearch == null) fixedSearch = normalizeAll(fixedNames);
                return fixedSearch[k];
            }
            return (npc() ? sCatalog.npcSearch : buff() ? sCatalog.buffSearch : sCatalog.itemSearch)[ids[k]];
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
            if (fixedIds != null) ids = fixedIds;
            else if (buff()) ids = subClass < 0 ? c.allBuffs : c.buffByClass[subClass];
            else if (npc()) ids = subClass < 0 ? c.allNpcs
                : subClass == NPC_BOSS ? bosses(c) : c.npcByClass[subClass];
            else if (itemClass == ALL_CLASSES) ids = c.allItems;
            else ids = subClass < 0 ? c.byClass[itemClass] : c.bySub[itemClass][subClass];
            if (ids == null) ids = new int[0];
            // Subcategoria sem icone proprio: o primeiro da lista.
            if (icon == null && iconFile == null && iconItem == 0 && ids.length > 0) iconItem = ids[0];
            return true;
        }
    }

    /**
     * Chefes: a lista a mao do jogo (so a cabeca de quem tem partes, e o que
     * da para invocar) e os NPCs de mod com a flag boss.
     */
    private static int[] bosses(Catalog c) {
        int[] fixed = CheatData.BOSSES_I;
        int[] mod = c.npcByClass[NPC_BOSS];
        int extra = 0;
        for (int id : mod) if (id >= sVanillaNpc) extra++;
        int[] out = Arrays.copyOf(fixed, fixed.length + extra);
        int k = fixed.length;
        for (int id : mod) if (id >= sVanillaNpc) out[k++] = id;
        return out;
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
     * O aside: Superpoderes, Eventos, Itens, NPCs, Buffs e Mods. Cada um abre as
     * categorias dele, e uma categoria pode abrir subcategorias (Itens ->
     * Corpo a corpo -> Espadas; Mods -> o mod -> as pastas dele). "Todos"
     * fica NA categoria, em cima: e a mesma lista das de baixo, sem filtro.
     */
    private static Section[] sections() {
        ArrayList<Section> l = new ArrayList<Section>();
        // Sigilo Celestial. A Estrela Cadente era a primeira ideia, mas o PNG
        // dela e uma tira de 8 quadros e saia como um risco.
        l.add(Section.powers(tr("Superpowers"), 3601, G_PLAYER, G_WORLD, G_EVENTS));

        Section[] items = {
            Section.items(tr("AllItems"), ALL_CLASSES, "ic_sec_tudo_item", 0, 9999),
            classGroup(tr("Melee"), CL_MELEE, "ic_sec_melee", 0, 1),
            classGroup(tr("Ranged"), CL_RANGED, "ic_sec_ranged", 0, 1),
            classGroup(tr("Magic"), CL_MAGIC, "ic_sec_magia", 0, 1),
            classGroup(tr("Summoning"), CL_SUMMON, "ic_sec_invoc", 0, 1),
            classGroup(tr("Ammo"), CL_AMMO, null, 40, 9999),          // Flecha de Madeira
            classGroup(tr("Tools"), CL_TOOL, null, 3521, 1),       // Picareta de Ouro
            classGroup(tr("Accessories"), CL_ACCESSORY, "ic_sec_acess", 0, 1),
            classGroup(tr("Armor"), CL_ARMOR, null, 231, 1),         // Elmo Derretido
            classGroup(tr("PotionsAndFood"), CL_POTION, "ic_sec_util", 0, 9999),
            classGroup(tr("BlocksAndFurniture"), CL_BLOCK, "ic_sec_blocos", 0, 9999),
            classGroup(tr("Other"), CL_OTHER, null, 29, 9999),          // Cristal de Vida
        };
        Section itemGroup = Section.group(tr("Items"), "ic_sec_tudo_item", 0, SPR_ITEM, items);
        itemGroup.iconTabs = true;
        l.add(itemGroup);

        Section[] npcs = {
            Section.npcClass(tr("FilterAll"), -1), Section.npcClass(tr("Bosses"), NPC_BOSS),
            Section.npcClass(tr("Monsters"), NPC_MONSTER), Section.npcClass(tr("TownNPCs"), NPC_TOWN),
            Section.npcClass(tr("Critters"), NPC_CRITTER), Section.npcClass(tr("Other"), NPC_OTHER),
        };
        l.add(Section.group(tr("NPCs"), "ic_sec_tudo_npc", 0, SPR_NPC, npcs));

        Section[] buffs = new Section[BUFF_CLASS_NAMES.length + 1];
        buffs[0] = Section.buffClass(tr("FilterAll"), -1);
        for (int c = 1; c < BUFF_CLASS_NAMES.length; c++) buffs[c] = Section.buffClass(tr(BUFF_CLASS_NAMES[c]), c);
        buffs[BUFF_CLASS_NAMES.length] = Section.buffClass(tr(BUFF_CLASS_NAMES[0]), 0);
        l.add(Section.group(tr("Buffs"), null, 3, SPR_BUFF, buffs));   // Regeneracao

        // O que os mods trouxeram: um por linha, cada um com as pastas dele.
        ArrayList<Section> mods = new ArrayList<Section>();
        addModSections(mods);
        if (!mods.isEmpty()) l.add(Section.mods(mods.toArray(new Section[0])));
        return l.toArray(new Section[0]);
    }

    /** Uma classe de item como entrada: Todos, e as subcategorias; "Outros" por ultimo. */
    private static Section classGroup(String title, int cls, String icon, int iconItem, int qty) {
        String[] names = SUB_NAMES[cls];
        Section[] children = new Section[names.length + 1];
        children[0] = Section.itemSub(tr("FilterAll"), cls, -1, qty);
        for (int k = 1; k < names.length; k++) children[k] = Section.itemSub(tr(names[k]), cls, k, qty);
        children[names.length] = Section.itemSub(tr(names[0]), cls, 0, qty);
        return Section.group(title, icon, iconItem, SPR_ITEM, children);
    }

    // ------------------------------ poderes ------------------------------
    //
    // A ordem e o contrato com bl::runtime::Power (Powers.h).

    private static final String[] POWER_NAME = {
        "ExtraDamage", "SuperSpeed", "SuperJump", "FreezeTime", "Invincible",
        "InfiniteMana", "InfiniteJumps", "TurboMining", "FullVision",
        "Fly", "XRay", "InfiniteMinions", "Rain", "Wind", "Bestiary",
        "Enemies", "MapTeleport", "ClearInventory", "RevealMap",
        "Hardmode", "Difficulty", "QuickRespawn", "RemoveEnemies",
        "StopBiomeSpread", "StopEvents",
        "BloodMoon", "SolarEclipse", "SlimeRain", "GoblinArmy", "FrostLegion",
        "Pirates", "MartianMadness", "PumpkinMoon", "FrostMoon", "Party",
        "Sandstorm",
    };
    private static final String[] POWER_DESC = {
        "ExtraDamageDescription",
        "SuperSpeedDescription",
        "SuperJumpDescription",
        "FreezeTimeDescription",
        "InvincibleDescription",
        "InfiniteManaDescription",
        "InfiniteJumpsDescription",
        "TurboMiningDescription",
        "FullVisionDescription",
        "FlyDescription",
        "XRayDescription",
        "InfiniteMinionsDescription",
        "RainDescription",
        "WindDescription",
        "BestiaryDescription",
        "EnemiesDescription",
        "MapTeleportDescription",
        "ClearInventoryDescription",
        "RevealMapDescription",
        "HardmodeDescription",
        "DifficultyDescription",
        "QuickRespawnDescription",
        "RemoveEnemiesDescription",
        "StopBiomeSpreadDescription",
        "StopEventsDescription",
        "BloodMoonDescription",
        "SolarEclipseDescription",
        "SlimeRainDescription",
        "GoblinArmyDescription",
        "FrostLegionDescription",
        "PiratesDescription",
        "MartianMadnessDescription",
        "MoonEventDescription",
        "MoonEventDescription",
        "PartyDescription",
        "SandstormDescription",
    };
    /** Rotulo de cada nivel. Um so = liga/desliga; nenhum = acao (ver isAction). */
    private static final String[][] POWER_LEVELS = {
        {"x2", "x5", "x10"}, {"x2", "x3"}, {"x2", "x3"},
        {"On"}, {"On"}, {"On"}, {"On"}, {"On"}, {"On"},
        {"SpeedNormal", "SpeedFast"}, {"On"}, {"On"},
        {"On"}, {"On"},     // chuva e vento: barras (isSlider)
        {},
        {"On"}, {"On"}, {}, {},   // inimigos: barra
        {"On"}, {"Classic", "Expert", "Master", "Journey"},
        {"On"},
        {},
        {"On"}, {},                  // infeccao: estado do mundo; parar eventos: acao
        {}, {}, {}, {}, {}, {}, {}, {}, {}, {}, {},   // eventos: acoes
    };
    /** Sprite de item que representa cada poder. */
    private static final int[] POWER_ICON = {
        1301,   // Emblema do Destruidor
        54,     // Botas de Hermes
        2423,   // Perna de Sapo
        3099,   // Cronometro
        58,     // Coracao
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
        1291,   // Fruta da Vida
        0,      // (acao da barra de inimigos, sem cartao)
        66,     // Po de Purificacao
        3117,   // Vela da Paz
        4271,   // Lagrima Sangrenta
        2767,   // Tabua Solar
        23,     // Gel
        361,    // Estandarte de Batalha Goblin
        602,    // Globo de Neve
        672,    // Cutelo
        2749,   // Cajado Xeno
        1844,   // Medalhao da Lua de Abobora
        1958,   // Presente Travesso
        3749,   // Presente de Festa
        857,    // Tempestade de Areia na Garrafa
    };

    // Ids que o Java precisa conhecer, na ordem de bl::runtime::Power.
    private static final int P_NO_SPAWNS = 15;
    private static final int P_HARDMODE = 19;
    private static final int P_DIFFICULTY = 20;
    private static final int P_TIME_STOP = 3;
    private static final int P_RAIN = 12;
    private static final int P_WIND = 13;
    private static final int P_CLEAR_ENEMIES = 22;
    private static final int P_STOP_INFECTION = 23;

    /**
     * Poder que e uma BARRA da coluna do X (chuva, vento, inimigos): o nivel e
     * a posicao 0..100 mais um, e 0 devolve ao jogo (Powers.h, kSliderLevels).
     */
    private static final int SLIDER_LEVELS = 101;

    private static boolean isSlider(int id) {
        return id == P_RAIN || id == P_WIND || id == P_NO_SPAWNS;
    }

    private static int maxLevel(int id) {
        return isSlider(id) ? SLIDER_LEVELS : POWER_LEVELS[id].length;
    }

    /**
     * A grade em duas partes, na ordem mostrada. Chuva, vento, inimigos e tempo
     * parado nao estao aqui: ficam na coluna do X, junto da hora do dia.
     */
    /**
     * Os grupos, cada um com o seu titulo. Superpoderes mostra Jogador e
     * Mundo; Eventos e outra entrada da coluna (sections()).
     */
    private static final int G_PLAYER = 0, G_WORLD = 1, G_EVENTS = 2;
    private static final String[] POWER_GROUP_NAME = {"Player", "World", "Events"};
    private static final int[][] POWER_GROUPS = {
        // Imortal, Reviver rapido, Dano, Velocidade, Super pulo, Pulo infinito,
        // Voar, Mana, Lacaios, Mineracao, Visao total, Raio-X, Teleporte, Limpar
        {4, 21, 0, 1, 2, 6, 9, 5, 11, 7, 8, 10, 16, 17},
        // Revelar mapa, Bestiario, Hardmode, Dificuldade, Parar infeccao
        {18, 14, 19, 20, 23},
        // Parar eventos; Lua de Sangue, Eclipse, Chuva de slime, Goblins,
        // Legiao gelida, Piratas, Marcianos, Lua de abobora, Lua gelida,
        // Festa, Tempestade de areia
        {24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35},
    };

    /** Poder com icone de interface do jogo em vez de sprite de item. */
    private static String powerRes(int id) {
        return id == P_NO_SPAWNS ? "ic_poder_spawn" : null;
    }

    /**
     * Poder que e o ESTADO do mundo (hardmode, dificuldade, infeccao parada):
     * o cartao mostra o que o mundo esta agora, e tocar manda mudar. Nao conta
     * como ligado, e o "Desligar tudo" nao mexe — desligar o hardmode e
     * decisao, nao faxina. A infeccao parada e salva no mundo pelo jogo.
     */
    private static boolean isWorldState(int id) {
        return id == P_HARDMODE || id == P_DIFFICULTY || id == P_STOP_INFECTION;
    }

    /** A posicao de cada estado do mundo em sWorld (e em nWorldState: 0, 1 e 5). */
    private static int worldSlot(int id) {
        return id == P_HARDMODE ? 0 : id == P_DIFFICULTY ? 1 : 2;
    }

    private static final String[] GAME_MODE = {"Classic", "Expert", "Master", "Journey"};
    /** O icone de cada modo, o da criacao de mundo (UI/WorldCreation). */
    private static final String[] GAME_MODE_ICON = {
        "ic_dif_normal", "ic_dif_expert", "ic_dif_master", "ic_dif_creative",
    };
    /** A ultima leitura de nWorldState (hardmode, modo, infeccao), corrigida na hora pelo toque. */
    private static int[] sWorld = {-1, -1, -1};

    private static void readWorldState() {
        int[] w = nWorldState();
        if (w != null && w.length >= 2) sWorld = new int[] {w[0], w[1], w.length > 5 ? w[5] : -1};
    }

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
        if (id == P_HARDMODE || id == P_STOP_INFECTION) {
            int slot = worldSlot(id);
            int on = sWorld[slot];
            if (on < 0) { toast(act, tr("EnterWorldFirst")); return; }
            nSetPower(id, on == 1 ? 2 : 1);
            sWorld[slot] = on == 1 ? 0 : 1;
        } else {
            int mode = sWorld[1];
            if (mode < 0) { toast(act, tr("EnterWorldFirst")); return; }
            int next = (mode + 1) % GAME_MODE.length;
            nSetPower(id, next + 1);
            sWorld[1] = next;
        }
    }

    static void toast(Activity act, String msg) {
        Toast.makeText(act, msg, Toast.LENGTH_SHORT).show();
    }

    /** Quanto tempo o cartao de uma acao fica verde, dizendo que foi. */
    private static final long ACTION_FLASH_MS = 1500;
    /** Nivel atual. O processo do jogo morre junto com o nativo, entao os dois
     *  nascem desligados e so este lado escreve. */
    private static final int[] sPowerLevels = new int[POWER_NAME.length];

    // ---- poderes salvos ----
    //
    // O que estava ligado volta ligado na proxima vez que o jogo abre, sem
    // precisar abrir o menu: o nativo guarda o nivel e aplica quando entra no
    // mundo. Acao (roda uma vez) e estado do mundo (hardmode, dificuldade) nao
    // entram: sao pedidos, nao interruptores.

    private static final String POWER_PREFS = "bunny_powers";

    private static boolean isSaved(int id) {
        return !isAction(id) && !isWorldState(id);
    }

    /** A barra guarda em "s": um "p" de antes era um nivel de 1 a 3, nao uma posicao. */
    private static String prefKey(int id) {
        return (isSlider(id) ? "s" : "p") + id;
    }

    private static void restorePowers(Activity act) {
        android.content.SharedPreferences p = act.getSharedPreferences(POWER_PREFS, Activity.MODE_PRIVATE);
        int n = 0;
        for (int id = 0; id < sPowerLevels.length; id++) {
            if (!isSaved(id)) continue;
            int lv = p.getInt(prefKey(id), 0);
            if (lv < 0 || lv > maxLevel(id)) lv = 0;
            sPowerLevels[id] = lv;
            if (lv > 0) { nSetPower(id, lv); n++; }
        }
        if (n > 0) Log.i("BunnyLoader", "menu: " + n + " poder(es) salvo(s) religado(s)");
    }

    private static void savePowers(Activity act) {
        android.content.SharedPreferences.Editor e =
            act.getSharedPreferences(POWER_PREFS, Activity.MODE_PRIVATE).edit();
        for (int id = 0; id < sPowerLevels.length; id++) {
            if (isSaved(id)) e.putInt(prefKey(id), sPowerLevels[id]);
        }
        e.apply();
    }

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

    static float unit(Activity a) {
        if (sUnit == 0) {
            DisplayMetrics m = a.getResources().getDisplayMetrics();
            float shortSide = Math.min(m.widthPixels, m.heightPixels);
            float longSide = Math.max(m.widthPixels, m.heightPixels);
            float u = Math.max(shortSide / UNITS_TALL, MIN_DP * m.density);
            sUnit = Math.min(u, longSide / UNITS_WIDE);
        }
        return sUnit;
    }

    static int px(Activity a, float u) {
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

    private static float contentUnits(Activity a) {
        return widthUnits(a) - SIDE - RAIL - 60;
    }


    /**
     * Tamanho de texto em pixels, e nao em sp: sp soma a escala de fonte do
     * sistema, e o jogo nao soma. Com a fonte do aparelho em "grande", o menu
     * crescia e o jogo em volta nao.
     */
    static void applyTextSize(Activity a, TextView t, float size) {
        t.setTextSize(TypedValue.COMPLEX_UNIT_PX, size * TEXT_SCALE * unit(a));
    }

    private static final int[] CORNER_LARGE = {4, 2, 1, 1}, CORNER_SMALL = {2, 1};

    static final class PixelPanel extends Drawable {
        private final int fill, light, dark, outline;
        private final int[] steps, innerSteps;
        private final float b;
        private final Paint paint = new Paint();
        private final Path outer = new Path(), inner = new Path();

        PixelPanel(float artPixel, int fill, int outline, boolean raised, boolean large) {
            this.b = artPixel;
            this.fill = fill;
            this.outline = outline;
            int lit = mix(fill, 0xFFFFFFFF, 0.14f), shade = mix(fill, 0xFF0D0F1A, 0.45f);
            this.light = raised ? lit : shade;
            this.dark = raised ? shade : lit;
            this.steps = large ? CORNER_LARGE : CORNER_SMALL;
            this.innerSteps = erode(steps);
        }

        @Override protected void onBoundsChange(Rect r) {
            stepped(outer, r.left, r.top, r.right, r.bottom, steps);
            stepped(inner, r.left + b, r.top + b, r.right - b, r.bottom - b, innerSteps);
        }

        private void stepped(Path p, float l, float t, float r, float bo, int[] s) {
            p.reset();
            int n = s.length;
            if (n == 0 || r - l < b * s[0] * 2 + b || bo - t < b * n * 2 + b) {
                p.addRect(l, t, r, bo, Path.Direction.CW);
                return;
            }
            float[] xs = new float[n * 2 + 1], ys = new float[n * 2 + 1];
            xs[0] = 0; ys[0] = n * b;
            int k = 1;
            for (int i = n - 1; i >= 0; i--) {
                xs[k] = s[i] * b; ys[k++] = (i + 1) * b;
                xs[k] = s[i] * b; ys[k++] = i * b;
            }
            p.moveTo(l + xs[0], t + ys[0]);
            for (int i = 0; i < k; i++) p.lineTo(l + xs[i], t + ys[i]);
            for (int i = k - 1; i >= 0; i--) p.lineTo(r - xs[i], t + ys[i]);
            for (int i = 0; i < k; i++) p.lineTo(r - xs[i], bo - ys[i]);
            for (int i = k - 1; i >= 0; i--) p.lineTo(l + xs[i], bo - ys[i]);
            p.close();
        }

        @Override public void draw(Canvas c) {
            Rect r = getBounds();
            paint.setColor(outline);
            c.drawPath(outer, paint);
            int saved = c.save();
            c.clipPath(inner);
            paint.setColor(fill);
            c.drawRect(r, paint);
            paint.setColor(light);
            c.drawRect(r.left + b, r.top + b, r.right - b, r.top + 2 * b, paint);
            c.drawRect(r.left + b, r.top + b, r.left + 2 * b, r.bottom - b, paint);
            paint.setColor(dark);
            c.drawRect(r.left + b, r.bottom - 2 * b, r.right - b, r.bottom - b, paint);
            c.drawRect(r.right - 2 * b, r.top + b, r.right - b, r.bottom - b, paint);
            c.restoreToCount(saved);
        }

        @Override public void setAlpha(int alpha) { }
        @Override public void setColorFilter(ColorFilter f) { }
        @Override public int getOpacity() { return PixelFormat.TRANSLUCENT; }
    }

    private static int[] erode(int[] s) {
        int n = s.length;
        int[] ind = new int[n + 2];
        System.arraycopy(s, 0, ind, 0, n);
        int[] out = new int[n];
        int len = 0;
        for (int y = 1; y <= n; y++) {
            int v = Math.max(ind[y] + 1, Math.max(ind[y - 1], ind[y + 1])) - 1;
            out[y - 1] = v;
            if (v > 0) len = y;
        }
        return Arrays.copyOf(out, len);
    }

    static int mix(int a, int b, float t) {
        int r = Math.round(((a >> 16) & 0xFF) + (((b >> 16) & 0xFF) - ((a >> 16) & 0xFF)) * t);
        int g = Math.round(((a >> 8) & 0xFF) + (((b >> 8) & 0xFF) - ((a >> 8) & 0xFF)) * t);
        int bl = Math.round((a & 0xFF) + ((b & 0xFF) - (a & 0xFF)) * t);
        return 0xFF000000 | (r << 16) | (g << 8) | bl;
    }

    static Drawable pixel(Activity a, int fill, boolean raised, boolean large, int outline) {
        return new PixelPanel(Math.max(1, px(a, 2)), fill, outline, raised, large);
    }

    static Drawable pixel(Activity a, int fill, boolean raised, boolean large) {
        return pixel(a, fill, raised, large, OUTLINE);
    }

    static Drawable well(Activity a, boolean large) {
        return pixel(a, WELL, false, large);
    }

    static Drawable gold(Activity a, boolean large) {
        return pixel(a, GOLD, true, large, GOLD_OUTLINE);
    }

    static Drawable selected(Activity a, boolean large) {
        return pixel(a, SELECTED, false, large, SELECTED_OUTLINE);
    }

    static Drawable panelBig(Activity a, int fill, int stroke) {
        return pixel(a, fill, fill != PANEL_DARK, true, stroke);
    }

    static Drawable panel(Activity a, int fill, int stroke) {
        return pixel(a, fill, fill != PANEL_DARK, false, stroke);
    }

    static View seam(Activity a, boolean vertical) {
        View v = new View(a);
        v.setBackgroundColor(OUTLINE);
        int t = Math.max(1, px(a, 2));
        v.setLayoutParams(vertical
            ? new LinearLayout.LayoutParams(t, LinearLayout.LayoutParams.MATCH_PARENT)
            : new LinearLayout.LayoutParams(LinearLayout.LayoutParams.MATCH_PARENT, t));
        return v;
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

        @Override public void setText(CharSequence text, BufferType type) {
            // Andy tem os acentos, mas nao as setas, o cirilico, o tailandes
            // nem o chines. O Layout preserva a fonte de apoio destes glifos
            // inclusive quando o valor da barra muda.
            SpannableStringBuilder styled = null;
            if (text != null) {
                for (int i = 0; i < text.length(); i++) {
                    char ch = text.charAt(i);
                    if ((ch >= '\u2190' && ch <= '\u21FF') || ch == '\u2212' || (ch >= '\u0400' && ch <= '\u052F')
                            || (ch >= '\u0E00' && ch <= '\u0E7F')
                            || (ch >= '\u3000' && ch <= '\u9FFF') || (ch >= '\uFF00' && ch <= '\uFFEF')) {
                        if (styled == null) styled = new SpannableStringBuilder(text);
                        styled.setSpan(new TypefaceSpan("sans-serif"), i, i + 1,
                                       Spanned.SPAN_EXCLUSIVE_EXCLUSIVE);
                    }
                }
            }
            super.setText(styled != null ? styled : text, type);
        }

        @Override protected void onDraw(Canvas c) {
            final Layout layout = getLayout();
            if (layout == null || getLineCount() != 1) { super.onDraw(c); return; }
            final TextPaint pt = getPaint();
            final int color = getCurrentTextColor();
            int saved = c.save();
            c.translate(getCompoundPaddingLeft(), getBaseline() - layout.getLineBaseline(0));
            try {
                pt.setStyle(Paint.Style.STROKE);
                pt.setStrokeWidth(strokeWidth);
                pt.setStrokeJoin(Paint.Join.ROUND);
                pt.setColor(0xFF000000);
                layout.draw(c);
                pt.setStyle(Paint.Style.FILL);
                pt.setColor(color);
                layout.draw(c);
            } finally {
                pt.setStyle(Paint.Style.FILL);
                pt.setColor(color);
                c.restoreToCount(saved);
            }
        }
    }

    static TextView text(Activity a, String s, float size, int color) {
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
     *
     * Em vietnamita o menu inteiro usa a fonte do sistema: Andy nao tem
     * as vogais com breve e chifre, o d cortado nem os tons com gancho e
     * ponto, e a troca por letra partia as palavras ao meio.
     */
    private static Typeface sFont;

    static Typeface font(Activity a) {
        if ("vi".equals(MenuLocalization.language())) return Typeface.DEFAULT;
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

    static Bitmap sprite(Activity a, String name) {
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

    private static String spriteKey(int kind, int id) {
        return SPR_DIR[kind] + id;
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
        final int kind = npc ? SPR_NPC : key.startsWith("buff/") ? SPR_BUFF : SPR_ITEM;
        Bitmap bmp = null;
        InputStream in = null;
        try {
            // Item, NPC e buff de mod nao tem sprite no APK: vem do PNG do proprio mod.
            String file = modTexture(kind, Integer.parseInt(key.substring(SPR_DIR[kind].length())));
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
    private static Bitmap spriteJa(Activity a, int kind, int id) {
        String k = spriteKey(kind, id);
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
    private static void requestSprite(Activity a, ImageView v, int kind, int id) {
        final String k = spriteKey(kind, id);
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

    static ImageView icon(Activity a, Bitmap bmp, float u) {
        ImageView v = new ImageView(a);
        v.setScaleType(ImageView.ScaleType.FIT_CENTER);
        setBitmap(a, v, bmp);
        v.setLayoutParams(new LinearLayout.LayoutParams(px(a, u), px(a, u)));
        return v;
    }

    /** Pixel art sem borrar: vizinho-mais-proximo. */
    static void setBitmap(Activity a, ImageView v, Bitmap bmp) {
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
                                       : spriteJa(a, s.iconKind, s.iconItem), u);
    }

    /**
     * O girassol do jogo abrindo e fechando, enquanto o catalogo monta.
     *
     * spr_loading.png e uma tira vertical de 19 quadros de 52x54: o botao
     * abre, as petalas crescem, murcham e sobra a semente — e volta. Desenhado
     * a mao, quadro a quadro, porque AnimationDrawable pede um drawable por
     * quadro e este menu roda sem a classe R do app.
     */
    static final class Sunflower extends View {
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

    // Config > Desenvolvedor do launcher (ui/Prefs.kt), lidas quando o jogo
    // abre: o Mod Menu (o coelho), o Editor e o Reiniciar.
    static boolean sMenuOn = true, sEditorOn = true, sRestartOn = true;

    private static void readDevSettings(Activity act) {
        try {
            SharedPreferences p = act.getSharedPreferences("settings", Activity.MODE_PRIVATE);
            sMenuOn = p.getBoolean("devModMenu", true);
            sEditorOn = p.getBoolean("devEditor", true);
            sRestartOn = p.getBoolean("devRestart", true);
        } catch (Throwable t) { /* fica tudo ligado */ }
    }

    public static void install(final Activity act) {
        sActivity = act;
        MenuLocalization.initialize(act);
        readDevSettings(act);
        // Sem o Mod Menu nao ha como desligar um poder: nenhum volta ligado.
        if (sMenuOn) {
            try { restorePowers(act); } catch (Throwable t) { /* sem os salvos, tudo desligado */ }
        }
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
        if (!sMenuOn) return;
        ImageView b = icon(act, sprite(act, "ic_bunny_menu"), BUTTON_SIZE);
        b.setPadding(px(act, 4), px(act, 4), px(act, 4), px(act, 4));
        b.setBackground(panel(act, PANEL, OUTLINE));
        b.setContentDescription(tr("ModMenu"));
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
                    if (dragging) savePosition(act, lp);
                    else toggleMenu(act);
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
                try { if (inWorld && nCheatsBlocked()) inWorld = false; } catch (Throwable t) { }
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

    // ---- o menu por cima do teclado ----
    //
    // Deitado, o teclado do celular ocupa mais da metade da altura: com o menu
    // do tamanho da tela, a busca ficava visivel e a lista de resultados toda
    // atras do teclado. Enquanto a busca tem o foco, o menu inteiro encolhe
    // para o espaco acima dele, e a janela do jogo fica parada (ADJUST_NOTHING),
    // sem ser empurrada. A altura vem do mesmo medidor do Editor.

    private static LinearLayout sMenuBody;
    private static DevTools.KeyboardWatcher sMenuKeyboard;
    private static int sMenuSoftInput = -1;

    static void menuKeyboard(final Activity act, boolean on) {
        if (on) {
            if (sMenuKeyboard != null) return;
            sMenuSoftInput = act.getWindow().getAttributes().softInputMode;
            act.getWindow().setSoftInputMode(android.view.WindowManager.LayoutParams.SOFT_INPUT_ADJUST_NOTHING
                | (sMenuSoftInput & android.view.WindowManager.LayoutParams.SOFT_INPUT_MASK_STATE));
            sMenuKeyboard = new DevTools.KeyboardWatcher(act, new DevTools.KeyboardListener() {
                @Override public void onKeyboard(int height) { fitMenuAbove(act, height); }
            });
        } else {
            if (sMenuKeyboard == null) return;
            sMenuKeyboard.close();
            sMenuKeyboard = null;
            fitMenuAbove(act, 0);
            if (sMenuSoftInput >= 0) act.getWindow().setSoftInputMode(sMenuSoftInput);
        }
    }

    private static void fitMenuAbove(Activity act, int keyboard) {
        if (sMenuBody == null) return;
        FrameLayout.LayoutParams lp = (FrameLayout.LayoutParams) sMenuBody.getLayoutParams();
        lp.topMargin = px(act, keyboard > 0 ? 6 : 14);
        lp.bottomMargin = keyboard > 0 ? keyboard + px(act, 6) : px(act, 14);
        sMenuBody.setLayoutParams(lp);
    }

    private static void toggleMenu(Activity act) {
        // Idioma trocado com o menu fechado: as listas e as secoes guardam o
        // catalogo velho, entao o menu inteiro e montado de novo.
        if (sOverlay != null && sOverlay.getVisibility() != View.VISIBLE
                && !sCatalogLoading && isStale()) {
            closeSlider();
            ViewGroup parent = (ViewGroup) sOverlay.getParent();
            if (parent != null) parent.removeView(sOverlay);
            sOverlay = null;
            sCatalog = null;
        }
        if (sOverlay != null) {
            closeSlider();
            sOverlay.setVisibility(
                sOverlay.getVisibility() == View.VISIBLE ? View.GONE : View.VISIBLE);
            if (sOverlay.getVisibility() == View.VISIBLE && sRailPaint != null) sRailPaint.run();
            return;
        }
        sOverlay = buildMenu(act);
        FrameLayout.LayoutParams lp = new FrameLayout.LayoutParams(
            FrameLayout.LayoutParams.MATCH_PARENT, FrameLayout.LayoutParams.MATCH_PARENT);
        act.addContentView(sOverlay, lp);
    }

    private static final float SIDE = 58;

    private static View buildMenu(final Activity act) {
        loadModItems();
        final Section[] all = sections();
        withCatalog(act, null);

        FrameLayout root = new FrameLayout(act);
        root.setBackgroundColor(SCRIM);
        noAutofill(root);
        root.setOnClickListener(new View.OnClickListener() {
            @Override public void onClick(View v) { }
        });

        LinearLayout body = new LinearLayout(act);
        body.setOrientation(LinearLayout.HORIZONTAL);
        body.setBackground(panelBig(act, PANEL, OUTLINE));
        int edge = px(act, 4);
        body.setPadding(edge, edge, edge, edge);
        FrameLayout.LayoutParams blp = new FrameLayout.LayoutParams(
            FrameLayout.LayoutParams.MATCH_PARENT, FrameLayout.LayoutParams.MATCH_PARENT);
        blp.setMargins(px(act, 14), px(act, 14), px(act, 14), px(act, 14));
        body.setLayoutParams(blp);
        root.addView(body);
        sMenuBody = body;

        final LinearLayout content = new LinearLayout(act);
        content.setOrientation(LinearLayout.VERTICAL);
        content.setPadding(px(act, 8), px(act, 6), px(act, 8), px(act, 6));

        LinearLayout aside = new LinearLayout(act);
        aside.setOrientation(LinearLayout.VERTICAL);
        aside.setGravity(Gravity.CENTER_HORIZONTAL);
        aside.setPadding(0, px(act, 6), 0, px(act, 4));
        ImageView logo = icon(act, sprite(act, "ic_bunny_head"), 28);
        LinearLayout.LayoutParams llp = new LinearLayout.LayoutParams(px(act, 28), px(act, 28));
        llp.bottomMargin = px(act, 4);
        aside.addView(logo, llp);

        final ScrollView asideScroll = new ScrollView(act);
        asideScroll.setVerticalScrollBarEnabled(false);
        final LinearLayout asideList = new LinearLayout(act);
        asideList.setOrientation(LinearLayout.VERTICAL);
        asideList.setGravity(Gravity.CENTER_HORIZONTAL);
        asideScroll.addView(asideList);
        aside.addView(asideScroll, new LinearLayout.LayoutParams(
            LinearLayout.LayoutParams.WRAP_CONTENT, 0, 1f));

        final View[] buttons = new View[all.length];
        for (int i = 0; i < all.length; i++) {
            final int index = i;
            FrameLayout b = new FrameLayout(act);
            b.setContentDescription(all[i].title);
            ImageView ic = sectionIcon(act, all[i], 26);
            b.addView(ic, new FrameLayout.LayoutParams(px(act, 26), px(act, 26), Gravity.CENTER));
            b.setOnClickListener(new View.OnClickListener() {
                @Override public void onClick(View v) {
                    select(act, content, all, buttons, index);
                }
            });
            LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(px(act, 44), px(act, 44));
            lp.topMargin = px(act, 4);
            asideList.addView(b, lp);
            buttons[i] = b;
        }
        aside.addView(DevTools.menuButtons(act));

        body.addView(aside, new LinearLayout.LayoutParams(px(act, SIDE), LinearLayout.LayoutParams.MATCH_PARENT));
        body.addView(seam(act, true));
        body.addView(content, new LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.MATCH_PARENT, 1f));
        body.addView(seam(act, true));
        body.addView(rail(act), new LinearLayout.LayoutParams(px(act, RAIL), LinearLayout.LayoutParams.MATCH_PARENT));

        select(act, content, all, buttons, 0);
        return root;
    }

    // ------------------------------ coluna do X ------------------------------
    //
    // O X no alto e, embaixo dele, o que muda o mundo inteiro a um toque, em
    // qualquer secao: a hora, a chuva, o vento e o tempo parado. A hora eram
    // quatro botoes embaixo do titulo; chuva, vento e tempo, cartoes na grade.

    /** Largura da coluna, em unidades. */
    private static final float RAIL = 48;

    /** Os icones de hora da Jornada (UI/Creative/Infinite_Powers), na ordem do toque. */
    private static final String[] TIME_ICON = {
        "ic_hora_amanhecer", "ic_hora_meiodia", "ic_hora_anoitecer", "ic_hora_meianoite",
    };
    private static final String[] TIME_NAME = {"Dawn", "Noon", "Dusk", "Midnight"};
    /** A ultima hora pedida; -1 = nenhuma ainda (o botao mostra o amanhecer). */
    private static int sTimeIndex = -1;
    /** Repinta os botoes da coluna: o "Desligar tudo" da grade tambem mexe neles. */
    private static Runnable sRailPaint;
    /** O "N ligados" da grade, se ela estiver na tela. */
    private static TextView sActiveLabel;

    /**
     * Fundo de todo botao (a coluna do X, os cartoes de poder, as pastas, o
     * "Desligar tudo"): um tom acima do painel, com borda clara; ligado, verde.
     */
    static Drawable actionBackground(Activity a, boolean on) {
        return pixel(a, on ? GRASS : BUTTON, true, false);
    }

    private static ImageView railButton(Activity act, LinearLayout col, Bitmap bmp, String name) {
        ImageView b = icon(act, bmp, 44);
        b.setContentDescription(name);
        b.setPadding(px(act, 7), px(act, 7), px(act, 7), px(act, 7));
        b.setBackground(actionBackground(act, false));
        LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(px(act, 44), px(act, 44));
        lp.topMargin = px(act, 6);
        col.addView(b, lp);
        return b;
    }

    /** O icone de um poder: o de interface do jogo, se tiver, ou o do item. */
    private static Bitmap powerIcon(Activity act, int id) {
        String res = powerRes(id);
        return res != null ? sprite(act, res) : spriteJa(act, SPR_ITEM, POWER_ICON[id]);
    }

    private static View rail(final Activity act) {
        LinearLayout col = new LinearLayout(act);
        col.setOrientation(LinearLayout.VERTICAL);
        col.setGravity(Gravity.CENTER_HORIZONTAL);
        col.setPadding(0, px(act, 4), 0, 0);

        ImageView close = icon(act, sprite(act, "ic_fechar"), 40);
        close.setContentDescription(tr("Close"));
        close.setPadding(px(act, 4), px(act, 4), px(act, 4), px(act, 4));
        close.setOnClickListener(new View.OnClickListener() {
            @Override public void onClick(View v) {
                closeSlider();
                if (sOverlay != null) sOverlay.setVisibility(View.GONE);
            }
        });
        LinearLayout.LayoutParams clp = new LinearLayout.LayoutParams(px(act, 40), px(act, 40));
        clp.bottomMargin = px(act, 4);
        col.addView(close, clp);

        // A hora: a barra do dia inteiro. O icone mostra o pedaco do dia que
        // ficou; fora do mundo, avisa.
        final ImageView time = railButton(act, col,
            sprite(act, TIME_ICON[sTimeIndex < 0 ? 0 : sTimeIndex]), tr("TimeOfDay"));
        time.setOnClickListener(new View.OnClickListener() {
            @Override public void onClick(View v) {
                if (!nInWorld()) { toast(act, tr("EnterWorldFirst")); return; }
                showSlider(act, time, timeSlider(act, time));
            }
        });

        // Chuva, vento e inimigos abrem a barra deles; o tempo parado so liga
        // e desliga, como o cartao.
        final int[] ids = {P_RAIN, P_WIND, P_NO_SPAWNS, P_TIME_STOP};
        final ImageView[] views = new ImageView[ids.length];
        for (int k = 0; k < ids.length; k++) {
            final int id = ids[k];
            final ImageView b = railButton(act, col, powerIcon(act, id), tr(POWER_NAME[id]));
            views[k] = b;
            b.setOnClickListener(new View.OnClickListener() {
                @Override public void onClick(View v) {
                    if (isSlider(id)) {
                        if (!nInWorld()) { toast(act, tr("EnterWorldFirst")); return; }
                        showSlider(act, b, powerSlider(act, id));
                        return;
                    }
                    setRailPower(id, sPowerLevels[id] > 0 ? 0 : 1);
                    savePowers(act);
                }
            });
        }
        sRailPaint = new Runnable() {
            @Override public void run() {
                int[] world = nWorldState();
                if (world != null && world.length > 2 && world[2] >= 0) {
                    int index = timeIndexOf(world[2]);
                    if (index != sTimeIndex) {
                        sTimeIndex = index;
                        setBitmap(act, time, sprite(act, TIME_ICON[index]));
                    }
                }
                for (int k = 0; k < views.length; k++) {
                    views[k].setBackground(actionBackground(act, sPowerLevels[ids[k]] > 0));
                }
            }
        };
        sRailPaint.run();
        return col;
    }

    /** Muda um poder da coluna e repinta quem mostra o estado dele. */
    private static void setRailPower(int id, int level) {
        sPowerLevels[id] = level;
        nSetPower(id, level);
        if (sRailPaint != null) sRailPaint.run();
        if (sActiveLabel != null) updateActiveCount(sActiveLabel);
    }

    // ------------------------------ barras da coluna ------------------------------
    //
    // Tocar na hora, na chuva, no vento ou nos inimigos abre, ao lado do botao,
    // um painel com uma barra de degrade: arrastar muda o mundo na hora. O
    // painel fecha com um toque fora dele.

    /** Minuto do relogio (0..1439) -> pedido de hora do nativo (Powers.h). */
    private static final int CLOCK_REQUEST = 100;
    /** Durante o arrasto, no maximo um pedido a cada isto: no multijogador, cada um vai ao servidor. */
    private static final long SLIDER_SEND_MS = 120;
    private static long sSliderSent;
    /** O painel aberto agora; null = nenhum. */
    private static View sSliderLayer;

    /** Arrastando: so passa um pedido de vez em quando. Soltou: sempre. */
    private static boolean sendNow(boolean done) {
        long now = SystemClock.uptimeMillis();
        if (!done && now - sSliderSent < SLIDER_SEND_MS) return false;
        sSliderSent = now;
        return true;
    }

    /** O que uma barra mostra e faz. */
    private abstract static class SliderSpec {
        String title;
        Bitmap icon;
        int max = 100;
        int initial;
        /** O degrade: a cor em cada ponto (0..1 da barra). */
        int[] colors;
        float[] stops;
        /** Marcas finas na barra (valores), como o meio-dia ou o x1. */
        int[] ticks = {};
        String[] captions = {};

        abstract String label(int v);
        /** `done`: o dedo saiu (ou foi um botao), hora de mandar o ultimo e salvar. */
        abstract void apply(int v, boolean done);
        /** Desenho na alca (o sol, a lua); null = alca lisa. */
        Bitmap handleIcon(int v) { return null; }
        /** Os botoes embaixo da barra. */
        void addButtons(Activity act, LinearLayout row, GradientRange bar) { }
    }

    /**
     * A barra das barras da coluna: o sulco com o degrade do que ela muda, as
     * marcas e a alca branca do Range, que pode levar um desenho (o sol, a lua).
     */
    private static final class GradientRange extends View {
        interface Listener { void changed(int value, boolean done); }

        private final SliderSpec spec;
        private final Paint paint = new Paint(Paint.ANTI_ALIAS_FLAG);
        private final Rect src = new Rect();
        private final Rect dst = new Rect();
        private final float border, groove, handleW, handleH;
        private LinearGradient shader;
        private float shaderX0 = -1, shaderX1 = -1;
        private int value;
        Listener listener;

        GradientRange(Activity a, SliderSpec spec) {
            super(a);
            this.spec = spec;
            this.value = Math.max(0, Math.min(spec.max, spec.initial));
            this.border = px(a, 2);
            this.groove = px(a, 14);
            boolean icon = spec.handleIcon(value) != null;
            this.handleW = px(a, icon ? 26 : 14);
            this.handleH = px(a, icon ? 30 : 26);
            paint.setFilterBitmap(false);   // pixel art: sem borrar
        }

        int value() { return value; }

        /** Muda pela mao de um botao: avisa como um toque solto. */
        void set(int v) {
            value = Math.max(0, Math.min(spec.max, v));
            invalidate();
            if (listener != null) listener.changed(value, true);
        }

        private float left() { return handleW / 2f; }
        private float right() { return getWidth() - handleW / 2f; }
        private float xOf(int v) { return left() + (right() - left()) * v / (float) spec.max; }

        @Override protected void onMeasure(int wSpec, int hSpec) {
            Activity a = (Activity) getContext();
            setMeasuredDimension(resolveSize(px(a, 330), wSpec),
                                 resolveSize((int) (handleH + px(a, 4)), hSpec));
        }

        @Override protected void onDraw(Canvas c) {
            final float mid = getHeight() / 2f;
            final float x0 = left(), x1 = right();
            final float top = mid - groove / 2f, base = mid + groove / 2f;

            paint.setShader(null);
            paint.setStyle(Paint.Style.FILL);
            paint.setColor(OUTLINE);
            c.drawRect(x0 - border, top - border, x1 + border, base + border, paint);
            if (shader == null || shaderX0 != x0 || shaderX1 != x1) {
                shader = new LinearGradient(x0, 0, x1, 0, spec.colors, spec.stops, Shader.TileMode.CLAMP);
                shaderX0 = x0;
                shaderX1 = x1;
            }
            paint.setShader(shader);
            c.drawRect(x0, top, x1, base, paint);
            paint.setShader(null);
            paint.setColor(0x30FFFFFF);
            c.drawRect(x0, top, x1, mid, paint);
            paint.setColor(0x30131625);
            c.drawRect(x0, base - border, x1, base, paint);

            paint.setColor(0x80131625);
            for (int t : spec.ticks) {
                float x = xOf(t);
                c.drawRect(x - border / 2f, top, x + border / 2f, base, paint);
            }

            // A alca do Range: branca, mais alta que o sulco, meia sombra embaixo.
            final float cx = xOf(value);
            final float ax = cx - handleW / 2f, ay = mid - handleH / 2f;
            paint.setColor(OUTLINE);
            c.drawRect(ax, ay, ax + handleW, ay + handleH, paint);
            paint.setColor(0xFFFFFFFF);
            c.drawRect(ax + border, ay + border, ax + handleW - border, ay + handleH - border, paint);
            paint.setColor(0xFFB9C0D4);
            c.drawRect(ax + border, mid + handleH / 6f, ax + handleW - border, ay + handleH - border, paint);

            Bitmap bmp = spec.handleIcon(value);
            if (bmp != null) {
                float side = handleW - border * 4;
                src.set(0, 0, bmp.getWidth(), bmp.getHeight());
                dst.set((int) (cx - side / 2f), (int) (mid - side / 2f),
                        (int) (cx + side / 2f), (int) (mid + side / 2f));
                c.drawBitmap(bmp, src, dst, paint);
            }
        }

        @Override public boolean onTouchEvent(MotionEvent e) {
            switch (e.getActionMasked()) {
                case MotionEvent.ACTION_DOWN:
                case MotionEvent.ACTION_MOVE:
                case MotionEvent.ACTION_UP:
                case MotionEvent.ACTION_CANCEL: {
                    float t = (e.getX() - left()) / Math.max(1f, right() - left());
                    if (t < 0) t = 0; else if (t > 1) t = 1;
                    int next = e.getActionMasked() == MotionEvent.ACTION_CANCEL
                        ? value : Math.round(t * spec.max);
                    boolean done = e.getActionMasked() == MotionEvent.ACTION_UP ||
                                   e.getActionMasked() == MotionEvent.ACTION_CANCEL;
                    if (next != value || done || e.getActionMasked() == MotionEvent.ACTION_DOWN) {
                        value = next;
                        invalidate();
                        if (listener != null) listener.changed(value, done);
                    }
                    getParent().requestDisallowInterceptTouchEvent(!done);
                    if (e.getActionMasked() == MotionEvent.ACTION_UP) performClick();
                    return true;
                }
                default:
                    return super.onTouchEvent(e);
            }
        }
    }

    private static void closeSlider() {
        if (sSliderLayer == null) return;
        if (sSliderLayer.getParent() instanceof ViewGroup) {
            ((ViewGroup) sSliderLayer.getParent()).removeView(sSliderLayer);
        }
        sSliderLayer = null;
    }

    /** O painel da barra, a esquerda do botao que o abriu, na altura dele. */
    private static void showSlider(final Activity act, View anchor, final SliderSpec spec) {
        if (!(sOverlay instanceof FrameLayout)) return;
        final FrameLayout root = (FrameLayout) sOverlay;
        closeSlider();
        sSliderSent = 0;

        // A camada pega o toque fora do painel e fecha.
        FrameLayout layer = new FrameLayout(act);
        layer.setOnClickListener(new View.OnClickListener() {
            @Override public void onClick(View v) { closeSlider(); }
        });

        LinearLayout box = new LinearLayout(act);
        box.setOrientation(LinearLayout.VERTICAL);
        box.setBackground(panelBig(act, PANEL, OUTLINE));
        box.setPadding(px(act, 12), px(act, 10), px(act, 12), px(act, 10));
        box.setClickable(true);   // o toque dentro nao fecha

        LinearLayout head = new LinearLayout(act);
        head.setOrientation(LinearLayout.HORIZONTAL);
        head.setGravity(Gravity.CENTER_VERTICAL);
        head.addView(icon(act, spec.icon, 26));
        TextView title = text(act, spec.title, 15, INK);
        title.setPadding(px(act, 8), 0, 0, 0);
        head.addView(title, new LinearLayout.LayoutParams(0,
            LinearLayout.LayoutParams.WRAP_CONTENT, 1f));
        box.addView(head, new LinearLayout.LayoutParams(
            LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.WRAP_CONTENT));
        final TextView value = text(act, "", 13, INK);
        value.setPadding(0, px(act, 6), 0, 0);
        box.addView(value);

        final GradientRange bar = new GradientRange(act, spec);
        value.setText(spec.label(bar.value()));
        bar.setContentDescription(spec.title + ": " + spec.label(bar.value()));
        bar.listener = new GradientRange.Listener() {
            @Override public void changed(int v, boolean done) {
                value.setText(spec.label(v));
                bar.setContentDescription(spec.title + ": " + spec.label(v));
                spec.apply(v, done);
            }
        };
        LinearLayout.LayoutParams blp = new LinearLayout.LayoutParams(
            LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.WRAP_CONTENT);
        blp.topMargin = px(act, 8);
        box.addView(bar, blp);

        if (spec.captions.length > 0) {
            LinearLayout captions = new LinearLayout(act);
            captions.setOrientation(LinearLayout.HORIZONTAL);
            for (int i = 0; i < spec.captions.length; i++) {
                TextView caption = text(act, spec.captions[i], 10, INK_DIM);
                caption.setGravity(i == 0 ? Gravity.LEFT : i == spec.captions.length - 1
                    ? Gravity.RIGHT : Gravity.CENTER);
                captions.addView(caption, new LinearLayout.LayoutParams(0,
                    LinearLayout.LayoutParams.WRAP_CONTENT, 1f));
            }
            box.addView(captions);
        }

        LinearLayout row = new LinearLayout(act);
        row.setOrientation(LinearLayout.HORIZONTAL);
        row.setGravity(Gravity.CENTER_VERTICAL);
        spec.addButtons(act, row, bar);
        if (row.getChildCount() > 0) {
            LinearLayout.LayoutParams rlp = new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.WRAP_CONTENT, LinearLayout.LayoutParams.WRAP_CONTENT);
            rlp.topMargin = px(act, 8);
            box.addView(row, rlp);
        }

        int margin = px(act, 8);
        int width = Math.min(px(act, 380), Math.max(1, root.getWidth() - 2 * margin));
        FrameLayout.LayoutParams lp = new FrameLayout.LayoutParams(width,
            FrameLayout.LayoutParams.WRAP_CONTENT, Gravity.TOP | Gravity.LEFT);
        box.measure(View.MeasureSpec.makeMeasureSpec(width, View.MeasureSpec.EXACTLY),
                    View.MeasureSpec.makeMeasureSpec(Math.max(1, root.getHeight() - 2 * margin),
                                                    View.MeasureSpec.AT_MOST));
        int[] at = new int[2], base = new int[2];
        anchor.getLocationInWindow(at);
        root.getLocationInWindow(base);
        int left = at[0] - base[0] - box.getMeasuredWidth() - margin;
        int top = at[1] - base[1] + anchor.getHeight() / 2 - box.getMeasuredHeight() / 2;
        lp.leftMargin = Math.max(margin, Math.min(left, root.getWidth() - width - margin));
        lp.topMargin = Math.max(margin, Math.min(top, root.getHeight() - box.getMeasuredHeight() - margin));
        layer.addView(box, lp);

        root.addView(layer, new FrameLayout.LayoutParams(
            FrameLayout.LayoutParams.MATCH_PARENT, FrameLayout.LayoutParams.MATCH_PARENT));
        sSliderLayer = layer;
    }

    /** Botao de texto do painel; verde = o que vale agora. */
    private static TextView sliderButton(Activity act, String label, boolean on) {
        TextView b = text(act, label, 12, INK);
        b.setGravity(Gravity.CENTER);
        b.setPadding(px(act, 10), px(act, 5), px(act, 10), px(act, 5));
        b.setBackground(actionBackground(act, on));
        return b;
    }

    private static void addToRow(Activity act, LinearLayout row, View b) {
        LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(
            LinearLayout.LayoutParams.WRAP_CONTENT, LinearLayout.LayoutParams.WRAP_CONTENT);
        lp.rightMargin = px(act, 6);
        row.addView(b, lp);
    }

    // ---- a hora ----

    /** Os botoes da Jornada viram atalhos da barra: amanhecer, meio-dia, anoitecer, meia-noite. */
    private static final int[] TIME_PRESET = {270, 720, 1170, 0};

    /** O pedaco do dia de um minuto, o do icone do botao: os quatro da Jornada. */
    private static int timeIndexOf(int minute) {
        if (minute >= 270 && minute < 600) return 0;
        if (minute >= 600 && minute < 1020) return 1;
        if (minute >= 1020 && minute < 1290) return 2;
        return 3;
    }

    private static String periodOf(int minute) {
        if (minute < 270) return tr("EarlyMorning");
        if (minute < 360) return tr("Dawn");
        if (minute < 690) return tr("Morning");
        if (minute < 750) return tr("Noon");
        if (minute < 1110) return tr("Afternoon");
        if (minute < 1230) return tr("Dusk");
        return tr("Night");
    }

    private static SliderSpec timeSlider(final Activity act, final ImageView button) {
        final Bitmap sun = sprite(act, "ic_hora_meiodia");
        final Bitmap moon = sprite(act, "ic_hora_meianoite");
        SliderSpec spec = new SliderSpec() {
            @Override String label(int v) {
                return String.format(Locale.ROOT, "%02d:%02d", v / 60, v % 60) + "  " + periodOf(v);
            }
            @Override void apply(int v, boolean done) {
                if (sendNow(done)) nSetTimeOfDay(CLOCK_REQUEST + v);
                int index = timeIndexOf(v);
                if (index != sTimeIndex) {
                    sTimeIndex = index;
                    setBitmap(act, button, sprite(act, TIME_ICON[index]));
                }
            }
            @Override Bitmap handleIcon(int v) {
                return v >= 270 && v < 1170 ? sun : moon;
            }
            @Override void addButtons(Activity a, LinearLayout row, final GradientRange bar) {
                for (int i = 0; i < TIME_PRESET.length; i++) {
                    final int minute = TIME_PRESET[i];
                    ImageView b = icon(a, sprite(a, TIME_ICON[i]), 30);
                    b.setContentDescription(tr(TIME_NAME[i]));
                    b.setPadding(px(a, 4), px(a, 4), px(a, 4), px(a, 4));
                    b.setBackground(actionBackground(a, false));
                    b.setOnClickListener(new View.OnClickListener() {
                        @Override public void onClick(View v) { bar.set(minute); }
                    });
                    addToRow(a, row, b);
                }
            }
        };
        spec.title = tr("TimeOfDay");
        spec.icon = sun;
        spec.max = 1439;
        int[] w = nWorldState();
        spec.initial = w != null && w.length > 2 && w[2] >= 0 ? w[2] : 720;
        // Madrugada, nascer do sol, ceu do dia, por do sol e a noite de volta.
        spec.colors = new int[] {
            0xFF0E1236, 0xFF1E2A5E, 0xFFE9875A, 0xFFF6C177, 0xFF7EC8F2, 0xFF9ADCFF,
            0xFF7EB8E6, 0xFFE06C4C, 0xFF5B3B85, 0xFF1E2350, 0xFF0E1236,
        };
        int[] at = {0, 240, 270, 360, 480, 720, 1020, 1170, 1230, 1320, 1439};
        spec.stops = new float[at.length];
        for (int i = 0; i < at.length; i++) spec.stops[i] = at[i] / 1439f;
        spec.ticks = new int[] {270, 720, 1170};
        spec.captions = new String[] {"00:00", "12:00", "23:59"};
        return spec;
    }

    // ---- chuva, vento, inimigos ----

    /** A taxa da Jornada (Powers.cpp, spawnMultiplierOf): o meio e x1, a ponta esquerda nenhum. */
    private static float spawnMultiplier(int v) {
        float t = v / 100f;
        if (t <= 0f) return 0f;
        return t < 0.5f ? 0.1f + (t / 0.5f) * 0.9f : 1f + ((t - 0.5f) / 0.5f) * 9f;
    }

    /** 0,5 / 2,3 / 10, com virgula e sem ",0". */
    private static String decimal(float x) {
        String s = String.format(Locale.ROOT, "%.1f", x);
        if (s.endsWith(".0")) s = s.substring(0, s.length() - 2);
        return s.replace('.', ',');
    }

    private static String sliderLabel(int id, int v) {
        if (id == P_RAIN) {
            if (v == 0) return tr("NoRain");
            String name = v < 25 ? tr("Drizzle") : v < 50 ? tr("Rain") : v < 75 ? tr("HeavyRain") : tr("Storm");
            return name + "  " + v + "%";
        }
        if (id == P_WIND) {
            int d = v - 50;
            int mph = Math.round(Math.abs(d) * 0.8f);
            if (Math.abs(d) <= 2) return tr("Calm");
            String name = Math.abs(d) < 25 ? tr("Breeze") : tr("Gale");
            return (d < 0 ? "←  " : "→  ") + name + "  " + mph + " mph";
        }
        if (v == 0) return tr("NoEnemies");
        return "x" + decimal(spawnMultiplier(v)) + (v == 50 ? "  " + tr("SpawnRateNormal") : "");
    }

    private static SliderSpec powerSlider(final Activity act, final int id) {
        final TextView[] auto = new TextView[1];
        SliderSpec spec = new SliderSpec() {
            @Override String label(int v) { return sliderLabel(id, v); }
            @Override void apply(int v, boolean done) {
                if (sendNow(done) || sPowerLevels[id] == 0) setRailPower(id, v + 1);
                if (auto[0] != null) auto[0].setBackground(actionBackground(act, false));
                if (done) savePowers(act);
            }
            @Override void addButtons(final Activity a, LinearLayout row, final GradientRange bar) {
                // Verde enquanto o jogo decide; mexer na barra apaga.
                final TextView b = sliderButton(a, id == P_NO_SPAWNS ? tr("SpeedNormal") : tr("Automatic"),
                                                sPowerLevels[id] == 0);
                auto[0] = b;
                b.setOnClickListener(new View.OnClickListener() {
                    @Override public void onClick(View v) {
                        setRailPower(id, 0);
                        savePowers(a);
                        b.setBackground(actionBackground(a, true));
                    }
                });
                addToRow(a, row, b);
                if (id != P_NO_SPAWNS) return;

                // Acao: some com os que ja existem. Pisca de verde, como o cartao.
                final TextView clear = sliderButton(a, tr(POWER_NAME[P_CLEAR_ENEMIES]), false);
                clear.setOnClickListener(new View.OnClickListener() {
                    @Override public void onClick(View v) {
                        if (!nInWorld()) { toast(a, tr("EnterWorldFirst")); return; }
                        nSetPower(P_CLEAR_ENEMIES, 1);
                        clear.setBackground(actionBackground(a, true));
                        clear.postDelayed(new Runnable() {
                            @Override public void run() { clear.setBackground(actionBackground(a, false)); }
                        }, ACTION_FLASH_MS);
                    }
                });
                addToRow(a, row, clear);
            }
        };
        spec.title = tr(POWER_NAME[id]);
        spec.icon = powerIcon(act, id);
        int[] w = nWorldState();
        boolean on = sPowerLevels[id] > 0;
        if (id == P_RAIN) {
            // Ceu limpo, nuvem, chuva e o escuro da tempestade.
            spec.colors = new int[] {0xFFA8DDFF, 0xFF8DB6E0, 0xFF6F84B5, 0xFF4C5583, 0xFF262A45};
            spec.stops = new float[] {0f, 0.25f, 0.5f, 0.75f, 1f};
            spec.ticks = new int[] {25, 50, 75};
            spec.captions = new String[] {tr("WeatherClear"), tr("Rain"), tr("Storm")};
            spec.initial = on ? sPowerLevels[id] - 1 : (w != null && w.length > 3 && w[3] >= 0 ? w[3] : 0);
        } else if (id == P_WIND) {
            // Calmo no meio, ventania nas pontas; cada lado e uma direcao.
            spec.colors = new int[] {0xFF1FA59A, 0xFF8FD9CF, 0xFFEEF4FF, 0xFF8FD9CF, 0xFF1FA59A};
            spec.stops = new float[] {0f, 0.25f, 0.5f, 0.75f, 1f};
            spec.ticks = new int[] {50};
            spec.captions = new String[] {"← 40 mph", tr("Calm"), "40 mph →"};
            spec.initial = on ? sPowerLevels[id] - 1 : (w != null && w.length > 4 && w[4] >= 0 ? w[4] : 50);
        } else {
            // Paz, o normal e o perigo.
            spec.colors = new int[] {0xFF22A851, 0xFFF2D04B, 0xFFD9342B};
            spec.stops = new float[] {0f, 0.5f, 1f};
            spec.ticks = new int[] {50};
            spec.captions = new String[] {tr("None"), "x1", "x10"};
            spec.initial = on ? sPowerLevels[id] - 1 : 50;
        }
        return spec;
    }

    /**
     * Sem o preenchimento automatico do Android: na busca, o teclado oferecia
     * "senha salva" e os logins guardados, como se fosse um formulario.
     */
    static void noAutofill(View v) {
        if (android.os.Build.VERSION.SDK_INT >= 26) {
            v.setImportantForAutofill(View.IMPORTANT_FOR_AUTOFILL_NO_EXCLUDE_DESCENDANTS);
        }
    }

    private static final int[] QTY_ITEM = {1, 10, 99, 9999};
    private static final int[] QTY_NPC = {1, 5, 10, 50};
    private static final int[] QTY_BUFF = {1, 5, 15, 60};
    private static final int[] sQtyChoice = {-1, -1, -1};

    private static String qtyText(Section s, int n) {
        return s.buff() ? n + " min" : "x" + n;
    }

    private static int[] qtyOptions(Section s) {
        return s.npc() ? QTY_NPC : s.buff() ? QTY_BUFF : QTY_ITEM;
    }

    private static int qtyDefault(Section s) {
        int chosen = sQtyChoice[s.spriteKind()];
        if (chosen > 0) return chosen;
        if (s.npc()) return 1;
        if (s.buff()) return 5;
        return s.defaultQty > 1 ? 9999 : 1;
    }

    private static int sCurrentSection = -1;
    private static final int SEARCH_DELAY_MS = 150;
    private static final long GIVEN_FLASH_MS = 1200;

    private static void select(final Activity act, final LinearLayout content,
                               final Section[] all, final View[] buttons, final int index) {
        sCurrentSection = index;
        for (int i = 0; i < buttons.length; i++) {
            buttons[i].setBackground(i == index ? gold(act, true) : well(act, true));
        }
        final Section s = all[index];
        content.removeAllViews();
        if (s.kind == Section.POWER) { showPowers(act, content, s); return; }
        if (s.modsRoot) { showMods(act, content, s, index); return; }
        showBrowser(act, content, s, index);
    }

    private static ArrayList<Section> visibleChildren(Section parent) {
        ArrayList<Section> shown = new ArrayList<Section>();
        if (parent.children == null) return shown;
        for (Section f : parent.children) {
            if (f.kind == Section.FOLDERS) {
                if (!parent.fromCatalog || f.children.length == 0) { shown.add(f); continue; }
                Section first = f.children[0];
                first.load();
                if (first.total() > 0) shown.add(f);
                continue;
            }
            f.load();
            if (!parent.fromCatalog || f.subClass < 0 || f.total() > 0) shown.add(f);
        }
        return shown;
    }

    private static TextView crumbText(Activity act, String s, boolean dim) {
        TextView t = text(act, s, dim ? 12 : 15, dim ? INK_DIM : INK);
        t.setSingleLine(true);
        t.setPadding(px(act, 6), 0, 0, 0);
        return t;
    }

    private static LinearLayout crumbs(Activity act, Section icon, String... parts) {
        LinearLayout t = new LinearLayout(act);
        t.setOrientation(LinearLayout.HORIZONTAL);
        t.setGravity(Gravity.CENTER_VERTICAL);
        t.addView(sectionIcon(act, icon, 24));
        for (int i = 0; i < parts.length; i++) {
            if (parts[i] == null) continue;
            if (i > 0) t.addView(crumbText(act, "›", true));
            t.addView(crumbText(act, parts[i], false));
        }
        return t;
    }

    private static TextView chip(Activity act, String label, boolean on) {
        TextView b = text(act, label, 12, INK);
        b.setSingleLine(true);
        b.setGravity(Gravity.CENTER);
        b.setPadding(px(act, 10), px(act, 4), px(act, 10), px(act, 4));
        b.setBackground(on ? gold(act, false) : well(act, false));
        return b;
    }

    private static void addSpaced(Activity act, LinearLayout row, View v, int w, int h) {
        LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(w, h);
        lp.rightMargin = px(act, 3);
        row.addView(v, lp);
    }

    private static View iconTab(Activity act, Section s, boolean on) {
        FrameLayout b = new FrameLayout(act);
        b.setContentDescription(s.title);
        b.setBackground(on ? gold(act, false) : well(act, false));
        b.addView(sectionIcon(act, s, 22), new FrameLayout.LayoutParams(px(act, 22), px(act, 22), Gravity.CENTER));
        return b;
    }

    private static View loadingPage(Activity act, LinearLayout content, Section s, Runnable redraw) {
        content.addView(crumbs(act, s, s.title), new LinearLayout.LayoutParams(
            LinearLayout.LayoutParams.MATCH_PARENT, px(act, 34)));
        View v = loadingView(act, tr("ReadingGameCatalog"));
        content.addView(v, new LinearLayout.LayoutParams(LinearLayout.LayoutParams.MATCH_PARENT, 0, 1f));
        withCatalog(act, redraw);
        return v;
    }

    private static void showBrowser(final Activity act, final LinearLayout content,
                                    final Section group, final int index) {
        content.removeAllViews();
        final Runnable redraw = new Runnable() {
            @Override public void run() {
                if (sCurrentSection == index) showBrowser(act, content, group, index);
            }
        };
        if (sCatalog == null) { loadingPage(act, content, group, redraw); return; }

        final ArrayList<Section> level1 = visibleChildren(group);
        if (level1.isEmpty()) {
            content.addView(crumbs(act, group, group.title));
            return;
        }
        if (group.tab >= level1.size()) group.tab = 0;
        final Section cur = level1.get(group.tab);
        Section leaf = cur;
        ArrayList<Section> level2 = null;
        if (cur.kind == Section.FOLDERS) {
            level2 = visibleChildren(cur);
            if (cur.tab >= level2.size()) cur.tab = 0;
            leaf = level2.isEmpty() ? null : level2.get(cur.tab);
        }

        LinearLayout tabs = new LinearLayout(act);
        tabs.setOrientation(LinearLayout.HORIZONTAL);
        tabs.setGravity(Gravity.CENTER_VERTICAL);
        LinearLayout first = new LinearLayout(act);
        first.setOrientation(LinearLayout.HORIZONTAL);
        first.setGravity(Gravity.CENTER_VERTICAL);
        for (int i = 0; i < level1.size(); i++) {
            final int k = i;
            Section c = level1.get(i);
            View b = group.iconTabs ? iconTab(act, c, i == group.tab) : chip(act, c.title, i == group.tab);
            b.setOnClickListener(new View.OnClickListener() {
                @Override public void onClick(View v) { group.tab = k; showBrowser(act, content, group, index); }
            });
            if (group.iconTabs) addSpaced(act, first, b, px(act, 34), px(act, 32));
            else addSpaced(act, first, b, LinearLayout.LayoutParams.WRAP_CONTENT, px(act, 30));
        }
        HorizontalScrollView firstScroll = new HorizontalScrollView(act);
        firstScroll.setHorizontalScrollBarEnabled(false);
        firstScroll.addView(first);
        if (level2 != null && level2.size() > 1) {
            tabs.addView(firstScroll, new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.WRAP_CONTENT, LinearLayout.LayoutParams.WRAP_CONTENT));
            View sep = seam(act, true);
            LinearLayout.LayoutParams sp = new LinearLayout.LayoutParams(Math.max(1, px(act, 2)), px(act, 24));
            sp.leftMargin = px(act, 4);
            sp.rightMargin = px(act, 7);
            tabs.addView(sep, sp);
            LinearLayout second = new LinearLayout(act);
            second.setOrientation(LinearLayout.HORIZONTAL);
            second.setGravity(Gravity.CENTER_VERTICAL);
            for (int i = 0; i < level2.size(); i++) {
                final int k = i;
                TextView b = chip(act, level2.get(i).title, i == cur.tab);
                b.setOnClickListener(new View.OnClickListener() {
                    @Override public void onClick(View v) { cur.tab = k; showBrowser(act, content, group, index); }
                });
                addSpaced(act, second, b, LinearLayout.LayoutParams.WRAP_CONTENT, px(act, 28));
            }
            HorizontalScrollView secondScroll = new HorizontalScrollView(act);
            secondScroll.setHorizontalScrollBarEnabled(false);
            secondScroll.addView(second);
            tabs.addView(secondScroll, new LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.WRAP_CONTENT, 1f));
        } else {
            tabs.addView(firstScroll, new LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.WRAP_CONTENT, 1f));
        }

        if (leaf == null) {
            content.addView(crumbs(act, group, group.title, cur.title));
            content.addView(tabs);
            return;
        }
        showGrid(act, content, leaf, crumbs(act, group, group.title, cur.title), tabs);
    }

    private static void showMods(final Activity act, final LinearLayout content,
                                 final Section mods, final int index) {
        content.removeAllViews();
        if (mods.tab >= mods.children.length) mods.tab = 0;
        final Section mod = mods.children[mods.tab];

        LinearLayout row = new LinearLayout(act);
        row.setOrientation(LinearLayout.HORIZONTAL);
        content.addView(row, new LinearLayout.LayoutParams(
            LinearLayout.LayoutParams.MATCH_PARENT, 0, 1f));

        LinearLayout list = new LinearLayout(act);
        list.setOrientation(LinearLayout.VERTICAL);
        list.setPadding(0, 0, px(act, 6), 0);
        for (int i = 0; i < mods.children.length; i++) {
            final int k = i;
            Section m = mods.children[i];
            LinearLayout b = new LinearLayout(act);
            b.setOrientation(LinearLayout.HORIZONTAL);
            b.setGravity(Gravity.CENTER_VERTICAL);
            b.setPadding(px(act, 7), px(act, 5), px(act, 7), px(act, 5));
            b.setBackground(i == mods.tab ? gold(act, true) : well(act, true));
            b.addView(sectionIcon(act, m, 26));
            TextView name = text(act, m.title, 12, INK);
            name.setSingleLine(true);
            name.setEllipsize(android.text.TextUtils.TruncateAt.END);
            name.setPadding(px(act, 7), 0, 0, 0);
            b.addView(name, new LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.WRAP_CONTENT, 1f));
            b.setOnClickListener(new View.OnClickListener() {
                @Override public void onClick(View v) { mods.tab = k; showMods(act, content, mods, index); }
            });
            LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT, px(act, 42));
            lp.bottomMargin = px(act, 4);
            list.addView(b, lp);
        }
        ScrollView listScroll = new ScrollView(act);
        listScroll.setVerticalScrollBarEnabled(false);
        listScroll.addView(list);
        row.addView(listScroll, new LinearLayout.LayoutParams(px(act, 200), LinearLayout.LayoutParams.MATCH_PARENT));
        row.addView(seam(act, true));

        final LinearLayout inner = new LinearLayout(act);
        inner.setOrientation(LinearLayout.VERTICAL);
        inner.setPadding(px(act, 8), 0, 0, 0);
        row.addView(inner, new LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.MATCH_PARENT, 1f));
        showModFolders(act, inner, mod, index);
    }

    private static void showModFolders(final Activity act, final LinearLayout inner,
                                       final Section mod, final int index) {
        inner.removeAllViews();
        final Runnable redraw = new Runnable() {
            @Override public void run() {
                if (sCurrentSection == index) showModFolders(act, inner, mod, index);
            }
        };
        if (sCatalog == null) { loadingPage(act, inner, mod, redraw); return; }
        if (mod.tab >= mod.children.length) mod.tab = 0;
        Section leaf = mod.children[mod.tab];
        LinearLayout tabs = new LinearLayout(act);
        tabs.setOrientation(LinearLayout.HORIZONTAL);
        for (int i = 0; i < mod.children.length; i++) {
            final int k = i;
            Section f = mod.children[i];
            LinearLayout b = new LinearLayout(act);
            b.setOrientation(LinearLayout.HORIZONTAL);
            b.setGravity(Gravity.CENTER_VERTICAL);
            b.setPadding(px(act, 8), 0, px(act, 10), 0);
            b.setBackground(i == mod.tab ? gold(act, false) : well(act, false));
            b.addView(sectionIcon(act, f, 20));
            TextView name = text(act, f.title, 12, INK);
            name.setSingleLine(true);
            name.setPadding(px(act, 5), 0, 0, 0);
            b.addView(name);
            b.setOnClickListener(new View.OnClickListener() {
                @Override public void onClick(View v) { mod.tab = k; showModFolders(act, inner, mod, index); }
            });
            addSpaced(act, tabs, b, LinearLayout.LayoutParams.WRAP_CONTENT, px(act, 30));
        }
        HorizontalScrollView scroll = new HorizontalScrollView(act);
        scroll.setHorizontalScrollBarEnabled(false);
        scroll.addView(tabs);
        showGrid(act, inner, leaf, crumbs(act, mod, mod.title), scroll);
    }

    private static final class CellViews {
        ImageView sprite;
        TextView given;
        int state = -1;
    }

    private static void showGrid(final Activity act, final LinearLayout content, final Section s,
                                 View title, View tabs) {
        s.filtered = null;
        s.load();

        LinearLayout header = new LinearLayout(act);
        header.setOrientation(LinearLayout.HORIZONTAL);
        header.setGravity(Gravity.CENTER_VERTICAL);
        header.addView(title, new LinearLayout.LayoutParams(
            LinearLayout.LayoutParams.WRAP_CONTENT, LinearLayout.LayoutParams.WRAP_CONTENT));

        LinearLayout searchBox = new LinearLayout(act);
        searchBox.setOrientation(LinearLayout.HORIZONTAL);
        searchBox.setGravity(Gravity.CENTER_VERTICAL);
        searchBox.setBackground(well(act, false));
        searchBox.setPadding(px(act, 8), 0, px(act, 6), 0);
        searchBox.addView(icon(act, sprite(act, "ic_lupa"), 14));
        final EditText searchField = new EditText(act);
        searchField.setSingleLine(true);
        searchField.setBackground(null);
        searchField.setTextColor(INK);
        searchField.setHintTextColor(INK_DIM);
        searchField.setHint(tr("SearchNameOrId"));
        searchField.setInputType(android.text.InputType.TYPE_CLASS_TEXT
            | android.text.InputType.TYPE_TEXT_VARIATION_FILTER
            | android.text.InputType.TYPE_TEXT_FLAG_NO_SUGGESTIONS);
        noAutofill(searchField);
        applyTextSize(act, searchField, 11);
        searchField.setTypeface(font(act));
        searchField.setPadding(px(act, 6), px(act, 2), 0, px(act, 2));
        searchField.setImeOptions(android.view.inputmethod.EditorInfo.IME_FLAG_NO_EXTRACT_UI
            | android.view.inputmethod.EditorInfo.IME_FLAG_NO_FULLSCREEN
            | android.view.inputmethod.EditorInfo.IME_ACTION_SEARCH);
        searchField.setOnFocusChangeListener(new View.OnFocusChangeListener() {
            @Override public void onFocusChange(View v, boolean focus) { menuKeyboard(act, focus); }
        });
        searchBox.addView(searchField, new LinearLayout.LayoutParams(
            0, LinearLayout.LayoutParams.WRAP_CONTENT, 1f));
        LinearLayout.LayoutParams bp = new LinearLayout.LayoutParams(0, px(act, 32), 1f);
        bp.leftMargin = px(act, 10);
        bp.rightMargin = px(act, 8);
        header.addView(searchBox, bp);

        final Runnable[] applyMode = { null };
        final View[] modeButtons = new View[2];
        for (int m = 0; m < 2; m++) {
            final boolean list = m == 1;
            FrameLayout b = new FrameLayout(act);
            b.setContentDescription(tr(list ? "ViewList" : "ViewIcons"));
            b.setBackground(list == listMode(act) ? gold(act, false) : well(act, false));
            ImageView ic = new ImageView(act);
            setBitmap(act, ic, modeIcon(list));
            b.addView(ic, new FrameLayout.LayoutParams(px(act, 18), px(act, 18), Gravity.CENTER));
            b.setOnClickListener(new View.OnClickListener() {
                @Override public void onClick(View v) {
                    if (list == listMode(act)) return;
                    setListMode(act, list);
                    modeButtons[0].setBackground(list ? well(act, false) : gold(act, false));
                    modeButtons[1].setBackground(list ? gold(act, false) : well(act, false));
                    if (applyMode[0] != null) applyMode[0].run();
                }
            });
            modeButtons[m] = b;
            LinearLayout.LayoutParams mp = new LinearLayout.LayoutParams(px(act, 32), px(act, 32));
            mp.rightMargin = px(act, m == 1 ? 8 : 3);
            header.addView(b, mp);
        }

        final int[] qty = { qtyDefault(s) };
        final int[] options = qtyOptions(s);
        final TextView[] qtyButtons = new TextView[options.length];
        for (int i = 0; i < options.length; i++) {
            final int n = options[i];
            final TextView b = text(act, qtyText(s, n), 12, INK);
            b.setSingleLine(true);
            b.setGravity(Gravity.CENTER);
            b.setPadding(px(act, 7), 0, px(act, 7), 0);
            b.setMinWidth(px(act, 38));
            b.setBackground(actionBackground(act, n == qty[0]));
            b.setOnClickListener(new View.OnClickListener() {
                @Override public void onClick(View v) {
                    qty[0] = n;
                    sQtyChoice[s.spriteKind()] = n;
                    for (int k = 0; k < options.length; k++) {
                        qtyButtons[k].setBackground(actionBackground(act, options[k] == n));
                    }
                }
            });
            qtyButtons[i] = b;
            LinearLayout.LayoutParams qp = new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.WRAP_CONTENT, px(act, 32));
            qp.leftMargin = px(act, 3);
            header.addView(b, qp);
        }
        content.addView(header, new LinearLayout.LayoutParams(
            LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.WRAP_CONTENT));

        if (tabs != null) {
            LinearLayout.LayoutParams tp = new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.WRAP_CONTENT);
            tp.topMargin = px(act, 6);
            content.addView(tabs, tp);
        }

        final LinearLayout strip = new LinearLayout(act);
        strip.setOrientation(LinearLayout.HORIZONTAL);
        strip.setGravity(Gravity.CENTER_VERTICAL);
        strip.setBackground(well(act, false));
        strip.setPadding(px(act, 10), 0, px(act, 10), 0);
        final ImageView stripSprite = icon(act, null, 24);
        strip.addView(stripSprite);
        final TextView stripName = text(act, "", 14, INK);
        stripName.setSingleLine(true);
        stripName.setEllipsize(android.text.TextUtils.TruncateAt.END);
        stripName.setPadding(px(act, 8), 0, 0, 0);
        strip.addView(stripName, new LinearLayout.LayoutParams(
            0, LinearLayout.LayoutParams.WRAP_CONTENT, 1f));
        final TextView stripId = text(act, "", 11, INK_DIM);
        stripId.setPadding(px(act, 8), 0, px(act, 12), 0);
        strip.addView(stripId);
        final TextView countLabel = text(act, "", 11, INK_DIM);
        strip.addView(countLabel);

        final int[] selectedId = { -1 };
        final int[] givenId = { -1 };
        final Runnable showSelected = new Runnable() {
            @Override public void run() {
                int id = selectedId[0];
                if (id < 0) {
                    setBitmap(act, stripSprite, null);
                    stripName.setText("");
                    stripId.setText("");
                    return;
                }
                int k = -1;
                for (int i = 0; i < s.total(); i++) if (s.ids[i] == id) { k = i; break; }
                String raw = k >= 0 ? s.nameAt(k) : null;
                stripName.setText(raw == null || raw.length() == 0 ? ("#" + id) : raw);
                stripId.setText((s.npc() ? "NPC " : s.buff() ? "buff " : "id ") + id);
                requestSprite(act, stripSprite, s.spriteKind(), id);
            }
        };
        final Runnable showCount = new Runnable() {
            @Override public void run() {
                int n = s.filtered == null ? s.total() : s.count();
                countLabel.setText(s.filtered == null ? contents(s) : tr("CountOfTotal", n, s.total()));
            }
        };
        showCount.run();

        if (s.total() == 0) {
            TextView empty = text(act, s.npc() ? tr("NoNPCsHere") : s.buff() ? tr("NoBuffsHere")
                : tr("ItemClassesMissing"), 12, INK_DIM);
            empty.setGravity(Gravity.CENTER);
            content.addView(empty, new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT, 0, 1f));
            return;
        }

        final FrameLayout holder = new FrameLayout(act);
        final android.widget.AbsListView[] view = { null };
        final int cell = px(act, 46);
        final BaseAdapter adapter = new BaseAdapter() {
            @Override public int getCount() { return s.count(); }
            @Override public Object getItem(int i) { return null; }
            @Override public long getItemId(int i) { return i; }
            @Override public View getView(int i, View recycled, ViewGroup parent) {
                if (parent instanceof ListView) return row(act, s, s.indexAt(i), recycled, selectedId[0], givenId[0], qty[0]);
                return cell(act, s, s.indexAt(i), recycled, cell, selectedId[0], givenId[0], qty[0]);
            }
        };
        final android.widget.AdapterView.OnItemClickListener give = new android.widget.AdapterView.OnItemClickListener() {
            @Override public void onItemClick(android.widget.AdapterView<?> p, View v, int position, long l) {
                final int id = s.ids[s.indexAt(position)];
                int n = qty[0] < 1 ? 1 : qty[0];
                if (s.buff()) nOnBuff(id, n * 60);
                else if (s.npc()) nOnSpawn(id, n);
                else nOnGive(id, n);
                selectedId[0] = id;
                givenId[0] = id;
                showSelected.run();
                adapter.notifyDataSetChanged();
                holder.postDelayed(new Runnable() {
                    @Override public void run() {
                        if (givenId[0] != id) return;
                        givenId[0] = -1;
                        adapter.notifyDataSetChanged();
                    }
                }, GIVEN_FLASH_MS);
            }
        };
        applyMode[0] = new Runnable() {
            @Override public void run() {
                int first = view[0] == null ? 0 : view[0].getFirstVisiblePosition();
                android.widget.AbsListView v;
                if (listMode(act)) {
                    ListView l = new ListView(act);
                    l.setDivider(new android.graphics.drawable.ColorDrawable(0));
                    l.setDividerHeight(px(act, 3));
                    v = l;
                } else {
                    GridView g = new GridView(act);
                    g.setColumnWidth(cell);
                    g.setNumColumns(GridView.AUTO_FIT);
                    g.setStretchMode(GridView.STRETCH_COLUMN_WIDTH);
                    g.setHorizontalSpacing(px(act, 4));
                    g.setVerticalSpacing(px(act, 4));
                    v = g;
                }
                v.setSelector(new android.graphics.drawable.ColorDrawable(0));
                v.setCacheColorHint(0);
                v.setVerticalScrollBarEnabled(false);
                v.setAdapter(adapter);
                v.setOnItemClickListener(give);
                holder.removeAllViews();
                holder.addView(v, new FrameLayout.LayoutParams(
                    FrameLayout.LayoutParams.MATCH_PARENT, FrameLayout.LayoutParams.MATCH_PARENT));
                v.setSelection(first);
                view[0] = v;
            }
        };
        applyMode[0].run();
        LinearLayout.LayoutParams gp = new LinearLayout.LayoutParams(
            LinearLayout.LayoutParams.MATCH_PARENT, 0, 1f);
        gp.topMargin = px(act, 6);
        gp.bottomMargin = px(act, 6);
        content.addView(holder, gp);
        content.addView(strip, new LinearLayout.LayoutParams(
            LinearLayout.LayoutParams.MATCH_PARENT, px(act, 36)));

        final Runnable[] pending = { null };
        searchField.addTextChangedListener(new TextWatcher() {
            @Override public void onTextChanged(CharSequence t, int a1, int b1, int c1) {
                if (pending[0] != null) holder.removeCallbacks(pending[0]);
                final String term = t.toString();
                pending[0] = new Runnable() {
                    @Override public void run() {
                        s.applyFilter(term);
                        showCount.run();
                        adapter.notifyDataSetChanged();
                        if (view[0] != null) view[0].setSelection(0);
                    }
                };
                holder.postDelayed(pending[0], SEARCH_DELAY_MS);
            }
            @Override public void beforeTextChanged(CharSequence t, int a1, int b1, int c1) { }
            @Override public void afterTextChanged(Editable e) { }
        });
    }

    private static Boolean sListMode;

    private static boolean listMode(Activity act) {
        if (sListMode == null) {
            boolean v = false;
            try { v = act.getSharedPreferences(PREFS, Activity.MODE_PRIVATE).getBoolean("listMode", false); }
            catch (Throwable t) { }
            sListMode = v;
        }
        return sListMode;
    }

    private static void setListMode(Activity act, boolean on) {
        sListMode = on;
        try { act.getSharedPreferences(PREFS, Activity.MODE_PRIVATE).edit().putBoolean("listMode", on).apply(); }
        catch (Throwable t) { }
    }

    private static final Bitmap[] sModeIcons = new Bitmap[2];

    private static Bitmap modeIcon(boolean list) {
        int k = list ? 1 : 0;
        if (sModeIcons[k] != null) return sModeIcons[k];
        int size = 11;
        Bitmap b = Bitmap.createBitmap(size, size, Bitmap.Config.ARGB_8888);
        int ink = 0xFFFFFFFF, edge = 0xFF000000;
        for (int y = 1; y < size - 1; y++) {
            for (int x = 1; x < size - 1; x++) {
                int ix = x - 1, iy = y - 1;
                boolean on = list ? (iy % 3 != 2 && ix != 2) : (ix % 5 != 4 && iy % 5 != 4);
                if (on) b.setPixel(x, y, ink);
            }
        }
        for (int y = 0; y < size; y++) {
            for (int x = 0; x < size; x++) {
                if (b.getPixel(x, y) != 0) continue;
                boolean near = (x > 0 && b.getPixel(x - 1, y) == ink) || (x < size - 1 && b.getPixel(x + 1, y) == ink)
                    || (y > 0 && b.getPixel(x, y - 1) == ink) || (y < size - 1 && b.getPixel(x, y + 1) == ink);
                if (near) b.setPixel(x, y, edge);
            }
        }
        sModeIcons[k] = b;
        return b;
    }

    private static final class RowViews {
        ImageView sprite, action;
        TextView name, detail, given;
        int state = -1;
    }

    private static View row(Activity act, Section s, int i, View recycled,
                            int selectedId, int givenId, int qty) {
        LinearLayout r;
        RowViews L;
        if (recycled instanceof LinearLayout && recycled.getTag() instanceof RowViews) {
            r = (LinearLayout) recycled;
            L = (RowViews) recycled.getTag();
        } else {
            L = new RowViews();
            r = new LinearLayout(act);
            r.setOrientation(LinearLayout.HORIZONTAL);
            r.setGravity(Gravity.CENTER_VERTICAL);
            r.setPadding(px(act, 8), px(act, 5), px(act, 6), px(act, 5));
            L.sprite = icon(act, null, 30);
            r.addView(L.sprite);
            LinearLayout labels = new LinearLayout(act);
            labels.setOrientation(LinearLayout.HORIZONTAL);
            labels.setGravity(Gravity.BOTTOM);
            labels.setPadding(px(act, 10), 0, 0, 0);
            L.name = text(act, "", 14, INK);
            L.name.setSingleLine(true);
            L.detail = text(act, "", 10, INK_DIM);
            L.detail.setPadding(px(act, 8), 0, 0, px(act, 2));
            L.given = text(act, "", 11, GRASS_LIT);
            L.given.setPadding(px(act, 10), 0, 0, px(act, 1));
            labels.addView(L.name);
            labels.addView(L.detail);
            labels.addView(L.given);
            r.addView(labels, new LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.WRAP_CONTENT, 1f));
            L.action = new ImageView(act);
            L.action.setScaleType(ImageView.ScaleType.FIT_CENTER);
            L.action.setPadding(px(act, 8), px(act, 6), px(act, 8), px(act, 6));
            L.action.setBackground(actionBackground(act, false));
            r.addView(L.action, new LinearLayout.LayoutParams(px(act, 46), px(act, 34)));
            r.setLayoutParams(new android.widget.AbsListView.LayoutParams(
                android.widget.AbsListView.LayoutParams.MATCH_PARENT,
                android.widget.AbsListView.LayoutParams.WRAP_CONTENT));
            r.setTag(L);
        }
        final int id = s.ids[i];
        String raw = s.nameAt(i);
        L.name.setText(raw == null || raw.length() == 0 ? ("#" + id) : raw);
        L.detail.setText(s.npc() ? ("NPC " + id) : s.buff() ? ("buff " + id) : ("id " + id));
        int state = id == selectedId ? 1 : 0;
        if (L.state != state) {
            r.setBackground(state == 1 ? selected(act, false) : well(act, false));
            L.state = state;
        }
        L.given.setText(id == givenId ? qtyText(s, qty).replace(" min", "m").replace("x", "+") : "");
        setBitmap(act, L.action, sprite(act, s.npc() ? "ic_invocar" : "ic_pegar"));
        requestSprite(act, L.sprite, s.spriteKind(), id);
        return r;
    }

    private static String contents(Section f) {
        int n = f.total();
        return tr(f.npc() ? (n == 1 ? "NpcCountOne" : "NpcCountOther")
                  : f.buff() ? (n == 1 ? "BuffCountOne" : "BuffCountOther")
                  : (n == 1 ? "ItemCountOne" : "ItemCountOther"), n);
    }

    private static View cell(Activity act, Section s, int i, View recycled, int size,
                             int selectedId, int givenId, int qty) {
        FrameLayout c;
        CellViews L;
        if (recycled instanceof FrameLayout && recycled.getTag() instanceof CellViews) {
            c = (FrameLayout) recycled;
            L = (CellViews) recycled.getTag();
        } else {
            c = new FrameLayout(act);
            c.setLayoutParams(new android.widget.AbsListView.LayoutParams(
                android.widget.AbsListView.LayoutParams.MATCH_PARENT, size));
            L = new CellViews();
            L.sprite = new ImageView(act);
            L.sprite.setScaleType(ImageView.ScaleType.FIT_CENTER);
            c.addView(L.sprite, new FrameLayout.LayoutParams(px(act, 32), px(act, 32), Gravity.CENTER));
            L.given = text(act, "", 10, GRASS_LIT);
            L.given.setSingleLine(true);
            FrameLayout.LayoutParams gl = new FrameLayout.LayoutParams(
                FrameLayout.LayoutParams.WRAP_CONTENT, FrameLayout.LayoutParams.WRAP_CONTENT,
                Gravity.BOTTOM | Gravity.RIGHT);
            gl.rightMargin = px(act, 3);
            c.addView(L.given, gl);
            c.setTag(L);
        }
        final int id = s.ids[i];
        String raw = s.nameAt(i);
        c.setContentDescription(raw == null || raw.length() == 0 ? ("#" + id) : raw);
        int state = id == selectedId ? 1 : 0;
        if (L.state != state) {
            c.setBackground(state == 1 ? selected(act, true) : well(act, true));
            L.state = state;
        }
        if (id == givenId) {
            L.given.setText(qtyText(s, qty).replace(" min", "m").replace("x", "+"));
            L.given.setVisibility(View.VISIBLE);
        } else {
            L.given.setVisibility(View.GONE);
        }
        requestSprite(act, L.sprite, s.spriteKind(), id);
        return c;
    }

    private static int sPowerTab, sPowerSel = -1;
    private static final int[] GROUP_ICON = {58, 1315, 1844};

    private static int powerColumns(Activity a) {
        int n = (int) (contentUnits(a) / 132);
        return n < 3 ? 3 : n > 8 ? 8 : n;
    }

    private static void showPowers(final Activity act, final LinearLayout content, final Section s) {
        content.removeAllViews();
        readWorldState();
        if (sPowerTab >= s.powerGroups.length) sPowerTab = 0;
        final int g = s.powerGroups[sPowerTab];
        final int[] group = POWER_GROUPS[g];
        if (sPowerSel < 0 || Arrays.binarySearch(sortedCopy(group), sPowerSel) < 0) sPowerSel = group[0];

        LinearLayout header = new LinearLayout(act);
        header.setOrientation(LinearLayout.HORIZONTAL);
        header.setGravity(Gravity.CENTER_VERTICAL);
        for (int i = 0; i < s.powerGroups.length; i++) {
            final int k = i;
            int gi = s.powerGroups[i];
            LinearLayout b = new LinearLayout(act);
            b.setOrientation(LinearLayout.HORIZONTAL);
            b.setGravity(Gravity.CENTER_VERTICAL);
            b.setPadding(px(act, 9), 0, px(act, 11), 0);
            b.setBackground(i == sPowerTab ? gold(act, false) : well(act, false));
            b.addView(icon(act, spriteJa(act, SPR_ITEM, GROUP_ICON[gi]), 20));
            TextView t = text(act, tr(POWER_GROUP_NAME[gi]), 13, INK);
            t.setSingleLine(true);
            t.setPadding(px(act, 6), 0, 0, 0);
            b.addView(t);
            b.setOnClickListener(new View.OnClickListener() {
                @Override public void onClick(View v) { sPowerTab = k; sPowerSel = -1; showPowers(act, content, s); }
            });
            addSpaced(act, header, b, LinearLayout.LayoutParams.WRAP_CONTENT, px(act, 32));
        }
        header.addView(new View(act), new LinearLayout.LayoutParams(0, 1, 1f));

        final View[] cards = new View[POWER_NAME.length];
        final LinearLayout offButton = new LinearLayout(act);
        offButton.setOrientation(LinearLayout.HORIZONTAL);
        offButton.setGravity(Gravity.CENTER_VERTICAL);
        offButton.setBackground(actionBackground(act, false));
        offButton.setPadding(px(act, 7), 0, px(act, 10), 0);
        final TextView activeLabel = text(act, "", 10, INK);
        activeLabel.setGravity(Gravity.CENTER);
        activeLabel.setMinWidth(px(act, 18));
        activeLabel.setBackground(pixel(act, GRASS, true, false));
        offButton.addView(activeLabel, new LinearLayout.LayoutParams(
            LinearLayout.LayoutParams.WRAP_CONTENT, px(act, 18)));
        TextView offText = text(act, tr("TurnOffAll"), 12, INK);
        offText.setPadding(px(act, 6), 0, 0, 0);
        offButton.addView(offText);
        offButton.setOnClickListener(new View.OnClickListener() {
            @Override public void onClick(View v) {
                for (int i = 0; i < sPowerLevels.length; i++) {
                    if (sPowerLevels[i] == 0) continue;
                    sPowerLevels[i] = 0;
                    nSetPower(i, 0);
                    if (cards[i] != null) paintPowerCard(act, cards[i], i);
                }
                if (sRailPaint != null) sRailPaint.run();
                savePowers(act);
                updateActiveCount(activeLabel);
            }
        });
        header.addView(offButton, new LinearLayout.LayoutParams(
            LinearLayout.LayoutParams.WRAP_CONTENT, px(act, 32)));
        sActiveLabel = activeLabel;
        content.addView(header, new LinearLayout.LayoutParams(
            LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.WRAP_CONTENT));

        final LinearLayout strip = new LinearLayout(act);
        strip.setOrientation(LinearLayout.HORIZONTAL);
        strip.setGravity(Gravity.CENTER_VERTICAL);
        strip.setBackground(well(act, false));
        strip.setPadding(px(act, 10), 0, px(act, 10), 0);
        final ImageView stripIcon = icon(act, null, 22);
        strip.addView(stripIcon);
        final TextView stripName = text(act, "", 14, INK);
        stripName.setSingleLine(true);
        stripName.setPadding(px(act, 8), 0, 0, 0);
        strip.addView(stripName);
        final TextView stripDesc = text(act, "", 12, INK_DIM);
        stripDesc.setSingleLine(true);
        stripDesc.setEllipsize(android.text.TextUtils.TruncateAt.END);
        stripDesc.setPadding(px(act, 10), 0, 0, 0);
        strip.addView(stripDesc, new LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.WRAP_CONTENT, 1f));
        final TextView stripLevels = text(act, "", 11, INK_DIM);
        stripLevels.setSingleLine(true);
        stripLevels.setPadding(px(act, 10), 0, 0, 0);
        strip.addView(stripLevels);
        final Runnable showSelected = new Runnable() {
            @Override public void run() {
                int id = sPowerSel;
                setBitmap(act, stripIcon, powerIcon(act, id));
                stripName.setText(tr(POWER_NAME[id]));
                boolean off = isWorldState(id) && sWorld[worldSlot(id)] < 0;
                stripDesc.setText(off ? tr("EnterWorld") : tr(POWER_DESC[id]));
                StringBuilder lv = new StringBuilder();
                if (POWER_LEVELS[id].length > 1) {
                    for (int k = 0; k < POWER_LEVELS[id].length; k++) {
                        if (k > 0) lv.append("  ·  ");
                        lv.append(tr(POWER_LEVELS[id][k]));
                    }
                }
                stripLevels.setText(lv.toString());
            }
        };

        final int columns = powerColumns(act);
        LinearLayout grid = new LinearLayout(act);
        grid.setOrientation(LinearLayout.VERTICAL);
        LinearLayout queue = null;
        for (int k = 0; k < group.length; k++) {
            if (k % columns == 0) {
                queue = new LinearLayout(act);
                queue.setOrientation(LinearLayout.HORIZONTAL);
                grid.addView(queue, new LinearLayout.LayoutParams(
                    LinearLayout.LayoutParams.MATCH_PARENT, px(act, 70)));
            }
            final int id = group[k];
            final View c = powerCard(act, id);
            c.setOnClickListener(new View.OnClickListener() {
                @Override public void onClick(View v) {
                    int previous = sPowerSel;
                    sPowerSel = id;
                    if (previous >= 0 && previous != id && cards[previous] != null) {
                        paintPowerCard(act, cards[previous], previous);
                    }
                    if (isAction(id)) {
                        nSetPower(id, 1);
                        flashActionCard(act, c, id);
                    } else if (isWorldState(id)) {
                        toggleWorldState(act, id);
                        paintPowerCard(act, c, id);
                    } else {
                        sPowerLevels[id] = (sPowerLevels[id] + 1) % (POWER_LEVELS[id].length + 1);
                        nSetPower(id, sPowerLevels[id]);
                        paintPowerCard(act, c, id);
                        savePowers(act);
                        updateActiveCount(activeLabel);
                    }
                    showSelected.run();
                }
            });
            cards[id] = c;
            LinearLayout.LayoutParams cp = new LinearLayout.LayoutParams(
                0, LinearLayout.LayoutParams.MATCH_PARENT, 1f);
            cp.setMargins(px(act, 2), px(act, 2), px(act, 2), px(act, 2));
            queue.addView(c, cp);
        }
        int remainder = (columns - group.length % columns) % columns;
        for (int i = 0; i < remainder; i++) {
            View filler = powerCard(act, group[0]);
            filler.setVisibility(View.INVISIBLE);
            LinearLayout.LayoutParams fp = new LinearLayout.LayoutParams(
                0, LinearLayout.LayoutParams.MATCH_PARENT, 1f);
            fp.setMargins(px(act, 2), px(act, 2), px(act, 2), px(act, 2));
            queue.addView(filler, fp);
        }

        ScrollView scroll = new ScrollView(act);
        scroll.setVerticalScrollBarEnabled(false);
        scroll.addView(grid);
        LinearLayout.LayoutParams sp = new LinearLayout.LayoutParams(
            LinearLayout.LayoutParams.MATCH_PARENT, 0, 1f);
        sp.topMargin = px(act, 6);
        sp.bottomMargin = px(act, 6);
        content.addView(scroll, sp);
        content.addView(strip, new LinearLayout.LayoutParams(
            LinearLayout.LayoutParams.MATCH_PARENT, px(act, 36)));
        showSelected.run();
        updateActiveCount(activeLabel);
    }

    private static int[] sortedCopy(int[] v) {
        int[] c = Arrays.copyOf(v, v.length);
        Arrays.sort(c);
        return c;
    }

    private static final class PowerCardViews {
        TextView nameLabel, levelLabel;
        ImageView icon;
    }

    private static View powerCard(Activity act, int id) {
        PowerCardViews k = new PowerCardViews();
        FrameLayout c = new FrameLayout(act);
        LinearLayout col = new LinearLayout(act);
        col.setOrientation(LinearLayout.VERTICAL);
        col.setGravity(Gravity.CENTER);
        col.setPadding(px(act, 4), px(act, 4), px(act, 4), px(act, 4));
        k.icon = icon(act, powerIcon(act, id), 28);
        col.addView(k.icon);
        k.nameLabel = text(act, tr(POWER_NAME[id]), 11, INK);
        k.nameLabel.setSingleLine(true);
        k.nameLabel.setEllipsize(android.text.TextUtils.TruncateAt.END);
        k.nameLabel.setGravity(Gravity.CENTER);
        LinearLayout.LayoutParams np = new LinearLayout.LayoutParams(
            LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.WRAP_CONTENT);
        np.topMargin = px(act, 3);
        col.addView(k.nameLabel, np);
        c.addView(col, new FrameLayout.LayoutParams(
            FrameLayout.LayoutParams.MATCH_PARENT, FrameLayout.LayoutParams.MATCH_PARENT));
        k.levelLabel = text(act, "", 9, GRASS_LIT);
        k.levelLabel.setSingleLine(true);
        k.levelLabel.setPadding(px(act, 4), 0, px(act, 4), 0);
        FrameLayout.LayoutParams lp = new FrameLayout.LayoutParams(
            FrameLayout.LayoutParams.WRAP_CONTENT, FrameLayout.LayoutParams.WRAP_CONTENT,
            Gravity.TOP | Gravity.RIGHT);
        lp.topMargin = px(act, 4);
        lp.rightMargin = px(act, 4);
        c.addView(k.levelLabel, lp);
        c.setTag(k);
        paintPowerCard(act, c, id);
        return c;
    }

    private static void setBadge(Activity act, TextView t, String label, boolean bright) {
        t.setText(label);
        t.setTextColor(bright ? 0xFFFFF36B : GRASS_LIT);
        t.setBackground(label.length() == 0 ? null : pixel(act, OUTLINE, true, false));
    }

    private static Drawable cardBackground(Activity act, int id, boolean on) {
        if (on) return pixel(act, GRASS, true, true);
        return id == sPowerSel ? selected(act, true) : well(act, true);
    }

    private static void paintPowerCard(Activity act, View c, int id) {
        PowerCardViews k = (PowerCardViews) c.getTag();
        if (isWorldState(id)) { paintWorldCard(act, c, k, id); return; }
        int n = sPowerLevels[id];
        boolean on = n > 0;
        c.setBackground(cardBackground(act, id, on));
        setBadge(act, k.levelLabel, on && POWER_LEVELS[id].length > 1 ? tr(POWER_LEVELS[id][n - 1]) : "", on);
    }

    private static void paintWorldCard(Activity act, View c, PowerCardViews k, int id) {
        int v = sWorld[worldSlot(id)];
        boolean on = v > 0;
        if (id == P_DIFFICULTY) {
            int m = v < 0 ? 0 : Math.min(v, GAME_MODE_ICON.length - 1);
            setBitmap(act, k.icon, sprite(act, GAME_MODE_ICON[m]));
        }
        c.setBackground(cardBackground(act, id, on));
        String label = v < 0 ? "" : id != P_DIFFICULTY ? (v == 1 ? tr("On") : "")
                                                       : tr(GAME_MODE[Math.min(v, GAME_MODE.length - 1)]);
        setBadge(act, k.levelLabel, label, on);
    }

    private static void flashActionCard(final Activity act, final View c, final int id) {
        PowerCardViews k = (PowerCardViews) c.getTag();
        c.setBackground(cardBackground(act, id, true));
        setBadge(act, k.levelLabel, tr("ActionDone"), true);
        c.postDelayed(new Runnable() {
            @Override public void run() {
                paintPowerCard(act, c, id);
            }
        }, ACTION_FLASH_MS);
    }

    private static void updateActiveCount(TextView t) {
        int n = 0;
        for (int v : sPowerLevels) if (v > 0) n++;
        t.setText(String.valueOf(n));
        View button = (View) t.getParent();
        if (button != null) button.setVisibility(n == 0 ? View.INVISIBLE : View.VISIBLE);
    }

    // --- painel de erro ------------------------------------------------------
    //
    // Chamado do nativo a cada BL_ERROR novo. Quem joga no celular nao tem
    // logcat: sem isto, um mod que quebra vira "nao funcionou" sem texto
    // nenhum. Mostra o log, com OK e Copiar; com o painel ja aberto, so troca
    // o texto (um erro atras do outro nao empilha janelas).

    private static AlertDialog sErrorDialog;
    private static String sErrorText = "";

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
        sErrorText = text;
        if (sErrorDialog != null && sErrorDialog.isShowing()) {
            sErrorDialog.setMessage(text);
            return;
        }

        final AlertDialog dlg = new AlertDialog.Builder(act)
            .setTitle("Bunny Loader — error")
            .setMessage(text)
            .setPositiveButton("OK", null)
            .setNeutralButton("Copy", null)
            .create();
        sErrorDialog = dlg;
        dlg.show();

        // Fonte de largura fixa: o trecho do codigo do mod marca a coluna do
        // erro com um ^ embaixo dela, e o texto e um log.
        TextView message = dlg.findViewById(android.R.id.message);
        if (message != null) {
            message.setTypeface(Typeface.MONOSPACE);
            message.setTextSize(TypedValue.COMPLEX_UNIT_SP, 12);
        }

        // Todo botao de AlertDialog fecha o dialogo ao ser tocado, e Copiar nao
        // pode: quem copia o log quase sempre quer continuar lendo. Trocar o
        // ouvinte DEPOIS do show() e a forma de ter um botao que nao fecha.
        Button copyButton = dlg.getButton(AlertDialog.BUTTON_NEUTRAL);
        if (copyButton == null) return;
        copyButton.setOnClickListener(new View.OnClickListener() {
            @Override public void onClick(View v) {
                ClipboardManager cm =
                    (ClipboardManager) act.getSystemService(Activity.CLIPBOARD_SERVICE);
                if (cm != null) cm.setPrimaryClip(ClipData.newPlainText("bunny", sErrorText));
                Toast.makeText(act, "Log copied", Toast.LENGTH_SHORT).show();
            }
        });
    }
}
