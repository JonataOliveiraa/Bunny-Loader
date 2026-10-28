using Terraria.ModLoader;

namespace CsGlobals.Common.GlobalItems;

[Autoload(false)]
public class FieldsWithoutInstanceGlobal : GlobalItem
{
    public int Shared;
}
