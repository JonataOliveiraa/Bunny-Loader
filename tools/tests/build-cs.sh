#!/usr/bin/env bash
# Compila os mods C# de teste (o .dll vai para a raiz de cada um). Pede o
# SDK do .NET e o dump do jogo em refs/DummyDll (as proxies sao geradas dele).
#
#   tools/tests/build-cs.sh            # todos
#   tools/tests/build-cs.sh hookcs     # so um
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
tests=("$@")
[ ${#tests[@]} -gt 0 ] || tests=(csharphello hookcs benchcs compilebench csitems csload csassets csmodtarget csmodref csloc csinstances csglobals cssave csnet csuse csheal cspickup csdraw csopen csnetitem cstooltip csrarity csprefix csrecipe csequip csdamage csenv cscombat csarmor csplayer csplayeritems csnetplayer csplayerhurt cspvp csbuff csplayerupdate csplayerworld csfishing csjump csplayerdraw csbufftip csmaterials cshair csresource csmount csprojectile csnpc csprojhit csgrapple csprojextras csnpcai cscollide csextraai csauto csdust csgore csnpchit csnpcproj csnpccontact csnpcdraw csnpcloot csbossbar csnpcspawn cstownnpc csnpcbestiary csemote csminion csnpcextra cstile csmodsystem csworldgen csmodsystem2 csperf csperfmod csbiome csmusic csbackground csclouds csbiomebestiary csmusicmenu cssound cschest cstileentity cswall cstree cspylon csresting cssmart cspageicons csconfig csconfigmp csconfigopen csmodcommand csachievement csangler cstouch csui csscreen csfont csexfx csexmelee csexranged csexsummon csexplayer csexitems csextiles csexworld csexcodex csexnpcs csextouch csvanillatex cstexperf cstexhearts csbanner csbiomemp csvisual csfilter csevents csstatueplace csmusicbox csapifix csblockb cstownattack csexshow csinvasion csinvasionmp csexe3 csblockf csexmp csbugs csconfiglabels csthief csapigaps csemotemenu csguide csinfodisplay csreports csinteract csradial csaltfire csalticon csaccslot csoutline csdualuse csglyph cslangs csgolf csspider cswhip csbuffbox cspaladin cssubworld cssubworld2 cssubmp cssubtrans cssubmap cssubwld cswldmake csmpperf csvisualclone cssigntext csrecipelimits)
for t in "${tests[@]}"; do
    proj=$(ls "$here/$t/src/"*.csproj)
    echo "==> $t"
    dotnet build "$proj" -c Release -nologo -v q
done
