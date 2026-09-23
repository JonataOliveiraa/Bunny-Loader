// Dobro de Dano — toda arma do jogo bate o dobro.
//
// O mod mais curto que faz algo de verdade. Anatomia:
//
//   1. pega o método do jogo, pela árvore de namespaces + assinatura do dump
//   2. pendura um hook nele
//   3. deixa o jogo rodar primeiro (original)
//   4. mexe no resultado
//
// Item.SetDefaults é chamado UMA vez por item criado, e é onde o Terraria
// preenche dano, cadência, tamanho do stack. Alterar aqui pega tudo: o que
// você cria, o que cai de inimigo, o que já está no baú.

const SetDefaults = Terraria.Item['void SetDefaults(int Type, ItemVariant variant)'];

SetDefaults.hook((original, self, type, variant) => {
    original(self, type, variant);      // 3 — o jogo aplica os defaults dele

    if (self.damage > 0) {              // 4 — só o que causa dano
        self.damage = self.damage * 2;
    }
});

bl.log('Dobro de Dano: ativo');
