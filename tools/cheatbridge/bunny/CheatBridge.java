package bunny;

import android.app.Activity;
import android.app.AlertDialog;
import android.content.ClipData;
import android.content.ClipboardManager;
import android.content.res.Resources;
import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.graphics.Color;
import android.graphics.Canvas;
import android.graphics.Paint;
import android.text.TextPaint;
import android.graphics.Typeface;
import android.graphics.drawable.BitmapDrawable;
import android.graphics.drawable.GradientDrawable;
import android.graphics.drawable.LayerDrawable;
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
import android.widget.SeekBar;
import android.widget.LinearLayout;
import android.widget.ScrollView;
import android.view.MotionEvent;
import android.view.ViewGroup;
import java.io.InputStream;
import java.text.Normalizer;
import java.util.LinkedHashMap;
import java.util.Map;
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

    // --- paleta ---
    private static final int PANEL      = 0xFF3F5297;
    private static final int PANEL_DARK = 0xFF2C3A6B;
    private static final int PANEL_LIT  = 0xFF5468B4;
    private static final int OUTLINE    = 0xFF131625;
    private static final int GRASS      = 0xFF22A851;
    private static final int GRASS_LIT  = 0xFF2EED52;
    private static final int DIRT       = 0xFF875F41;
    private static final int INK        = 0xFFFFFFFF;
    private static final int INK_DIM    = 0xFFC3CBEA;
    private static final int SCRIM      = 0xC0000000;

    private static Activity sActivity;
    private static View sOverlay;

    // ------------------------------ secoes ------------------------------

    /** Uma secao do menu. `npc` troca "dar item" por "invocar". */
    private static final class Section {
        final String group, title, subtitle;
        String[] names;
        int[] ids;
        final int[] stacks;   // null quando e NPC
        final boolean npc;
        /** Catalogo inteiro: os nomes vem do jogo, e o id E o indice. */
        final boolean todos;
        /** Sprite do jogo que anuncia a secao, em res/drawable. */
        final String icone;

        Section(String group, String title, String subtitle,
                String[] names, int[] ids, int[] stacks, boolean npc, String icone) {
            this(group, title, subtitle, names, ids, stacks, npc, false, icone);
        }

        Section(String group, String title, String subtitle,
                String[] names, int[] ids, int[] stacks, boolean npc, boolean todos,
                String icone) {
            this.group = group; this.title = title; this.subtitle = subtitle;
            this.names = names; this.ids = ids; this.stacks = stacks;
            this.npc = npc; this.todos = todos; this.icone = icone;
        }

        /** Nomes sem acento e em minuscula, calculados UMA vez. */
        String[] busca;
        /** Indices visiveis depois do filtro. null = todos. */
        int[] filtro;

        int total() { return names == null ? 0 : names.length; }
        int count() { return filtro != null ? filtro.length : total(); }
        int indiceDe(int posicao) { return filtro != null ? filtro[posicao] : posicao; }

        /**
         * Monta o indice de busca. Uma passada por 6147 nomes, na primeira vez
         * que alguem digita — normalizar a cada tecla seria refazer isso a cada
         * letra.
         */
        void prepararBusca() {
            if (busca != null) return;
            busca = new String[total()];
            for (int i = 0; i < busca.length; i++) busca[i] = semAcento(names[i]);
        }

        /**
         * Filtra por nome OU por id, e poe o que COMECA com o termo na frente.
         *
         * Procurar "espada" com 6147 itens devolve dezenas; quem digitou quer
         * "Espada Larga" antes de "Suporte de Espada". Duas passadas resolvem,
         * sem ordenar nada.
         */
        void filtrar(String termo) {
            String t = semAcento(termo).trim();
            if (t.length() == 0) { filtro = null; return; }
            prepararBusca();

            int[] achados = new int[total()];
            int n = 0;
            for (int i = 0; i < busca.length; i++) {
                if (busca[i].startsWith(t) || String.valueOf(ids[i]).equals(t)) achados[n++] = i;
            }
            for (int i = 0; i < busca.length; i++) {
                if (!busca[i].startsWith(t) && busca[i].contains(t) && t.length() > 1) {
                    achados[n++] = i;
                }
            }
            int[] fim = new int[n];
            System.arraycopy(achados, 0, fim, 0, n);
            filtro = fim;
        }

        /**
         * Puxa a tabela de nomes do nativo, uma vez.
         *
         * Pode nao estar pronta: sao ~6800 nomes moidos em fatias, algumas
         * centenas por quadro, para nao engasgar o jogo. Ate la devolve false e
         * a tela diz isso, em vez de mostrar uma lista vazia sem explicacao.
         */
        boolean carregar() {
            if (names != null) return true;
            String[] n = npc ? nNpcNames() : nItemNames();
            if (n == null) return false;
            names = n;
            ids = new int[n.length];
            for (int i = 0; i < n.length; i++) ids[i] = i;
            return true;
        }
    }

    /** Minuscula e sem acento: "Poção" acha com "pocao". */
    private static String semAcento(String s) {
        if (s == null) return "";
        String d = Normalizer.normalize(s, Normalizer.Form.NFD);
        StringBuilder b = new StringBuilder(d.length());
        for (int i = 0; i < d.length(); i++) {
            char c = d.charAt(i);
            if (Character.getType(c) != Character.NON_SPACING_MARK) b.append(c);
        }
        return b.toString().toLowerCase();
    }

    private static Section[] sections() {
        return new Section[] {
            new Section("ITENS", "Corpo a corpo", "Espadas, lancas e tudo que bate de perto.",
                CheatData.CORPO_A_CORPO_N, CheatData.CORPO_A_CORPO_I, CheatData.CORPO_A_CORPO_S, false, "ic_sec_melee"),
            new Section("ITENS", "A distancia", "Armas de fogo e arcos. Levam municao junto.",
                CheatData.A_DISTANCIA_N, CheatData.A_DISTANCIA_I, CheatData.A_DISTANCIA_S, false, "ic_sec_ranged"),
            new Section("ITENS", "Magia", "Cajados, livros e armas de mana.",
                CheatData.MAGIA_N, CheatData.MAGIA_I, CheatData.MAGIA_S, false, "ic_sec_magia"),
            new Section("ITENS", "Invocacao", "Cajados que chamam servos para lutar por voce.",
                CheatData.INVOCACAO_N, CheatData.INVOCACAO_I, CheatData.INVOCACAO_S, false, "ic_sec_invoc"),
            new Section("ITENS", "Acessorios", "Botas, asas, escudos e emblemas.",
                CheatData.ACESSORIOS_N, CheatData.ACESSORIOS_I, CheatData.ACESSORIOS_S, false, "ic_sec_acess"),
            new Section("ITENS", "Blocos e moveis", "Material de construcao e estacoes de trabalho.",
                CheatData.BLOCOS_E_MOVEIS_N, CheatData.BLOCOS_E_MOVEIS_I, CheatData.BLOCOS_E_MOVEIS_S, false, "ic_sec_blocos"),
            new Section("ITENS", "Uteis", "Cristais, pocoes e municao infinita.",
                CheatData.UTEIS_N, CheatData.UTEIS_I, CheatData.UTEIS_S, false, "ic_sec_util"),
            new Section("MUNDO", "Chefes", "Invoca o chefe ao seu lado. Prepare-se antes.",
                CheatData.CHEFES_N, CheatData.CHEFES_I, null, true, "ic_sec_chefe"),
            new Section("MUNDO", "Monstros", "Inimigos comuns, para testar arma nova.",
                CheatData.MONSTROS_N, CheatData.MONSTROS_I, null, true, "ic_sec_monstro"),
            new Section("MUNDO", "Moradores", "Traz um NPC da cidade. Ele ainda precisa de casa.",
                CheatData.MORADORES_N, CheatData.MORADORES_I, null, true, "ic_sec_morador"),
            new Section("TUDO", "Todos os itens", "Os 6147 ids do jogo, com o nome no seu idioma.",
                null, null, null, false, true, "ic_sec_tudo_item"),
            new Section("TUDO", "Todos os NPCs", "Os 697 ids do jogo, chefe e bicho incluidos.",
                null, null, null, true, true, "ic_sec_tudo_npc"),
        };
    }

    // ------------------------------ desenho ------------------------------

    /**
     * O menu estava na metade da escala do jogo.
     *
     * Medido na mesma tela de 1600x900: o MENOR texto que o Terraria desenha (a
     * versao no rodape) tem 25 px de altura, e o nome de item aqui tinha 13. Ou
     * seja, o texto de leitura do menu era menor que qualquer coisa que o jogo
     * mostra — daí a sensacao de miniatura.
     *
     * Dois fatores, e nao um: o TEXTO precisa quase dobrar para alcancar o
     * jogo, mas dobrar tambem as medidas jogaria a coluna da esquerda para 40%
     * da largura da tela. As caixas crescem o suficiente para caber o texto
     * maior, e so.
     */
    private static final float ESCALA_TEXTO = 1.9f;
    private static final float ESCALA_DIM = 1.3f;

    private static int px(Activity a, float dp) {
        return (int) TypedValue.applyDimension(
            TypedValue.COMPLEX_UNIT_DIP, dp * ESCALA_DIM, a.getResources().getDisplayMetrics());
    }

    /** Painel do Terraria: contorno escuro, corpo azul, luz em cima. */
    /** Marca do item escolhido na coluna: faixa clara, sem moldura preta. */
    private static GradientDrawable destaque(Activity a) {
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
     * so o subtitulo, que nao precisa.
     */
    private static final class Contornado extends TextView {
        private final float traco;

        Contornado(Activity a) {
            super(a);
            traco = px(a, 2);
            // O TextView mede o texto em FILL; o contorno e STROKE e passa
            // metade da espessura para FORA do glifo, nos quatro lados. Sem
            // esta folga a ultima letra e as descidas (g, p, q) saiam raspadas.
            int folga = (int) Math.ceil(traco / 2f) + 1;
            setPadding(folga, folga, folga, folga);
            setIncludeFontPadding(true);
        }

        @Override protected void onDraw(Canvas c) {
            if (getLineCount() != 1) { super.onDraw(c); return; }
            final String txt = getText().toString();
            final TextPaint pt = getPaint();
            final int cor = getCurrentTextColor();
            final float x = getPaddingLeft();
            final float y = getBaseline();

            pt.setStyle(Paint.Style.STROKE);
            pt.setStrokeWidth(traco);
            pt.setStrokeJoin(Paint.Join.ROUND);
            pt.setColor(0xFF000000);
            c.drawText(txt, x, y, pt);

            pt.setStyle(Paint.Style.FILL);
            pt.setColor(cor);
            c.drawText(txt, x, y, pt);
        }
    }

    private static TextView text(Activity a, String s, int size, int color) {
        TextView t = new Contornado(a);
        t.setText(s);
        t.setTextSize(size * ESCALA_TEXTO);
        t.setTextColor(color);
        t.setTypeface(fonte(a));
        return t;
    }

    /**
     * A fonte do Terraria, a mesma do launcher.
     *
     * Vem dos assets e nao de res/font: este menu roda de um dex carregado em
     * memoria, sem a classe R do app, e `Resources.getFont` so existe da API 26
     * para cima enquanto o app vai ate a 24.
     */
    private static Typeface sFonte;

    private static Typeface fonte(Activity a) {
        if (sFonte == null) {
            try {
                sFonte = Typeface.createFromAsset(a.getAssets(), "fonte/bunny.ttf");
            } catch (Throwable t) {
                sFonte = Typeface.DEFAULT;
            }
        }
        return sFonte;
    }

    /** Sprite do res/drawable do app — o jogo roda no NOSSO processo. */
    private static Bitmap sprite(Activity a, String name) {
        try {
            Resources r = a.getResources();
            int id = r.getIdentifier(name, "drawable", a.getPackageName());
            if (id == 0) return null;
            BitmapFactory.Options o = new BitmapFactory.Options();
            o.inScaled = false;   // pixel art nao escala no decode
            return BitmapFactory.decodeResource(r, id, o);
        } catch (Throwable t) {
            return null;
        }
    }

    /**
     * Sprite do jogo por ID, de assets/sprites/{item,npc}/<id>.png.
     *
     * Vem de arquivo porque nao da para vir do jogo: as texturas dele estao so
     * na GPU (medido — o atlas e 2048x2048 com isReadable = 0, e Blit/
     * ReadPixels/GetNativeTexturePtr sairam deste binario no strip do IL2CPP).
     *
     * O cache e pequeno de proposito: a lista recicla, entao o que importa e
     * nao redecodificar o que esta na tela agora. Segurar 6000 bitmaps seria
     * trocar um engasgo por um estouro de memoria.
     */
    private static final int CACHE_SPRITES = 192;
    private static final Map<String, Bitmap> sCache =
        new LinkedHashMap<String, Bitmap>(64, 0.75f, true) {
            @Override protected boolean removeEldestEntry(Map.Entry<String, Bitmap> e) {
                return size() > CACHE_SPRITES;
            }
        };

    private static int[] sNpcFrames;

    /** Quadros da tira do NPC. 1 quando o jogo ainda nao disse. */
    private static int framesDe(int id) {
        if (sNpcFrames == null) sNpcFrames = nNpcFrames();
        if (sNpcFrames == null || id < 0 || id >= sNpcFrames.length) return 1;
        return sNpcFrames[id] < 1 ? 1 : sNpcFrames[id];
    }

    private static Bitmap spriteById(Activity a, boolean npc, int id) {
        final String caminho = "sprites/" + (npc ? "npc/" : "item/") + id + ".png";
        if (sCache.containsKey(caminho)) return sCache.get(caminho);
        Bitmap bmp = null;
        InputStream in = null;
        try {
            in = a.getAssets().open(caminho);
            BitmapFactory.Options o = new BitmapFactory.Options();
            o.inScaled = false;   // pixel art nao escala no decode
            bmp = BitmapFactory.decodeStream(in, null, o);
            // O PNG de NPC e uma TIRA VERTICAL de quadros. Mostrar a tira
            // inteira espremida num quadradinho deixava a Geleia Azul com duas
            // cabecas. Fica so o primeiro quadro.
            if (bmp != null && npc) {
                int q = framesDe(id);
                int alt = bmp.getHeight() / q;
                if (q > 1 && alt > 0) {
                    Bitmap corte = Bitmap.createBitmap(bmp, 0, 0, bmp.getWidth(), alt);
                    if (corte != bmp) bmp.recycle();
                    bmp = corte;
                }
            }
        } catch (Throwable t) {
            bmp = null;           // id sem sprite: a linha fica so com o nome
        } finally {
            if (in != null) try { in.close(); } catch (Throwable ignored) { }
        }
        sCache.put(caminho, bmp);
        return bmp;
    }

    private static ImageView icon(Activity a, Bitmap bmp, int dp) {
        ImageView v = new ImageView(a);
        if (bmp != null) {
            BitmapDrawable d = new BitmapDrawable(a.getResources(), bmp);
            d.getPaint().setFilterBitmap(false);  // vizinho-mais-proximo
            v.setImageDrawable(d);
        }
        v.setLayoutParams(new LinearLayout.LayoutParams(px(a, dp), px(a, dp)));
        return v;
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

    /** Botao flutuante que abre o menu. */
    private static void buildToggle(final Activity act) {
        ImageView b = icon(act, sprite(act, "ic_bunny_head"), 40);
        b.setPadding(px(act, 6), px(act, 6), px(act, 6), px(act, 6));
        b.setBackground(panel(act, PANEL, OUTLINE));
        b.setOnClickListener(new View.OnClickListener() {
            @Override public void onClick(View v) { toggleMenu(act); }
        });
        FrameLayout.LayoutParams lp = new FrameLayout.LayoutParams(
            px(act, 52), px(act, 52));
        lp.gravity = Gravity.TOP | Gravity.START;
        lp.leftMargin = px(act, 16);
        lp.topMargin = px(act, 56);
        act.addContentView(b, lp);
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
        final Section[] all = sections();

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
        blp.setMargins(px(act, 16), px(act, 16), px(act, 16), px(act, 16));
        body.setLayoutParams(blp);
        root.addView(body);

        // ---- coluna da direita (criada antes: o aside precisa preenche-la) ----
        final LinearLayout content = new LinearLayout(act);
        content.setOrientation(LinearLayout.VERTICAL);
        content.setBackground(panelBig(act, PANEL, OUTLINE));
        content.setPadding(px(act, 12), px(act, 12), px(act, 12), px(act, 12));

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
        aside.addView(rule(act));

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
            b.setPadding(px(act, 8), px(act, 8), px(act, 10), px(act, 8));
            b.addView(icon(act, sprite(act, s.icone), 22));
            TextView rot = text(act, s.title, 14, INK);
            rot.setPadding(px(act, 8), 0, 0, 0);
            b.addView(rot);
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
            px(act, 200), LinearLayout.LayoutParams.MATCH_PARENT);
        alp.rightMargin = px(act, 10);
        body.addView(aside, alp);
        body.addView(content, new LinearLayout.LayoutParams(
            0, LinearLayout.LayoutParams.MATCH_PARENT, 1f));

        // Fechar: icone no canto superior direito, sobre tudo. Era uma barra
        // vermelha no pe da coluna, que comia altura de lista e ficava longe do
        // polegar de quem segura o aparelho deitado.
        ImageView fechar = icon(act, sprite(act, "ic_fechar"), 34);
        fechar.setPadding(px(act, 4), px(act, 4), px(act, 4), px(act, 4));
        fechar.setOnClickListener(new View.OnClickListener() {
            @Override public void onClick(View v) {
                if (sOverlay != null) sOverlay.setVisibility(View.GONE);
            }
        });
        FrameLayout.LayoutParams flp = new FrameLayout.LayoutParams(px(act, 42), px(act, 42));
        flp.gravity = Gravity.TOP | Gravity.END;
        flp.topMargin = px(act, 24);
        flp.rightMargin = px(act, 24);
        root.addView(fechar, flp);

        select(act, content, all, buttons, 0);
        return root;
    }

    private static View rule(Activity act) {
        View v = new View(act);
        v.setBackgroundColor(OUTLINE);
        LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(
            LinearLayout.LayoutParams.MATCH_PARENT, px(act, 2));
        lp.topMargin = px(act, 8);
        lp.bottomMargin = px(act, 4);
        v.setLayoutParams(lp);
        return v;
    }

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
        private final Paint tinta = new Paint(Paint.ANTI_ALIAS_FLAG);
        private final float borda, alca;
        private int valor;
        Runnable aoMudar;

        Range(Activity a, int max, int inicial) {
            super(a);
            this.max = max < 1 ? 1 : max;
            this.valor = inicial;
            this.borda = px(a, 2);
            this.alca = px(a, 15);
            setPadding(0, px(a, 7), 0, px(a, 7));
        }

        int valor() { return valor; }

        @Override protected void onMeasure(int wSpec, int hSpec) {
            setMeasuredDimension(resolveSize(px((Activity) getContext(), 200), wSpec),
                                 resolveSize((int) (alca + px((Activity) getContext(), 12)), hSpec));
        }

        @Override protected void onDraw(Canvas c) {
            final float meio = getHeight() / 2f;
            final float sulco = px((Activity) getContext(), 12);
            final float x0 = alca / 2f, x1 = getWidth() - alca / 2f;
            final float topo = meio - sulco / 2f, base = meio + sulco / 2f;
            final float raio = sulco / 2f;

            tinta.setStyle(Paint.Style.FILL);
            tinta.setColor(OUTLINE);
            c.drawRoundRect(x0 - borda, topo - borda, x1 + borda, base + borda, raio, raio, tinta);
            tinta.setColor(PANEL_DARK);
            c.drawRoundRect(x0, topo, x1, base, raio, raio, tinta);

            final float t = (valor - 1) / (float) (max - 1 == 0 ? 1 : max - 1);
            final float cx = x0 + (x1 - x0) * t;
            if (cx > x0) {
                tinta.setColor(GRASS);
                c.drawRoundRect(x0, topo, cx, base, raio, raio, tinta);
            }

            // A alca e MAIS ALTA que o sulco e e BRANCA: dentro do verde do
            // preenchido, uma alca verde sumia — virava uma listra e ninguem
            // via onde pegar.
            final float ah = alca * 1.7f;
            final float ax = cx - alca / 2f, ay = meio - ah / 2f;
            final float ar = px((Activity) getContext(), 3);
            tinta.setColor(OUTLINE);
            c.drawRoundRect(ax, ay, ax + alca, ay + ah, ar, ar, tinta);
            tinta.setColor(0xFFFFFFFF);
            c.drawRoundRect(ax + borda, ay + borda, ax + alca - borda, ay + ah - borda,
                            ar, ar, tinta);
            // Meia sombra embaixo: e assim que o jogo da volume a um botao.
            tinta.setColor(0xFFB9C0D4);
            c.drawRect(ax + borda, meio + ah / 6f, ax + alca - borda, ay + ah - borda, tinta);
        }

        @Override public boolean onTouchEvent(MotionEvent e) {
            switch (e.getAction()) {
                case MotionEvent.ACTION_DOWN:
                case MotionEvent.ACTION_MOVE:
                case MotionEvent.ACTION_UP:
                    final float x0 = alca / 2f, x1 = getWidth() - alca / 2f;
                    float t = (e.getX() - x0) / Math.max(1f, x1 - x0);
                    if (t < 0) t = 0; else if (t > 1) t = 1;
                    int novo = 1 + Math.round(t * (max - 1));
                    if (novo != valor) {
                        valor = novo;
                        invalidate();
                        if (aoMudar != null) aoMudar.run();
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

    /** O valor que mais se repete — para o slider comecar onde era o padrao. */
    private static int maisComum(int[] v) {
        if (v == null || v.length == 0) return 1;
        int melhor = v[0], melhorN = 0;
        for (int a : v) {
            int n = 0;
            for (int b : v) if (a == b) n++;
            if (n > melhorN) { melhorN = n; melhor = a; }
        }
        return melhor < 1 ? 1 : melhor;
    }

    /** Troca a secao mostrada e marca o botao escolhido. */
    private static void select(final Activity act, LinearLayout content,
                               Section[] all, View[] buttons, int index) {
        for (int i = 0; i < buttons.length; i++) {
            // So o escolhido tem fundo. Antes cada item da coluna era um
            // retangulo azul-escuro com contorno preto, e onze deles empilhados
            // viravam uma parede de caixas — o que se via era a moldura, nao a
            // lista.
            buttons[i].setBackground(i == index ? destaque(act) : null);
        }
        final Section s = all[index];
        s.filtro = null;   // filtro e da visita, nao da secao
        content.removeAllViews();

        LinearLayout head = new LinearLayout(act);
        head.setOrientation(LinearLayout.VERTICAL);
        head.addView(text(act, s.title, 19, INK));
        head.addView(text(act, s.subtitle, 11, INK_DIM));
        content.addView(head);

        if (!s.carregar()) {
            // A primeira pergunta e o gatilho: o nativo so comeca a moer quando
            // alguem pede. Em vez de mandar o jogador tocar de novo, voltamos
            // sozinhos ate ficar pronto.
            content.addView(text(act,
                "Lendo os nomes do jogo, no seu idioma. Sao ~6800, moidos aos "
                + "poucos para nao engasgar o jogo...", 13, INK_DIM));
            final LinearLayout alvo = content;
            final Section[] todas = all;
            final View[] bts = buttons;
            final int idx = index;
            content.postDelayed(new Runnable() {
                @Override public void run() { select(act, alvo, todas, bts, idx); }
            }, 600);
            return;
        }

        // ---- quantidade ----
        final int max = s.npc ? MAX_NPC : MAX_ITEM;
        int inicial = s.npc ? 1 : maisComum(s.stacks);
        if (inicial > max) inicial = max;
        final int[] qtd = { inicial };

        final TextView rotulo = text(act, "Quantidade: " + inicial, 12, INK);
        rotulo.setWidth(px(act, 118));
        final Range barra = new Range(act, max, inicial);
        barra.aoMudar = new Runnable() {
            @Override public void run() {
                qtd[0] = barra.valor();
                rotulo.setText("Quantidade: " + qtd[0]);
            }
        };

        LinearLayout linhaQtd = new LinearLayout(act);
        linhaQtd.setOrientation(LinearLayout.HORIZONTAL);
        linhaQtd.setGravity(Gravity.CENTER_VERTICAL);
        linhaQtd.setPadding(0, px(act, 4), 0, px(act, 4));
        linhaQtd.addView(rotulo);
        linhaQtd.addView(barra, new LinearLayout.LayoutParams(
            0, LinearLayout.LayoutParams.WRAP_CONTENT, 1f));
        content.addView(linhaQtd);

        // ---- busca ----
        LinearLayout linhaBusca = new LinearLayout(act);
        linhaBusca.setOrientation(LinearLayout.HORIZONTAL);
        linhaBusca.setGravity(Gravity.CENTER_VERTICAL);
        linhaBusca.setBackground(panel(act, PANEL_DARK, OUTLINE));
        linhaBusca.setPadding(px(act, 8), px(act, 2), px(act, 8), px(act, 2));
        linhaBusca.addView(icon(act, sprite(act, "ic_lupa"), 20));

        final EditText campo = new EditText(act);
        campo.setSingleLine(true);
        campo.setBackground(null);
        campo.setTextColor(INK);
        campo.setHintTextColor(INK_DIM);
        campo.setHint(s.npc ? "Buscar NPC por nome ou id" : "Buscar item por nome ou id");
        campo.setTextSize(13 * ESCALA_TEXTO);
        campo.setTypeface(fonte(act));
        campo.setPadding(px(act, 8), px(act, 6), 0, px(act, 6));
        linhaBusca.addView(campo, new LinearLayout.LayoutParams(
            0, LinearLayout.LayoutParams.WRAP_CONTENT, 1f));

        LinearLayout.LayoutParams lbp = new LinearLayout.LayoutParams(
            LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.WRAP_CONTENT);
        lbp.topMargin = px(act, 4);
        lbp.bottomMargin = px(act, 6);
        content.addView(linhaBusca, lbp);

        // ---- lista ----
        //
        // ListView, e nao ScrollView com tudo dentro: "Todos os itens" tem 6147
        // linhas, e montar 6147 Views de uma vez trava o jogo por segundos. A
        // ListView so monta o que cabe na tela e reaproveita ao rolar.
        ListView lista = new ListView(act);
        // Uma linha quase invisivel entre as linhas, no lugar da borda preta
        // arredondada que cada card tinha: com 6147 deles, a tela virava uma
        // grade de caixinhas em vez de uma lista.
        lista.setDivider(new android.graphics.drawable.ColorDrawable(0x22FFFFFF));
        lista.setDividerHeight(px(act, 1));
        lista.setCacheColorHint(0);
        final BaseAdapter adaptador = new BaseAdapter() {
            @Override public int getCount() { return s.count(); }
            @Override public Object getItem(int i) { return null; }
            @Override public long getItemId(int i) { return i; }
            @Override public View getView(int i, View reuso, ViewGroup pai) {
                return row(act, s, s.indiceDe(i), qtd, reuso);
            }
        };
        lista.setAdapter(adaptador);
        campo.addTextChangedListener(new TextWatcher() {
            @Override public void onTextChanged(CharSequence t, int a1, int b1, int c1) {
                s.filtrar(t.toString());
                adaptador.notifyDataSetChanged();
                lista.setSelection(0);
            }
            @Override public void beforeTextChanged(CharSequence t, int a1, int b1, int c1) { }
            @Override public void afterTextChanged(Editable e) { }
        });
        content.addView(lista, new LinearLayout.LayoutParams(
            LinearLayout.LayoutParams.MATCH_PARENT, 0, 1f));
    }

    /** As partes de uma linha, para trocar o conteudo em vez de remontar. */
    private static final class Linha {
        ImageView sprite;
        TextView nome, detalhe;
        ImageView acao;
    }

    /** Uma linha: sprite, nome, id e o botao de acao. */
    private static View row(final Activity act, final Section s, final int i,
                            final int[] qtd, View reuso) {
        final Linha L;
        LinearLayout r;
        if (reuso instanceof LinearLayout && reuso.getTag() instanceof Linha) {
            r = (LinearLayout) reuso;
            L = (Linha) reuso.getTag();
        } else {
            L = new Linha();
            r = new LinearLayout(act);
            r.setOrientation(LinearLayout.HORIZONTAL);
            r.setGravity(Gravity.CENTER_VERTICAL);
            r.setPadding(px(act, 8), px(act, 7), px(act, 8), px(act, 7));

            L.sprite = icon(act, null, 34);
            L.sprite.setScaleType(ImageView.ScaleType.FIT_CENTER);
            r.addView(L.sprite);

            // Nome e id na MESMA linha: empilhados, cada linha da lista gastava
            // uma altura de texto a mais, e na escala do jogo isso e uma linha
            // inteira de lista a menos por tela.
            LinearLayout rotulos = new LinearLayout(act);
            rotulos.setOrientation(LinearLayout.HORIZONTAL);
            rotulos.setGravity(Gravity.BOTTOM);
            rotulos.setPadding(px(act, 8), 0, 0, 0);
            L.nome = text(act, "", 14, INK);
            L.detalhe = text(act, "", 10, INK_DIM);
            L.detalhe.setPadding(px(act, 8), 0, 0, px(act, 2));
            rotulos.addView(L.nome);
            rotulos.addView(L.detalhe);
            r.addView(rotulos, new LinearLayout.LayoutParams(
                0, LinearLayout.LayoutParams.WRAP_CONTENT, 1f));

            // So o sinal, sem palavra: a acao ja esta dita pela secao, e o
            // texto roubava metade da largura de cada linha.
            // So o sinal, sem caixa nenhuma atras: a moldura verde competia
            // com o sprite do item na mesma linha.
            L.acao = new ImageView(act);
            L.acao.setScaleType(ImageView.ScaleType.FIT_CENTER);
            L.acao.setPadding(px(act, 4), px(act, 4), px(act, 4), px(act, 4));
            r.addView(L.acao, new LinearLayout.LayoutParams(px(act, 40), px(act, 34)));

            r.setLayoutParams(new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT,
                LinearLayout.LayoutParams.WRAP_CONTENT));
            r.setTag(L);
        }

        final int id = s.ids[i];
        final String bruto = s.names[i];
        // Id sem nome na Localization (buraco na tabela): mostra o id, que e o
        // que o jogador precisa para saber o que pediu.
        final String nome = (bruto == null || bruto.length() == 0) ? ("#" + id) : bruto;

        Bitmap bmp = spriteById(act, s.npc, id);
        if (bmp != null) {
            BitmapDrawable d = new BitmapDrawable(act.getResources(), bmp);
            d.getPaint().setFilterBitmap(false);   // vizinho-mais-proximo
            L.sprite.setImageDrawable(d);
        } else {
            L.sprite.setImageDrawable(null);
        }

        L.nome.setText(nome);
        L.detalhe.setText(s.npc ? ("NPC " + id) : ("id " + id));
        L.acao.setContentDescription(s.npc ? "Invocar" : "Pegar");
        Bitmap bAcao = sprite(act, s.npc ? "ic_invocar" : "ic_pegar");
        if (bAcao != null) {
            BitmapDrawable dAcao = new BitmapDrawable(act.getResources(), bAcao);
            dAcao.getPaint().setFilterBitmap(false);
            L.acao.setImageDrawable(dAcao);
        }
        L.acao.setOnClickListener(new View.OnClickListener() {
            @Override public void onClick(View v) {
                int n = qtd[0] < 1 ? 1 : qtd[0];
                if (s.npc) {
                    // A contagem vai JUNTO: o nativo guarda o pedido num slot
                    // so, consumido uma vez por quadro, entao dez chamadas
                    // seguidas viravam um NPC.
                    nOnSpawn(id, n);
                    Toast.makeText(act, nome + (n > 1 ? " x" + n : "") + " invocado",
                        Toast.LENGTH_SHORT).show();
                } else {
                    nOnGive(id, n);
                    Toast.makeText(act, nome + (n > 1 ? " x" + n : ""),
                        Toast.LENGTH_SHORT).show();
                }
            }
        });
        return r;
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
        Button copiar = dlg.getButton(AlertDialog.BUTTON_NEUTRAL);
        if (copiar == null) return;
        copiar.setOnClickListener(new View.OnClickListener() {
            @Override public void onClick(View v) {
                ClipboardManager cm =
                    (ClipboardManager) act.getSystemService(Activity.CLIPBOARD_SERVICE);
                if (cm != null) cm.setPrimaryClip(ClipData.newPlainText("bunny", body));
                Toast.makeText(act, "Log copiado", Toast.LENGTH_SHORT).show();
            }
        });
    }
}
