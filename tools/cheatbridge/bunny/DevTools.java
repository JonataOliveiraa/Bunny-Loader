package bunny;

import android.animation.ValueAnimator;
import android.app.Activity;
import android.content.Context;
import android.content.Intent;
import android.content.SharedPreferences;
import android.graphics.Bitmap;
import android.graphics.Rect;
import android.graphics.Typeface;
import android.os.Handler;
import android.os.Looper;
import android.os.Process;
import android.os.SystemClock;
import android.text.Editable;
import android.text.InputType;
import android.text.Spannable;
import android.text.SpannableStringBuilder;
import android.text.TextWatcher;
import android.text.style.ForegroundColorSpan;
import android.view.Gravity;
import android.view.KeyEvent;
import android.view.View;
import android.view.ViewGroup;
import android.view.ViewTreeObserver;
import android.view.WindowManager;
import android.view.animation.DecelerateInterpolator;
import android.view.inputmethod.EditorInfo;
import android.view.inputmethod.InputMethodManager;
import android.widget.EditText;
import android.widget.FrameLayout;
import android.widget.HorizontalScrollView;
import android.widget.ImageView;
import android.widget.LinearLayout;
import android.widget.PopupWindow;
import android.widget.ScrollView;
import android.widget.TextView;
import java.util.ArrayList;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

import static bunny.CheatBridge.GRASS;
import static bunny.CheatBridge.GRASS_LIT;
import static bunny.CheatBridge.INK;
import static bunny.CheatBridge.INK_DIM;
import static bunny.CheatBridge.OUTLINE;
import static bunny.CheatBridge.PANEL;
import static bunny.CheatBridge.PANEL_DARK;
import static bunny.CheatBridge.PANEL_LIT;
import static bunny.CheatBridge.SCRIM;
import static bunny.CheatBridge.applyTextSize;
import static bunny.CheatBridge.icon;
import static bunny.CheatBridge.panel;
import static bunny.CheatBridge.panelBig;
import static bunny.CheatBridge.px;
import static bunny.CheatBridge.sprite;
import static bunny.CheatBridge.text;

/**
 * As ferramentas de quem faz mod, dentro do jogo: reiniciar (os mods sao lidos
 * de novo da pasta) e o console JS.
 *
 * O console e um painel no pe da tela, e nao uma janela por cima: o jogo segue
 * visivel e tocavel acima dele. Com o teclado aberto, o painel sobe junto e
 * fica logo acima dele — sem a tela cheia de edicao que o Android poe no lugar
 * do jogo quando o aparelho esta deitado (IME_FLAG_NO_EXTRACT_UI), e sem
 * empurrar a tela do jogo para cima (SOFT_INPUT_ADJUST_NOTHING enquanto ele
 * esta aberto).
 */
final class DevTools {

    private DevTools() { }

    // ============================== botoes do Mod Menu ==============================

    /** O quadro 9 da tira do girassol: a flor aberta, o icone do "Reiniciar" parado. */
    private static final int BLOOM_FRAME = 9, FRAME_W = 52, FRAME_H = 54;
    private static Bitmap sBloom;

    static Bitmap bloom(Activity a) {
        if (sBloom == null) {
            Bitmap strip = sprite(a, "spr_loading");
            if (strip != null && strip.getHeight() >= (BLOOM_FRAME + 1) * FRAME_H) {
                sBloom = Bitmap.createBitmap(strip, 0, BLOOM_FRAME * FRAME_H, FRAME_W, FRAME_H);
            }
        }
        return sBloom;
    }

    /** Uma linha com "Reiniciar" e "Console", embaixo das horas do dia. */
    static View menuButtons(final Activity act) {
        LinearLayout row = new LinearLayout(act);
        row.setOrientation(LinearLayout.HORIZONTAL);
        View restart = pill(act, icon(act, bloom(act), 18), "Reiniciar", PANEL_DARK, 9f);
        restart.setOnClickListener(new View.OnClickListener() {
            @Override public void onClick(View v) { confirmRestart(act); }
        });
        View console = pill(act, consoleBadge(act, 10), "Console", PANEL_DARK, 9f);
        console.setOnClickListener(new View.OnClickListener() {
            @Override public void onClick(View v) {
                if (CheatBridge.sOverlay != null) CheatBridge.sOverlay.setVisibility(View.GONE);
                openConsole(act);
            }
        });
        LinearLayout.LayoutParams l = new LinearLayout.LayoutParams(0, px(act, 30), 1f);
        l.rightMargin = px(act, 2);
        row.addView(restart, l);
        LinearLayout.LayoutParams r = new LinearLayout.LayoutParams(0, px(act, 30), 1f);
        r.leftMargin = px(act, 2);
        row.addView(console, r);
        LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(
            LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.WRAP_CONTENT);
        lp.topMargin = px(act, 4);
        lp.bottomMargin = px(act, 2);
        row.setLayoutParams(lp);
        return row;
    }

    /** "</>" na fonte do jogo, o simbolo do console. */
    private static TextView consoleBadge(Activity act, float size) {
        TextView t = text(act, "</>", size, GRASS_LIT);
        t.setGravity(Gravity.CENTER);
        return t;
    }

