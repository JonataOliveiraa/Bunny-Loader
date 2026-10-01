Toda arma nasce com o **dobro do dano**.

O mod engancha no momento em que o jogo define os atributos de um item
(`Item.SetDefaults`) e dobra o dano antes de ele chegar ao inventário. Vale
para tudo que causa dano:

- espada, arco e cajado, como a [i:757] Lâmina da Terra;
- até a [i:1] picareta.

Itens sem dano não são tocados.

> [!TIP]
> É o menor mod que faz alguma coisa: o `content/main.js` tem poucas linhas e é
> um bom primeiro arquivo para ler.
