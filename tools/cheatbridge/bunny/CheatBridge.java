package bunny;

import android.app.Activity;
import android.app.AlertDialog;
import android.content.ClipData;
import android.content.ClipboardManager;
import android.content.res.Resources;
import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.graphics.Color;
import android.graphics.drawable.BitmapDrawable;
import android.graphics.drawable.GradientDrawable;
import android.graphics.drawable.LayerDrawable;
import android.util.TypedValue;
import android.view.Gravity;
import android.view.View;
import android.widget.Button;
import android.widget.FrameLayout;
import android.widget.ImageView;
import android.widget.LinearLayout;
import android.widget.ScrollView;
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
    public static native void nOnSpawn(int type);

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
        final String[] names;
        final int[] ids;
        final int[] stacks;   // null quando e NPC
        final boolean npc;

        Section(String group, String title, String subtitle,
                String[] names, int[] ids, int[] stacks, boolean npc) {
            this.group = group; this.title = title; this.subtitle = subtitle;
            this.names = names; this.ids = ids; this.stacks = stacks; this.npc = npc;
        }
    }

    private static Section[] sections() {
        return new Section[] {
            new Section("ITENS", "Corpo a corpo", "Espadas, lancas e tudo que bate de perto.",
                CheatData.CORPO_A_CORPO_N, CheatData.CORPO_A_CORPO_I, CheatData.CORPO_A_CORPO_S, false),
            new Section("ITENS", "A distancia", "Armas de fogo e arcos. Levam municao junto.",
                CheatData.A_DISTANCIA_N, CheatData.A_DISTANCIA_I, CheatData.A_DISTANCIA_S, false),
            new Section("ITENS", "Magia", "Cajados, livros e armas de mana.",
                CheatData.MAGIA_N, CheatData.MAGIA_I, CheatData.MAGIA_S, false),
            new Section("ITENS", "Invocacao", "Cajados que chamam servos para lutar por voce.",
                CheatData.INVOCACAO_N, CheatData.INVOCACAO_I, CheatData.INVOCACAO_S, false),
            new Section("ITENS", "Acessorios", "Botas, asas, escudos e emblemas.",
                CheatData.ACESSORIOS_N, CheatData.ACESSORIOS_I, CheatData.ACESSORIOS_S, false),
            new Section("ITENS", "Blocos e moveis", "Material de construcao e estacoes de trabalho.",
                CheatData.BLOCOS_E_MOVEIS_N, CheatData.BLOCOS_E_MOVEIS_I, CheatData.BLOCOS_E_MOVEIS_S, false),
            new Section("ITENS", "Uteis", "Cristais, pocoes e municao infinita.",
                CheatData.UTEIS_N, CheatData.UTEIS_I, CheatData.UTEIS_S, false),
            new Section("MUNDO", "Chefes", "Invoca o chefe ao seu lado. Prepare-se antes.",
                CheatData.CHEFES_N, CheatData.CHEFES_I, null, true),
            new Section("MUNDO", "Monstros", "Inimigos comuns, para testar arma nova.",
                CheatData.MONSTROS_N, CheatData.MONSTROS_I, null, true),
            new Section("MUNDO", "Moradores", "Traz um NPC da cidade. Ele ainda precisa de casa.",
                CheatData.MORADORES_N, CheatData.MORADORES_I, null, true),
        };
    }

    // ------------------------------ desenho ------------------------------

    private static int px(Activity a, float dp) {
        return (int) TypedValue.applyDimension(
            TypedValue.COMPLEX_UNIT_DIP, dp, a.getResources().getDisplayMetrics());
    }

    /** Painel do Terraria: contorno escuro, corpo azul, luz em cima. */
    private static GradientDrawable panel(Activity a, int fill, int stroke) {
        GradientDrawable d = new GradientDrawable();
        d.setColor(fill);
        d.setStroke(px(a, 2), stroke);
        d.setCornerRadius(px(a, 3));
        return d;
    }

    private static TextView text(Activity a, String s, int size, int color) {
        TextView t = new TextView(a);
        t.setText(s);
        t.setTextSize(size);
        t.setTextColor(color);
        return t;
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
        content.setBackground(panel(act, PANEL, OUTLINE));
        content.setPadding(px(act, 12), px(act, 12), px(act, 12), px(act, 12));

        // ---- coluna da esquerda ----
        LinearLayout aside = new LinearLayout(act);
        aside.setOrientation(LinearLayout.VERTICAL);
        aside.setBackground(panel(act, PANEL, OUTLINE));
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
            TextView b = text(act, s.title, 14, INK);
            b.setPadding(px(act, 10), px(act, 9), px(act, 10), px(act, 9));
            b.setOnClickListener(new View.OnClickListener() {
                @Override public void onClick(View v) {
                    select(act, content, all, buttons, index);
                }
            });
            LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT,
                LinearLayout.LayoutParams.WRAP_CONTENT);
            lp.bottomMargin = px(act, 3);
            asideList.addView(b, lp);
            buttons[i] = b;
        }

        TextView close = text(act, "Fechar", 14, INK);
        close.setGravity(Gravity.CENTER);
        close.setPadding(0, px(act, 10), 0, px(act, 10));
        close.setBackground(panel(act, 0xFF8B3A3A, OUTLINE));
        close.setOnClickListener(new View.OnClickListener() {
            @Override public void onClick(View v) {
                if (sOverlay != null) sOverlay.setVisibility(View.GONE);
            }
        });
        LinearLayout.LayoutParams clp = new LinearLayout.LayoutParams(
            LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.WRAP_CONTENT);
        clp.topMargin = px(act, 8);
        aside.addView(close, clp);

        LinearLayout.LayoutParams alp = new LinearLayout.LayoutParams(
            px(act, 190), LinearLayout.LayoutParams.MATCH_PARENT);
        alp.rightMargin = px(act, 10);
        body.addView(aside, alp);
        body.addView(content, new LinearLayout.LayoutParams(
            0, LinearLayout.LayoutParams.MATCH_PARENT, 1f));

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

    /** Troca a secao mostrada e marca o botao escolhido. */
    private static void select(final Activity act, LinearLayout content,
                               Section[] all, View[] buttons, int index) {
        for (int i = 0; i < buttons.length; i++) {
            buttons[i].setBackground(
                i == index ? panel(act, PANEL_LIT, GRASS) : panel(act, PANEL_DARK, OUTLINE));
        }
        Section s = all[index];
        content.removeAllViews();

        LinearLayout head = new LinearLayout(act);
        head.setOrientation(LinearLayout.VERTICAL);
        head.addView(text(act, s.title, 19, INK));
        head.addView(text(act, s.subtitle, 11, INK_DIM));
        content.addView(head);
        content.addView(rule(act));

        ScrollView scroll = new ScrollView(act);
        LinearLayout list = new LinearLayout(act);
        list.setOrientation(LinearLayout.VERTICAL);
        for (int i = 0; i < s.names.length; i++) {
            list.addView(row(act, s, i));
        }
        scroll.addView(list);
        content.addView(scroll, new LinearLayout.LayoutParams(
            LinearLayout.LayoutParams.MATCH_PARENT, 0, 1f));
    }

    /** Uma linha: nome, quantidade e o botao de acao. */
    private static View row(final Activity act, final Section s, final int i) {
        final String name = s.names[i];
        final int id = s.ids[i];
        final int stack = s.stacks == null ? 1 : s.stacks[i];

        LinearLayout r = new LinearLayout(act);
        r.setOrientation(LinearLayout.HORIZONTAL);
        r.setGravity(Gravity.CENTER_VERTICAL);
        r.setBackground(panel(act, PANEL_DARK, OUTLINE));
        r.setPadding(px(act, 10), px(act, 8), px(act, 8), px(act, 8));

        LinearLayout labels = new LinearLayout(act);
        labels.setOrientation(LinearLayout.VERTICAL);
        labels.addView(text(act, name, 14, INK));
        labels.addView(text(act,
            s.npc ? ("NPC " + id) : (stack > 1 ? ("x" + stack + "  ·  id " + id) : ("id " + id)),
            10, INK_DIM));
        r.addView(labels, new LinearLayout.LayoutParams(
            0, LinearLayout.LayoutParams.WRAP_CONTENT, 1f));

        Button go = new Button(act);
        go.setText(s.npc ? "Invocar" : "Pegar");
        go.setAllCaps(false);
        go.setTextColor(Color.WHITE);
        go.setTextSize(13);
        go.setBackground(panel(act, s.npc ? DIRT : GRASS, OUTLINE));
        go.setOnClickListener(new View.OnClickListener() {
            @Override public void onClick(View v) {
                if (s.npc) {
                    nOnSpawn(id);
                    Toast.makeText(act, name + " invocado", Toast.LENGTH_SHORT).show();
                } else {
                    nOnGive(id, stack);
                    Toast.makeText(act, name + (stack > 1 ? " x" + stack : ""),
                        Toast.LENGTH_SHORT).show();
                }
            }
        });
        r.addView(go, new LinearLayout.LayoutParams(
            px(act, 82), LinearLayout.LayoutParams.WRAP_CONTENT));

        LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(
            LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.WRAP_CONTENT);
        lp.bottomMargin = px(act, 4);
        r.setLayoutParams(lp);
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