    /** Botao com a cara dos do menu: icone e rotulo num painel. */
    private static LinearLayout pill(Activity act, View iconView, String label, int fill) {
        return pill(act, iconView, label, fill, 11f);
    }

    private static LinearLayout pill(Activity act, View iconView, String label, int fill, float size) {
        LinearLayout b = new LinearLayout(act);
        b.setOrientation(LinearLayout.HORIZONTAL);
        b.setGravity(Gravity.CENTER);
        b.setBackground(panel(act, fill, OUTLINE));
        int side = size < 11f ? 3 : 7;
        b.setPadding(px(act, side), 0, px(act, side), 0);
        if (iconView != null) b.addView(iconView);
        if (label != null) {
            TextView t = text(act, label, size, INK);
            t.setSingleLine(true);
            t.setPadding(iconView != null ? px(act, 4) : 0, 0, 0, 0);
            b.addView(t);
        }
        b.setClickable(true);
        return b;
    }

    // ================================== reiniciar ==================================
    //
    // O jogo le os mods UMA vez, ao subir. Reiniciar e fechar o processo do jogo
    // e abrir de novo — a RestartActivity do launcher (outro processo) espera
    // este morrer e abre a GameActivity, que carrega os mods da pasta outra vez.
    // Salvar antes e pedido ao nativo: o save do jogo so roda na thread dele.

    private static final long RESTART_POLL_MS = 100;
    private static final long RESTART_TIMEOUT_MS = 30000;
    private static View sRestartDialog;

    static void confirmRestart(final Activity act) {
        if (sRestartDialog != null) return;
        final boolean inWorld = CheatBridge.nInWorld();

        final FrameLayout scrim = new FrameLayout(act);
        scrim.setBackgroundColor(SCRIM);
        scrim.setClickable(true);   // o toque no escuro nao vai ao jogo

        final LinearLayout card = new LinearLayout(act);
        card.setOrientation(LinearLayout.VERTICAL);
        card.setBackground(panelBig(act, PANEL, OUTLINE));
        card.setPadding(px(act, 16), px(act, 14), px(act, 16), px(act, 14));
        card.setClickable(true);

        LinearLayout head = new LinearLayout(act);
        head.setGravity(Gravity.CENTER_VERTICAL);
        head.addView(icon(act, bloom(act), 30));
        TextView title = text(act, "Reiniciar o jogo", 16, INK);
        title.setPadding(px(act, 8), 0, 0, 0);
        head.addView(title);
        card.addView(head);

        TextView msg = text(act, inWorld
            ? "O jogo fecha e abre de novo, lendo os mods da pasta outra vez. "
              + "O que não foi salvo neste mundo se perde se você não salvar agora."
            : "O jogo fecha e abre de novo, lendo os mods da pasta outra vez.", 11, INK_DIM);
        msg.setPadding(0, px(act, 10), 0, px(act, 12));
        card.addView(msg);

        LinearLayout buttons = new LinearLayout(act);
        buttons.setGravity(Gravity.END);
        if (inWorld) {
            View save = pill(act, null, "Salvar e reiniciar", GRASS);
            save.setOnClickListener(new View.OnClickListener() {
                @Override public void onClick(View v) { startRestart(act, card, true); }
            });
            buttons.addView(save, buttonParams(act));
        }
        View plain = pill(act, null, inWorld ? "Só reiniciar" : "Reiniciar", PANEL_LIT);
        plain.setOnClickListener(new View.OnClickListener() {
            @Override public void onClick(View v) { startRestart(act, card, false); }
        });
        buttons.addView(plain, buttonParams(act));
        View cancel = pill(act, null, "Cancelar", PANEL_DARK);
        cancel.setOnClickListener(new View.OnClickListener() {
            @Override public void onClick(View v) { closeRestartDialog(); }
        });
        buttons.addView(cancel, buttonParams(act));
        card.addView(buttons);

        int width = Math.min(px(act, 460), act.getResources().getDisplayMetrics().widthPixels - px(act, 40));
        FrameLayout.LayoutParams cl = new FrameLayout.LayoutParams(width, FrameLayout.LayoutParams.WRAP_CONTENT);
        cl.gravity = Gravity.CENTER;
        scrim.addView(card, cl);
        act.addContentView(scrim, new FrameLayout.LayoutParams(
            FrameLayout.LayoutParams.MATCH_PARENT, FrameLayout.LayoutParams.MATCH_PARENT));
        sRestartDialog = scrim;
    }

    private static LinearLayout.LayoutParams buttonParams(Activity act) {
        LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(
            LinearLayout.LayoutParams.WRAP_CONTENT, px(act, 30));
        lp.leftMargin = px(act, 6);
        return lp;
    }

    private static void closeRestartDialog() {
        if (sRestartDialog == null) return;
        ViewGroup parent = (ViewGroup) sRestartDialog.getParent();
        if (parent != null) parent.removeView(sRestartDialog);
        sRestartDialog = null;
    }

