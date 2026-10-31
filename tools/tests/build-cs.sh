#!/usr/bin/env bash
# Compila os mods C# de teste (o .dll vai para content/ de cada um). Pede o
# SDK do .NET e o dump do jogo em refs/DummyDll (as proxies sao geradas dele).
#
#   tools/tests/build-cs.sh            # todos
#   tools/tests/build-cs.sh hookcs     # so um
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
tests=("$@")
[ ${#tests[@]} -gt 0 ] || tests=(csharphello hookcs benchcs compilebench csitems csload csassets csmodtarget csmodref csloc csinstances csglobals cssave csnet csuse csheal cspickup csdraw csopen csnetitem cstooltip csrarity csprefix csrecipe csequip csdamage csenv cscombat csarmor csplayer csplayeritems csnetplayer csplayerhurt cspvp csbuff csplayerupdate csplayerworld csfishing csjump csplayerdraw csbufftip csmaterials cshair csresource csmount csprojectile csnpc csprojhit csgrapple csprojextras csnpcai cscollide csextraai csauto csdust csgore csnpchit csnpcproj csnpccontact csnpcdraw csnpcloot csbossbar csnpcspawn cstownnpc)
for t in "${tests[@]}"; do
    proj=$(ls "$here/$t/src/"*.csproj)
    echo "==> $t"
    dotnet build "$proj" -c Release -nologo -v q
done
