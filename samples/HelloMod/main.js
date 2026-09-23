// Hello Mod — turbina a Minishark (cadência + velocidade do projétil).
//
// ================================ API ================================
//
//   Terraria.Item                    classe, pela árvore de namespaces
//   Terraria.ID.ItemID.Minishark     campo estático como propriedade
//   Terraria.Main.myPlayer           PROPRIEDADE C# (vira get_myPlayer())
//   Classe['ret Nome(T a, U b)']     método, por ASSINATURA (recomendado)
//   Classe.Nome                      método, se houver um só overload
//   obj.campo / obj.campo = v        campo de instância como propriedade
//   metodo.hook((original, self, ...args) => { ... })
//   bl.log(...)
//   bl.classOf(ns, nome)             escape hatch, quando a árvore não ajuda
//
// Arrays:
//
//   const p = Terraria.Main.player[Terraria.Main.myPlayer];
//   p.statLife = p.statLifeMax;
//   Terraria.Main.player.length
//
// Enum sai como NÚMERO, direto — sem `.value__`:
//
//   Terraria.PartyHatColor.Pink      // 2
//
// Struct (Vector2, Color, Rectangle) é uma VISTA para dentro do dono, então
// escrever nela altera o jogo:
//
//   npc.position.X = 100;            // move o NPC
//   item.color.R = 255;
//   npc.position = outroVector2;     // troca o struct inteiro
//   bl.log(npc.position)             // Vector2(X=100, Y=42)
//
// Duas exceções, de propósito: o struct que volta de um MÉTODO e o que chega
// como ARGUMENTO de hook são cópias — escrever neles não vai a lugar nenhum,
// que é a semântica de valor do C#.
//
// Chamar método do jogo — o objeto é `this`, como em JS:
//
//   Terraria.Main['int get_myPlayer()']()
//   item['void SetDefaults(int Type, ItemVariant variant)'](98, null)
//
// Criar objeto, equivalente a `new Item()` / `new Vector2(5,10)` em C#:
//
//   const it = Terraria.Item.new();
//   it['void .ctor()']();
//
//   const v = Microsoft.Xna.Framework.Vector2.new();
//   v['void .ctor(float x, float y)'](5.0, 10.0);   // struct também
//
// No hook, `original` aceita ARGUMENTOS — é assim que se edita uma entrada
// antes de deixar o jogo processá-la. O que você não passar fica como veio:
//
//   Dano.hook((original, self, dano) => original(self, dano * 2));
//
// E o que o callback devolver vira o retorno do método, float inclusive:
//
//   Distance.hook((original, a, b) => original(a, b) * 10);
//
// A assinatura vem do dump, copiada como está lá. Prefira-a ao nome puro:
// nome + contagem de parâmetros não desambigua overloads. Quando o nome é
// ambíguo o loader RECUSA e lista os overloads, em vez de escolher um.
//
// Método do jogo que lança vira exceção JS — não é engolido.
//
// O que ainda NÃO dá: hookar método que devolve struct, e hookar método com
// mais de 8 argumentos inteiros ou 8 de ponto flutuante (o excedente viaja
// pela pilha, que não capturamos). Os dois são recusados com mensagem, nunca
// adivinhados.

const MINISHARK = Terraria.ID.ItemID.Minishark;
bl.log('HelloMod: Minishark = ' + MINISHARK + '; hookando Item.SetDefaults');

const SetDefaults = Terraria.Item['void SetDefaults(int Type, ItemVariant variant)'];

SetDefaults.hook((original, self, type, variant) => {
    original(self, type, variant);   // deixa o jogo aplicar os defaults
    if (type === MINISHARK) {
        self.useTime = 4;            // cadência (menor = mais rápido)
        self.useAnimation = 4;
        self.shootSpeed = 10.0;      // float: o tipo do campo decide a escrita
        bl.log('HelloMod: Minishark turbinada!');
    }
});
