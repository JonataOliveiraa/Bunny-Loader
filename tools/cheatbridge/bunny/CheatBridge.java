package bunny;

import android.app.Activity;
import android.graphics.Color;
import android.view.Gravity;
import android.view.View;
import android.widget.Button;
import android.widget.LinearLayout;
import android.widget.ScrollView;
import android.widget.TextView;
import android.widget.Toast;

// Menu de cheats do Bunny Loader: um overlay de Views do Android na Activity do
// jogo (mesmo mecanismo do botao, provado sem root). Um botao-toggle abre um
// painel rolavel de itens; tocar num item chama o nativo (RegisterNatives) que
// enfileira o "dar item", executado na thread do jogo pelo hook de DoUpdate.
//
// Compilada para um dex embutido na libbunny (tools/build-cheatbridge-dex.sh).
// UI do Android por cima da SurfaceView: independe de GLES/Vulkan e o input
// funciona sozinho. Overlay ImGui via render-hook fica pra polimento futuro.
public class CheatBridge {

    // Implementado em C++ -> bl::runtime::requestGive(type).
    public static native void nOnGive(int type);

    // Itens do menu v1 (curados do dump). Depois: picker completo + categorias
    // populadas por mods (tl.cheatMenu).
    private static final String[] NAMES = {
        "Minishark", "Megashark", "Star Cannon", "Space Gun", "Muramasa",
        "Terra Blade", "Meowmere", "Zenith", "Demon Wings",
        "Life Crystal", "Mana Crystal", "Gravitation Potion",
    };
    private static final int[] IDS = {
        98, 533, 197, 127, 155,
        757, 3063, 4956, 492,
        12, 109, 305,
    };

    public static void install(final Activity act) {
        act.runOnUiThread(new Runnable() {
            @Override public void run() {
                try { build(act); } catch (Throwable t) { /* nunca derruba o jogo */ }
            }
        });
    }

    private static void build(final Activity act) {
        final float d = act.getResources().getDisplayMetrics().density;

        // Painel (escondido por padrao).
        final LinearLayout panel = new LinearLayout(act);
        panel.setOrientation(LinearLayout.VERTICAL);
        panel.setBackgroundColor(0xE6141414);
        panel.setPadding(px(12, d), px(12, d), px(12, d), px(12, d));
        panel.setVisibility(View.GONE);

        TextView title = new TextView(act);
        title.setText("Bunny Loader — Itens");
        title.setTextColor(Color.WHITE);
        title.setTextSize(16);
        title.setPadding(0, 0, 0, px(8, d));
        panel.addView(title);

        final ScrollView scroll = new ScrollView(act);
        LinearLayout list = new LinearLayout(act);
        list.setOrientation(LinearLayout.VERTICAL);
        for (int i = 0; i < NAMES.length; i++) {
            final int id = IDS[i];
            final String name = NAMES[i];
            Button b = new Button(act);
            b.setText(name);
            b.setAllCaps(false);
            b.setOnClickListener(new View.OnClickListener() {
                @Override public void onClick(View v) {
                    nOnGive(id);
                    Toast.makeText(act, name + " +1", Toast.LENGTH_SHORT).show();
                }
            });
            list.addView(b);
        }
        scroll.addView(list);
        // Limita a altura pra rolar em vez de ocupar a tela toda.
        LinearLayout.LayoutParams slp = new LinearLayout.LayoutParams(
                px(200, d), px(280, d));
        panel.addView(scroll, slp);

        // Botao-toggle.
        final Button toggle = new Button(act);
        toggle.setText("🐰");
        toggle.setAllCaps(false);
        toggle.setOnClickListener(new View.OnClickListener() {
            @Override public void onClick(View v) {
                panel.setVisibility(
                        panel.getVisibility() == View.VISIBLE ? View.GONE : View.VISIBLE);
            }
        });

        // Posicionamento: toggle no topo-esquerda; painel logo abaixo.
        FrameLp tglp = new FrameLp(act, Gravity.TOP | Gravity.START, 24, 60, d);
        FrameLp pnlp = new FrameLp(act, Gravity.TOP | Gravity.START, 24, 120, d);
        act.addContentView(panel, pnlp.get());
        act.addContentView(toggle, tglp.get());
    }

    private static int px(int dp, float density) {
        return (int) (dp * density + 0.5f);
    }

    // Helper pra montar FrameLayout.LayoutParams (addContentView usa o layout do
    // content, tipicamente FrameLayout).
    private static final class FrameLp {
        final android.widget.FrameLayout.LayoutParams lp;
        FrameLp(Activity act, int gravity, int leftDp, int topDp, float d) {
            lp = new android.widget.FrameLayout.LayoutParams(
                    android.widget.FrameLayout.LayoutParams.WRAP_CONTENT,
                    android.widget.FrameLayout.LayoutParams.WRAP_CONTENT);
            lp.gravity = gravity;
            lp.leftMargin = (int) (leftDp * d + 0.5f);
            lp.topMargin = (int) (topDp * d + 0.5f);
        }
        android.widget.FrameLayout.LayoutParams get() { return lp; }
    }
}
