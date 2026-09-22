package bunny;

import android.app.Activity;
import android.view.Gravity;
import android.view.View;
import android.widget.Button;
import android.widget.FrameLayout;

// Ponte minima do menu de cheats: um botao flutuante na Activity do jogo.
//
// Compilada para um dex embutido na libbunny (tools/build-cheatbridge-dex.sh) e
// carregada em runtime via InMemoryDexClassLoader (ui/CheatButton.cpp). Nao faz
// parte do app launcher — vive dentro do processo do jogo. O onClick chama um
// metodo nativo (RegisterNatives) que enfileira o pedido; o hook de DoUpdate o
// executa na thread do jogo.
public class CheatBridge implements View.OnClickListener {

    // Implementado em C++ -> bl::runtime::requestGive(type).
    public static native void nOnGive(int type);

    // Chamada pelo nativo com a Activity do jogo. Monta o botao na UI thread.
    public static void install(final Activity act) {
        act.runOnUiThread(new Runnable() {
            @Override public void run() {
                Button b = new Button(act);
                b.setText("Minishark");
                b.setAllCaps(false);
                b.setOnClickListener(new CheatBridge());

                FrameLayout.LayoutParams lp = new FrameLayout.LayoutParams(
                        FrameLayout.LayoutParams.WRAP_CONTENT,
                        FrameLayout.LayoutParams.WRAP_CONTENT);
                lp.gravity = Gravity.TOP | Gravity.START;
                lp.topMargin = 120;
                lp.leftMargin = 24;
                act.addContentView(b, lp);
            }
        });
    }

    @Override public void onClick(View v) {
        nOnGive(98);  // Minishark (ItemID)
    }
}
