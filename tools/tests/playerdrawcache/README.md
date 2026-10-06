# Cache de DrawData do jogador

O fixture compara três formas de inserir um marcador em ModifyDrawInfo: escrever no próximo slot sem aumentar DrawDataCacheCount, atualizar o array e o contador manualmente e usar ModPlayer.AddDrawData. O marcador é produzido com MagicPixel nativo; o fixture não chama DrawData.Draw.

O hook de diagnóstico observa o cache depois de DrawPlayer_RenderAllLayers. O caso sem contador precisa desaparecer do cache ativo; os casos manual e helper precisam sobreviver às camadas e transformações. O marcador magenta continua visível depois do término para conferência visual, até o processo ser encerrado.

O marcador fica próximo ao centro da tela para continuar visível com o zoom do jogo. A presença no cache não substitui a conferência da imagem. Veja os [resultados e os limites da reprodução](RESULTADOS.md).

O fixture também verifica o prefixo inserido pelo helper com Skin após Head, uma ordem já satisfeita e uma ordem cíclica. Marcadores adicionais dentro de Skin e Head permitem conferir a ordem efetiva dos segmentos no cache. A linha final exige desenhos observados em cada caso e nenhuma perda do marcador. O ciclo deve produzir o aviso de ordem ignorada e manter o desenho original.

O [fixture multiplayer](../mpmodplayer/README.md) repete os três modos para o jogador local e remoto nos dois processos, incluindo reordenação e reconexão. Seus [resultados](../mpmodplayer/RESULTADOS-DRAW-MODITEM.md) registram 28 verificações de DrawDataCache aprovadas e conferência visual nas duas telas.

Com uma instância Android de teste conectada e o APK instalado:

```powershell
python tools/tests/moditemhooks/device.py prepare --fixture playerdrawcache --run-id minha-execucao
```

Inicie o jogo pelo launcher. O fixture roda apenas para o jogador local e desenho sem sombra, entre as atualizações 60 e 260. Leia o log do processo com.bunnyloader:game: a linha playerdrawcache FIM deve terminar em passou=true, com lost=0, failed=0 e reordered/unchanged/cyclic maiores que zero. Os números de desenhos observados variam com a taxa de quadros.

Depois da coleta do log e da captura visual:

```powershell
python tools/tests/moditemhooks/device.py cleanup --fixture playerdrawcache --run-id minha-execucao
```

O script usa o UID do manifesto, preserva as preferências próprias da sessão e verifica o caminho antes de remover o fixture. Use o mesmo run-id nas tentativas e na limpeza. O teste não altera o inventário ou os dados do personagem.
