// A base de GlobalItem, GlobalNPC e GlobalProjectile: código de mod para as
// entidades do JOGO (e as de mod).
class GlobalType {
    // true: cada entidade ganha a própria cópia (item.GetGlobalItem(Classe)).
    get InstancePerEntity() { return false; }

    // lateInstantiation: true depois do SetDefaults (o tipo já é o final).
    AppliesToEntity(entity, lateInstantiation) { return true; }
    Clone(from, to) { return Entities.Clone(this); }
    NewInstance(target) { return Entities.Clone(this); }

    SetStaticDefaults() {}
    SetDefaults(entity) {}
    AddRecipeGroups() {}
    AddRecipes() {}
    PostSetupContent() {}
}