    /** Troca o cartao pelo girassol girando e espera o nativo dizer que pode fechar. */
    private static void startRestart(final Activity act, LinearLayout card, boolean save) {
        card.removeAllViews();
        card.setGravity(Gravity.CENTER_HORIZONTAL);
        card.addView(new CheatBridge.Sunflower(act));
        final TextView status = text(act, save ? "Salvando o jogador e o mundo..." : "Reiniciando...", 12, INK);
        status.setGravity(Gravity.CENTER);
        status.setPadding(0, px(act, 8), 0, 0);
        card.addView(status);
        saveConsoleState(act);

        CheatBridge.nRequestRestart(save);
        final long start = SystemClock.uptimeMillis();
        final Handler h = new Handler(Looper.getMainLooper());
        h.post(new Runnable() {
            @Override public void run() {
                boolean ready;
                try { ready = CheatBridge.nRestartReady(); } catch (Throwable t) { ready = true; }
                if (ready) { status.setText("Reiniciando..."); h.postDelayed(new Runnable() {
                    @Override public void run() { relaunch(act); }
                }, 60); return; }
                if (SystemClock.uptimeMillis() - start > RESTART_TIMEOUT_MS) {
                    // O quadro do jogo nao andou (travou, ou esta pausado no
                    // fundo): reinicia sem o save, que e o que ainda da.
                    status.setText("O jogo não respondeu. Reiniciando sem salvar...");
                    h.postDelayed(new Runnable() {
                        @Override public void run() { relaunch(act); }
                    }, 1200);
                    return;
                }
                h.postDelayed(this, RESTART_POLL_MS);
            }
        });
    }

