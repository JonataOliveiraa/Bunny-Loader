# Regressão de SaveData/LoadData de ModItem

Esta suíte verifica o mecanismo do loader. O item de teste tem dono, contador
e dados aninhados; há também classes com herança, tag vazio e callbacks que
lançam exceções. Ela não depende da implementação do Valentine Ring.

## Testes sem dispositivo

Na raiz do repositório:

```powershell
node tools/tests/moditemdata/check.mjs
```

O runtime carrega os arquivos JS reais do projeto e usa o registro real de
`ModItem`. Somente as chamadas ao Terraria/IL2CPP são simuladas. Os casos
incluem 200 round trips determinísticos de dados variados, valores ausentes,
herança, instâncias independentes, validação de JSON, exceções, recuperação,
clones, ordem dos pacotes, retransmissão, desconexão e drops mobile.

O teste C++ inclui diretamente o cabeçalho usado em produção, sem uma cópia
da implementação do formato:

```powershell
wsl.exe -d Ubuntu -- bash -lc 'cd "/mnt/c/Scripts/Bunny Loader" && g++ -std=c++20 -Wall -Wextra -Werror -fsanitize=address,undefined -Iapp/src/main/cpp tools/tests/moditemdata/format.cpp -o /tmp/bunny-moditemdata-format && /tmp/bunny-moditemdata-format'
```

Também pode ser compilado em outro ambiente com C++20, ajustando os caminhos.
Verifica arquivos antigos e novos, linhas longas, Unicode, contêineres,
prefixos, números inválidos, streams com erro e delimitadores indevidos.

## Integração no Terraria Android

Requer APK debug compilado, ADB, dispositivo com root, personagem e mundo
locais existentes. Feche o jogo antes de `prepare`. O runner instala o APK,
faz backup das preferências, habilita somente este fixture e inicia **cópias**
dos saves chamadas `BL_ItemData_Test`. Os slots alterados na rodada nativa
são restaurados antes de retornar ao loop do jogo.

```powershell
python tools/tests/moditemdata/device.py prepare --device 127.0.0.1:16384 --role native
python tools/tests/moditemdata/device.py launch --device 127.0.0.1:16384 --role native
# Depois do carregamento e da linha FIM no log:
python tools/tests/moditemdata/device.py collect --device 127.0.0.1:16384 --role native
```

`collect` falha se não houver conclusão com verificações executadas e zero
falhas. Os logs ficam em `build/moditemdata/<dispositivo>/`. A rodada chama
`SavePlayer`/`LoadPlayer` reais, escreve um personagem separado
`BL_ItemData_Probe.plr`, verifica todos os 15 contêineres e as falhas dos
callbacks. Para baús, chama `SaveChests` e `FixAgainstExploits` reais: o
stream vanilla é em memória, e o save complementar `.wld.bl` vai para disco.
Não é um teste de fechar/reabrir o processo durante a gravação do mundo.

## Multiplayer entre duas instâncias

Após guardar os logs nativos, feche o jogo de teste. Prepare os dois lados:

```powershell
python tools/tests/moditemdata/device.py prepare --device 127.0.0.1:16384 --role host
python tools/tests/moditemdata/device.py prepare --device 127.0.0.1:16416 --role client
adb -s 127.0.0.1:16384 forward tcp:7777 tcp:7777
python tools/tests/moditemdata/device.py launch --device 127.0.0.1:16384 --role host
# Aguarde o host registrar running=true.
python tools/tests/moditemdata/device.py launch --device 127.0.0.1:16416 --role client
# Depois das conclusões FIM dos dois lados:
python tools/tests/moditemdata/device.py collect --device 127.0.0.1:16384 --role host
python tools/tests/moditemdata/device.py collect --device 127.0.0.1:16416 --role client
```

O host usa a entrada nativa `GUIMultiplayerHost.HostServer`. O cliente acessa
`10.0.2.2:7777`, o host NAT dos emuladores usados nesta rodada. Ajuste esse
endereço em `content/multiplayer.js` para outros dispositivos.

Os pacotes do fixture coordenam as etapas, sem transmitir dono ou estado do
item. Esses campos passam exclusivamente pelos hooks do loader. O cliente
dropa pela interface mobile; o host pega com `Player.PickupItem`, dropa pelo
item selecionado e o cliente pega novamente. Uma alteração somente no
contador do inventário deve ser detectada e chegar ao servidor. Depois, um
item de baú vai e volta pela mensagem nativa 32. O fixture prepara um baú
vazio no cliente antes do envio: o protocolo vanilla ignora a mensagem 32
quando o baú ainda não existe naquele cliente.

## Restaurar o ambiente

Execute ao terminar, inclusive após falha:

```powershell
python tools/tests/moditemdata/device.py restore --device 127.0.0.1:16384
python tools/tests/moditemdata/device.py restore --device 127.0.0.1:16416
adb -s 127.0.0.1:16384 forward --remove tcp:7777
```

`restore` encerra o jogo de teste, restaura as preferências anteriores e
remove somente a pasta deste fixture, depois de verificar seu caminho. As
cópias dos saves e os logs permanecem para diagnóstico. O APK instalado
permanece sendo o build testado.

Resultados da execução: [RESULTADOS.md](RESULTADOS.md).