    /**
     * Abre a RestartActivity no processo do launcher e fecha este. O
     * startActivity volta depois que o sistema registrou o pedido, entao
     * matar o processo logo em seguida nao o perde.
     */
    static void relaunch(Activity act) {
        try {
            Intent i = new Intent();
            i.setClassName(act.getPackageName(), "dev.bunnyloader.RestartActivity");
            i.putExtra("pid", Process.myPid());
            i.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK | Intent.FLAG_ACTIVITY_NO_ANIMATION);
            act.startActivity(i);
            act.overridePendingTransition(0, 0);
        } catch (Throwable t) {
            CheatBridge.toast(act, "Não consegui reiniciar: " + t);
            closeRestartDialog();
            return;
        }
        Process.killProcess(Process.myPid());
        System.exit(0);
    }

    // =================================== console ===================================

    private static final String PREFS = "bunny_console";
    private static final int MAX_HISTORY = 60;
    /** Linhas da saida guardadas; passando disso, a saida recomeca das ultimas. */
    private static final int MAX_LINES = 700, KEEP_LINES = 450;
    private static final long POLL_MS = 120;

    // Cores da saida, por especie (Console.h no nativo).
    private static final int C_INPUT = 0xFFE8ECFF, C_PROMPT = 0xFF8A95C8, C_RESULT = 0xFFFFFFFF,
        C_ERROR = 0xFFFF7A7A, C_PRINT = 0xFFFFE08A, C_LOG = 0xFF9FB0E8, C_WARN = 0xFFFFB35C,
        C_HINT = 0xFF8A95C8;

    // A saida e o historico valem a partida inteira: fechar e abrir o console
    // mostra o que ja estava la. O historico (e o rascunho) vao tambem para o
    // disco, e sobrevivem ao "Reiniciar".
    private static final SpannableStringBuilder sOutput = new SpannableStringBuilder();
    private static int sOutputLines;
    private static ArrayList<String> sHistory;
    private static Console sConsole;

    static void openConsole(Activity act) {
        if (sConsole != null) {
            sConsole.show();
            return;
        }
        loadHistory(act);
        sConsole = new Console(act);
        sConsole.show();
    }

    private static void saveConsoleState(Activity act) {
        if (sConsole != null) sConsole.saveDraft();
        saveHistory(act);
    }

    private static SharedPreferences prefs(Context c) {
        return c.getSharedPreferences(PREFS, Context.MODE_PRIVATE);
    }

    private static void loadHistory(Activity act) {
        if (sHistory != null) return;
        sHistory = new ArrayList<String>();
        try {
            String all = prefs(act).getString("history", "");
            if (all.length() > 0) {
                for (String s : all.split("\u0001", -1)) if (s.length() > 0) sHistory.add(s);
            }
        } catch (Throwable t) { /* sem historico */ }
    }

    private static void saveHistory(Activity act) {
        if (sHistory == null) return;
        StringBuilder b = new StringBuilder();
        for (int i = 0; i < sHistory.size(); i++) {
            if (i > 0) b.append('\u0001');
            b.append(sHistory.get(i));
        }
        try { prefs(act).edit().putString("history", b.toString()).apply(); } catch (Throwable t) { }
    }

    /** Uma entrada da saida: especie (o 1o caractere, como no nativo) e texto. */
    private static void appendEntry(char kind, String body) {
        int color;
        String prefix = null;
        int prefixColor = C_PROMPT;
        switch (kind) {
            case 'i': color = C_INPUT; prefix = "› "; break;
            case 'r': color = C_RESULT; prefix = "← "; break;
            case 'x': color = C_ERROR; prefix = "× "; prefixColor = C_ERROR; break;
            case 'p': color = C_PRINT; prefix = "chat: "; prefixColor = 0xFFB9A060; break;
            case 'w': color = C_WARN; break;
            case 'e': color = C_ERROR; break;
            case 'h': color = C_HINT; break;
            default:  color = C_LOG; break;
        }
        if (sOutput.length() > 0) sOutput.append('\n');
        if (prefix != null) span(prefix, prefixColor);
        span(body, color);
        sOutputLines += 1;
        for (int i = 0; i < body.length(); i++) if (body.charAt(i) == '\n') sOutputLines++;
    }

    private static void span(String s, int color) {
        int start = sOutput.length();
        sOutput.append(s);
        sOutput.setSpan(new ForegroundColorSpan(color), start, sOutput.length(),
            Spannable.SPAN_EXCLUSIVE_EXCLUSIVE);
    }

    /** Passou do teto: fica so o fim. */
    private static boolean trimOutput() {
        if (sOutputLines <= MAX_LINES) return false;
        int drop = sOutputLines - KEEP_LINES, at = 0;
        while (drop > 0 && at < sOutput.length()) {
            if (sOutput.charAt(at) == '\n') drop--;
            at++;
        }
        sOutput.delete(0, at);
        sOutputLines = KEEP_LINES;
        return true;
    }

    /** O painel do console. Um por partida; fechar so o tira da tela. */
    private static final class Console {
        final Activity act;
        final LinearLayout panel;
        final View body;
        final TextView output;
        final ScrollView outputScroll;
        final EditText editor;
        final TextView collapseButton;
        final Handler handler = new Handler(Looper.getMainLooper());
        KeyboardWatcher keyboard;
        int keyboardHeight;
        int savedSoftInput = -1;
        boolean collapsed;
        boolean showing;
        int historyIndex = -1;
        String scratch = "";
        ValueAnimator slide;
        FrameLayout.LayoutParams layout;

        private final Runnable poll = new Runnable() {
            @Override public void run() {
                pullOutput();
                if (showing) handler.postDelayed(this, POLL_MS);
            }
        };

        Console(final Activity a) {
            act = a;
            panel = new LinearLayout(a);
            panel.setOrientation(LinearLayout.VERTICAL);
            panel.setBackground(panelBig(a, PANEL, OUTLINE));
            panel.setPadding(px(a, 8), px(a, 6), px(a, 8), px(a, 8));
            // Toque no painel e do painel: sem isto, o espaco entre as partes
            // deixava o toque atravessar para o jogo.
            panel.setClickable(true);

            // ---- topo ----
            LinearLayout head = new LinearLayout(a);
            head.setOrientation(LinearLayout.HORIZONTAL);
            head.setGravity(Gravity.CENTER_VERTICAL);
            TextView badge = consoleBadge(a, 12);
            badge.setBackground(panel(a, PANEL_DARK, OUTLINE));
            badge.setPadding(px(a, 6), 0, px(a, 6), 0);
            head.addView(badge, new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.WRAP_CONTENT, px(a, 26)));
            TextView title = text(a, "Console JS", 13, INK);
            title.setPadding(px(a, 8), 0, px(a, 6), 0);
            head.addView(title);
            TextView where = text(a, "na thread do jogo", 9, INK_DIM);
            where.setSingleLine(true);
            head.addView(where, new LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.WRAP_CONTENT, 1f));

            head.addView(iconButton(a, arrow(a, -90), "Anterior", new Runnable() {
                @Override public void run() { history(-1); }
            }));
            head.addView(iconButton(a, arrow(a, 90), "Próximo", new Runnable() {
                @Override public void run() { history(+1); }
            }));
            head.addView(textButton(a, "Limpar", PANEL_DARK, new Runnable() {
                @Override public void run() { clearOutput(); }
            }));
            head.addView(iconButton(a, icon(a, bloom(a), 20), "Reiniciar", new Runnable() {
                @Override public void run() { saveDraft(); hideKeyboard(); confirmRestart(act); }
            }));
            collapseButton = textButton(a, "_", PANEL_DARK, new Runnable() {
                @Override public void run() { setCollapsed(!collapsed); }
            });
            head.addView(collapseButton);
            head.addView(iconButton(a, icon(a, sprite(a, "ic_fechar"), 20), "Fechar", new Runnable() {
                @Override public void run() { hide(); }
            }));
            panel.addView(head, new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT, px(a, 28)));

            LinearLayout inner = new LinearLayout(a);
            inner.setOrientation(LinearLayout.VERTICAL);
            body = inner;

            // ---- saida ----
            output = new TextView(a);
            output.setTypeface(Typeface.MONOSPACE);
            applyTextSize(a, output, 7.5f);
            output.setTextColor(C_LOG);
            output.setTextIsSelectable(true);
            output.setPadding(px(a, 6), px(a, 4), px(a, 6), px(a, 4));
            outputScroll = new ScrollView(a);
            outputScroll.setBackground(panel(a, PANEL_DARK, OUTLINE));
            outputScroll.setFillViewport(true);
            outputScroll.addView(output);
            LinearLayout.LayoutParams ol = new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT, 0, 1f);
            ol.topMargin = px(a, 5);
            inner.addView(outputScroll, ol);

            // ---- editor + rodar ----
            LinearLayout editRow = new LinearLayout(a);
            editRow.setOrientation(LinearLayout.HORIZONTAL);
            editor = new EditText(a);
            editor.setTypeface(Typeface.MONOSPACE);
            applyTextSize(a, editor, 8.5f);
            editor.setTextColor(INK);
            editor.setHintTextColor(C_HINT);
            editor.setHint("player.statLife = 500");
            editor.setBackground(panel(a, PANEL_DARK, OUTLINE));
            editor.setPadding(px(a, 6), px(a, 4), px(a, 6), px(a, 4));
            editor.setGravity(Gravity.TOP | Gravity.START);
            // Codigo: sem corretor, sem sugestao, varias linhas. E sem a tela
            // cheia de edicao do Android no aparelho deitado.
            editor.setInputType(InputType.TYPE_CLASS_TEXT | InputType.TYPE_TEXT_FLAG_MULTI_LINE
                | InputType.TYPE_TEXT_FLAG_NO_SUGGESTIONS);
            editor.setImeOptions(EditorInfo.IME_FLAG_NO_EXTRACT_UI | EditorInfo.IME_FLAG_NO_FULLSCREEN
                | EditorInfo.IME_ACTION_NONE);
            editor.setHorizontallyScrolling(false);
            editor.setMinLines(2);
            editor.setMaxLines(5);
            editor.setVerticalScrollBarEnabled(true);
            editor.setText(prefs(a).getString("draft", ""));
            editor.setSelection(editor.getText().length());
            editor.addTextChangedListener(new CodeWatcher(editor));
            editor.setOnKeyListener(new View.OnKeyListener() {
                @Override public boolean onKey(View v, int keyCode, KeyEvent e) {
                    if (e.getAction() != KeyEvent.ACTION_DOWN) {
                        // Voltar com o teclado ja fechado: fecha o console.
                        if (keyCode == KeyEvent.KEYCODE_BACK && e.getAction() == KeyEvent.ACTION_UP
                            && keyboardHeight == 0) { hide(); return true; }
                        return false;
                    }
                    // Teclado fisico: Ctrl+Enter roda.
                    if (keyCode == KeyEvent.KEYCODE_ENTER && e.isCtrlPressed()) { run(); return true; }
                    return false;
                }
            });
            editRow.addView(editor, new LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.WRAP_CONTENT, 1f));
            LinearLayout runButton = pill(a, null, "Rodar", GRASS);
            runButton.setOnClickListener(new View.OnClickListener() {
                @Override public void onClick(View v) { run(); }
            });
            LinearLayout.LayoutParams rl = new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.WRAP_CONTENT, LinearLayout.LayoutParams.MATCH_PARENT);
            rl.leftMargin = px(a, 6);
            editRow.addView(runButton, rl);
            LinearLayout.LayoutParams el = new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.WRAP_CONTENT);
            el.topMargin = px(a, 5);
            inner.addView(editRow, el);

            // ---- simbolos ----
            inner.addView(symbolBar(a), new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT, px(a, 26)));

            panel.addView(inner, new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT, 0, 1f));

            layout = new FrameLayout.LayoutParams(FrameLayout.LayoutParams.MATCH_PARENT, restingHeight());
            layout.gravity = Gravity.BOTTOM;
            layout.leftMargin = px(a, 12);
            layout.rightMargin = px(a, 12);
            layout.bottomMargin = px(a, 8);
            a.addContentView(panel, layout);

            if (sOutput.length() == 0) {
                appendEntry('h', "O código roda na thread do jogo, como num hook. Main, ID e player (o "
                    + "seu jogador) já estão prontos. print(...) escreve no chat; bl.log(...), no log "
                    + "(que aparece aqui também). let/const valem só na execução; para guardar entre "
                    + "uma e outra, var ou globalThis.x.");
            }
            output.setText(sOutput);
        }

        // ---- mostrar, esconder, recolher ----

        void show() {
            if (showing) return;
            showing = true;
            panel.setVisibility(View.VISIBLE);
            // A tela do jogo nao sobe com o teclado: quem sobe e o painel.
            WindowManager.LayoutParams wl = act.getWindow().getAttributes();
            savedSoftInput = wl.softInputMode;
            act.getWindow().setSoftInputMode(WindowManager.LayoutParams.SOFT_INPUT_ADJUST_NOTHING
                | (savedSoftInput & WindowManager.LayoutParams.SOFT_INPUT_MASK_STATE));
            keyboard = new KeyboardWatcher(act, this);
            handler.post(poll);
            if (!collapsed) focusEditor();
            scrollToEnd();
        }

        void hide() {
            if (!showing) return;
            showing = false;
            saveDraft();
            hideKeyboard();
            editor.clearFocus();
            if (keyboard != null) { keyboard.close(); keyboard = null; }
            if (slide != null) slide.cancel();
            keyboardHeight = 0;
            layout.bottomMargin = px(act, 8);
            layout.height = restingHeight();
            panel.setLayoutParams(layout);
            panel.setVisibility(View.GONE);
            if (savedSoftInput >= 0) act.getWindow().setSoftInputMode(savedSoftInput);
            handler.removeCallbacks(poll);
        }

        void setCollapsed(boolean c) {
            collapsed = c;
            body.setVisibility(c ? View.GONE : View.VISIBLE);
            collapseButton.setText(c ? "^" : "_");
            if (c) hideKeyboard();
            place(keyboardHeight, false);
            if (!c) focusEditor();
        }

        void focusEditor() {
            editor.requestFocus();
            editor.postDelayed(new Runnable() {
                @Override public void run() {
                    InputMethodManager imm = (InputMethodManager) act.getSystemService(Context.INPUT_METHOD_SERVICE);
                    if (imm != null) imm.showSoftInput(editor, InputMethodManager.SHOW_IMPLICIT);
                }
            }, 120);
        }

        void hideKeyboard() {
            InputMethodManager imm = (InputMethodManager) act.getSystemService(Context.INPUT_METHOD_SERVICE);
            if (imm != null) imm.hideSoftInputFromWindow(editor.getWindowToken(), 0);
        }

        // ---- o painel acompanha o teclado ----

        /** Altura com o teclado fechado: pouco menos da metade da tela. */
        int restingHeight() {
            return Math.round(screenHeight() * 0.46f);
        }

        int screenHeight() {
            View decor = act.getWindow().getDecorView();
            int h = decor.getHeight();
            return h > 0 ? h : act.getResources().getDisplayMetrics().heightPixels;
        }

        /** O teclado mudou de altura (0 = fechado): o painel vai para cima dele. */
        void onKeyboard(int height) {
            keyboardHeight = height;
            place(height, true);
        }

        void place(int kb, boolean animate) {
            final int fromBottom = layout.bottomMargin, fromHeight = panel.getHeight() > 0 ? panel.getHeight() : layout.height;
            final int toBottom = kb + px(act, kb > 0 ? 4 : 8);
            int available = screenHeight() - toBottom - px(act, 8);
            final int toHeight = collapsed ? ViewGroup.LayoutParams.WRAP_CONTENT
                : Math.max(px(act, 90), Math.min(restingHeight(), available));
            if (slide != null) slide.cancel();
            if (!animate || collapsed) {
                layout.bottomMargin = toBottom;
                layout.height = toHeight;
                panel.setLayoutParams(layout);
                scrollToEnd();
                return;
            }
            // Pela margem e pela altura, nao por translacao: a Activity do jogo
            // desenha por software, e View transladada so e redesenhada no
            // quadro antigo (o mesmo cuidado do botao do menu).
            slide = ValueAnimator.ofFloat(0f, 1f);
            slide.setDuration(140);
            slide.setInterpolator(new DecelerateInterpolator());
            slide.addUpdateListener(new ValueAnimator.AnimatorUpdateListener() {
                @Override public void onAnimationUpdate(ValueAnimator va) {
                    float f = (Float) va.getAnimatedValue();
                    layout.bottomMargin = Math.round(fromBottom + (toBottom - fromBottom) * f);
                    layout.height = Math.round(fromHeight + (toHeight - fromHeight) * f);
                    panel.setLayoutParams(layout);
                }
            });
            slide.start();
            panel.postDelayed(new Runnable() {
                @Override public void run() { scrollToEnd(); }
            }, 160);
        }

        // ---- rodar, historico, saida ----

        void run() {
            String code = editor.getText().toString();
            if (code.trim().length() == 0) return;
            appendEntry('i', code);
            refreshOutput();
            if (sHistory.isEmpty() || !sHistory.get(sHistory.size() - 1).equals(code)) {
                sHistory.add(code);
                while (sHistory.size() > MAX_HISTORY) sHistory.remove(0);
                saveHistory(act);
            }
            historyIndex = -1;
            scratch = "";
            editor.setText("");
            saveDraft();
            try { CheatBridge.nConsoleRun(code); } catch (Throwable t) {
                appendEntry('x', "o núcleo nativo não respondeu: " + t);
                refreshOutput();
            }
        }

        void history(int step) {
            if (sHistory.isEmpty()) return;
            if (historyIndex < 0) {
                if (step > 0) return;
                scratch = editor.getText().toString();
                historyIndex = sHistory.size();
            }
            historyIndex += step;
            if (historyIndex < 0) historyIndex = 0;
            if (historyIndex >= sHistory.size()) {
                historyIndex = -1;
                editor.setText(scratch);
            } else {
                editor.setText(sHistory.get(historyIndex));
            }
            editor.setSelection(editor.getText().length());
        }

        void pullOutput() {
            String[] entries;
            try { entries = CheatBridge.nConsoleTake(); } catch (Throwable t) { return; }
            if (entries == null || entries.length == 0) return;
            for (String e : entries) {
                if (e == null || e.length() == 0) continue;
                appendEntry(e.charAt(0), e.substring(1));
            }
            refreshOutput();
        }

        void refreshOutput() {
            trimOutput();
            output.setText(sOutput);
            scrollToEnd();
        }

        void clearOutput() {
            sOutput.clear();
            sOutputLines = 0;
            output.setText(sOutput);
        }

        void scrollToEnd() {
            // scrollTo, e nao fullScroll: o fullScroll leva o FOCO para baixo e o
            // teclado perdia o editor.
            outputScroll.post(new Runnable() {
                @Override public void run() {
                    outputScroll.scrollTo(0, Math.max(0, output.getHeight() - outputScroll.getHeight()));
                }
            });
        }

        void saveDraft() {
            try { prefs(act).edit().putString("draft", editor.getText().toString()).apply(); } catch (Throwable t) { }
        }

        // ---- teclas ----

        /** As teclas que o teclado do celular esconde, uma a um toque. */
        View symbolBar(Activity a) {
            HorizontalScrollView scroll = new HorizontalScrollView(a);
            scroll.setHorizontalScrollBarEnabled(false);
            LinearLayout row = new LinearLayout(a);
            row.setOrientation(LinearLayout.HORIZONTAL);
            row.setPadding(0, px(a, 4), 0, 0);
            final String[] keys = {"Tab", "(", ")", "{", "}", "[", "]", ";", ".", ",", "'", "\"", "`",
                "=", "=>", "+", "-", "*", "/", "!", "<", ">", "&", "|", "?", ":", "_", "$"};
            for (final String k : keys) {
                TextView key = new TextView(a);
                key.setText(k);
                key.setTypeface(Typeface.MONOSPACE, Typeface.BOLD);
                applyTextSize(a, key, 9);
                key.setTextColor(INK);
                key.setGravity(Gravity.CENTER);
                key.setBackground(panel(a, PANEL_DARK, OUTLINE));
                key.setMinWidth(px(a, 30));
                key.setPadding(px(a, 6), 0, px(a, 6), 0);
                key.setOnClickListener(new View.OnClickListener() {
                    @Override public void onClick(View v) { insert(k.equals("Tab") ? "  " : k); }
                });
                LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(
                    LinearLayout.LayoutParams.WRAP_CONTENT, LinearLayout.LayoutParams.MATCH_PARENT);
                lp.rightMargin = px(a, 3);
                row.addView(key, lp);
            }
            scroll.addView(row);
            return scroll;
        }

        void insert(String s) {
            int start = Math.max(0, editor.getSelectionStart()), end = Math.max(0, editor.getSelectionEnd());
            editor.getText().replace(Math.min(start, end), Math.max(start, end), s);
            if (!editor.hasFocus()) editor.requestFocus();
        }

        private View iconButton(Activity a, View iconView, String description, final Runnable action) {
            FrameLayout b = new FrameLayout(a);
            b.setBackground(panel(a, PANEL_DARK, OUTLINE));
            b.setContentDescription(description);
            FrameLayout.LayoutParams il = new FrameLayout.LayoutParams(px(a, 20), px(a, 20));
            il.gravity = Gravity.CENTER;
            b.addView(iconView, il);
            b.setOnClickListener(new View.OnClickListener() {
                @Override public void onClick(View v) { action.run(); }
            });
            LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(px(a, 30), px(a, 26));
            lp.leftMargin = px(a, 4);
            b.setLayoutParams(lp);
            return b;
        }

        private TextView textButton(Activity a, String label, int fill, final Runnable action) {
            TextView t = text(a, label, 10, INK);
            t.setGravity(Gravity.CENTER);
            t.setBackground(panel(a, fill, OUTLINE));
            t.setPadding(px(a, 8), 0, px(a, 8), 0);
            t.setMinWidth(px(a, 30));
            t.setOnClickListener(new View.OnClickListener() {
                @Override public void onClick(View v) { action.run(); }
            });
            LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.WRAP_CONTENT, px(a, 26));
            lp.leftMargin = px(a, 4);
            t.setLayoutParams(lp);
            return t;
        }
    }

    /** As setas do menu, giradas: -90 aponta para cima, 90 para baixo. */
    private static View arrow(Activity a, float degrees) {
        ImageView v = icon(a, sprite(a, "ic_seta_dir"), 16);
        v.setRotation(degrees);
        return v;
    }

    // ================================ o teclado ================================

    /**
     * A altura do teclado, de qualquer versao do Android.
     *
     * Uma PopupWindow de largura zero e altura da tela, que o teclado
     * redimensiona (ADJUST_RESIZE, a dela); a diferenca entre a tela e a parte
     * visivel dela e o teclado. Funciona com a janela do jogo em tela cheia e
     * em ADJUST_NOTHING, que e justamente quando a janela do jogo nao sabe do
     * teclado.
     */
    private static final class KeyboardWatcher implements ViewTreeObserver.OnGlobalLayoutListener {
        private final Activity act;
        private final Console console;
        private final View probe;
        private final PopupWindow popup;
        private final Rect frame = new Rect();
        private int last = -1;

        KeyboardWatcher(Activity a, Console c) {
            act = a;
            console = c;
            probe = new View(a);
            popup = new PopupWindow(probe, 0, ViewGroup.LayoutParams.MATCH_PARENT);
            popup.setSoftInputMode(WindowManager.LayoutParams.SOFT_INPUT_ADJUST_RESIZE
                | WindowManager.LayoutParams.SOFT_INPUT_STATE_UNCHANGED);
            popup.setInputMethodMode(PopupWindow.INPUT_METHOD_NEEDED);
            popup.setFocusable(false);
            popup.setTouchable(false);
            probe.getViewTreeObserver().addOnGlobalLayoutListener(this);
            final View decor = a.getWindow().getDecorView();
            decor.post(new Runnable() {
                @Override public void run() {
                    try {
                        if (!act.isFinishing()) popup.showAtLocation(decor, Gravity.NO_GRAVITY, 0, 0);
                    } catch (Throwable t) { /* sem medida: o painel fica no pe da tela */ }
                }
            });
        }

        @Override public void onGlobalLayout() {
            probe.getWindowVisibleDisplayFrame(frame);
            int screen = console.screenHeight();
            int kb = screen - frame.bottom;
            // A barra de navegacao que aparece junto nao e teclado.
            if (kb < screen / 8) kb = 0;
            if (kb != last) {
                last = kb;
                console.onKeyboard(kb);
            }
        }

        void close() {
            probe.getViewTreeObserver().removeOnGlobalLayoutListener(this);
            try { popup.dismiss(); } catch (Throwable t) { }
        }
    }

    // ================================ o editor ================================

    /**
     * Recuo automatico e cores de sintaxe.
     *
     * Enter repete o recuo da linha de cima (e acrescenta dois espacos depois
     * de "{", "(" ou "["). As cores sao repintadas um instante depois da ultima
     * tecla, e nao a cada uma.
     */
    private static final class CodeWatcher implements TextWatcher {
        private static final Pattern TOKENS = Pattern.compile(
            "(//[^\\n]*|/\\*[\\s\\S]*?(?:\\*/|$))"
            + "|(\"(?:\\\\.|[^\"\\\\\\n])*\"?|'(?:\\\\.|[^'\\\\\\n])*'?|`(?:\\\\.|[^`\\\\])*`?)"
            + "|\\b(\\d+(?:\\.\\d+)?)\\b"
            + "|\\b(const|let|var|function|return|if|else|for|while|do|break|continue|new|class|"
            + "extends|this|super|typeof|instanceof|of|in|try|catch|finally|throw|switch|case|"
            + "default|null|undefined|true|false|async|await|delete|void)\\b"
            + "|\\b(Terraria|Microsoft|System|bl|print|Main|ModItem|ModNPC|ModProjectile|ModPlayer|"
            + "ModBuff|ModTile|ModSystem|ModContent|ModLoader|Vector2|Color|Rectangle|Rand|Ref|"
            + "MathHelper|SoundEngine|SoundStyle)\\b");
        private static final int[] COLORS = {
            0,
            0xFF7F8AB8,   // comentario
            0xFF9CE37D,   // texto
            0xFF7FD1FF,   // numero
            0xFFFFD24A,   // palavra-chave
            0xFFFF9EDB,   // API do jogo e do Bunny Loader
        };
        private static final long DELAY_MS = 120;

        /** Marca das cores deste editor, para tirar so as nossas. */
        private static final class Hl extends ForegroundColorSpan {
            Hl(int c) { super(c); }
        }

        private final EditText editor;
        private int indentAt = -1;
        private String indent;
        private boolean busy;
        private final Runnable paint = new Runnable() {
            @Override public void run() { colorize(); }
        };

        CodeWatcher(EditText e) {
            editor = e;
            e.post(paint);
        }

        @Override public void beforeTextChanged(CharSequence s, int start, int count, int after) { }

        @Override public void onTextChanged(CharSequence s, int start, int before, int count) {
            if (busy || count != 1 || before != 0 || s.charAt(start) != '\n') return;
            int lineStart = start - 1;
            while (lineStart >= 0 && s.charAt(lineStart) != '\n') lineStart--;
            lineStart++;
            StringBuilder b = new StringBuilder();
            for (int i = lineStart; i < start && s.charAt(i) == ' '; i++) b.append(' ');
            int last = start - 1;
            while (last >= lineStart && s.charAt(last) == ' ') last--;
            if (last >= lineStart) {
                char c = s.charAt(last);
                if (c == '{' || c == '(' || c == '[') b.append("  ");
            }
            if (b.length() > 0) {
                indentAt = start + 1;
                indent = b.toString();
            }
        }

        @Override public void afterTextChanged(Editable e) {
            if (busy) return;
            if (indent != null && indentAt >= 0 && indentAt <= e.length()) {
                busy = true;
                e.insert(indentAt, indent);
                busy = false;
            }
            indent = null;
            indentAt = -1;
            editor.removeCallbacks(paint);
            editor.postDelayed(paint, DELAY_MS);
        }

        private void colorize() {
            Editable e = editor.getText();
            for (Hl h : e.getSpans(0, e.length(), Hl.class)) e.removeSpan(h);
            if (e.length() > 20000) return;   // colado enorme: sem cor, sem engasgo
            Matcher m = TOKENS.matcher(e);
            while (m.find()) {
                for (int g = 1; g < COLORS.length; g++) {
                    if (m.start(g) < 0) continue;
                    e.setSpan(new Hl(COLORS[g]), m.start(g), m.end(g), Spannable.SPAN_EXCLUSIVE_EXCLUSIVE);
                    break;
                }
            }
        }
    }
}
