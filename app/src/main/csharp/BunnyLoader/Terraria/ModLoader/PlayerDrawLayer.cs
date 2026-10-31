using System.Collections;
using System.Collections.Generic;
using Terraria.DataStructures;

namespace Terraria.ModLoader;

public abstract class PlayerDrawLayer : ModType
{
    public abstract class Transformation
    {
        public virtual Transformation Parent => null!;

        protected abstract void PreDraw(ref PlayerDrawSet drawInfo);

        protected abstract void PostDraw(ref PlayerDrawSet drawInfo);

        public void PreDrawRecursive(ref PlayerDrawSet drawInfo)
        {
            Parent?.PreDrawRecursive(ref drawInfo);
            PreDraw(ref drawInfo);
        }

        public void PostDrawRecursive(ref PlayerDrawSet drawInfo)
        {
            PostDraw(ref drawInfo);
            Parent?.PostDrawRecursive(ref drawInfo);
        }
    }

    public abstract class Position { }

    public sealed class Between : Position
    {
        public PlayerDrawLayer Layer1 { get; } = null!;
        public PlayerDrawLayer Layer2 { get; } = null!;

        public Between(PlayerDrawLayer layer1, PlayerDrawLayer layer2)
        {
            Layer1 = layer1;
            Layer2 = layer2;
        }

        public Between() { }
    }

    public class Multiple : Position, IEnumerable
    {
        public delegate bool Condition(PlayerDrawSet drawInfo);

        public IList<(Between, Condition)> Positions { get; } = new List<(Between, Condition)>();

        public void Add(Between position, Condition condition) => Positions.Add((position, condition));

        public IEnumerator GetEnumerator() => Positions.GetEnumerator();
    }

    public class BeforeParent : Position
    {
        public PlayerDrawLayer Parent { get; }

        public BeforeParent(PlayerDrawLayer parent) => Parent = parent;
    }

    public class AfterParent : Position
    {
        public PlayerDrawLayer Parent { get; }

        public AfterParent(PlayerDrawLayer parent) => Parent = parent;
    }

    readonly List<PlayerDrawLayer> childrenBefore = new();
    readonly List<PlayerDrawLayer> childrenAfter = new();

    public bool Visible { get; private set; } = true;

    public virtual Transformation Transform => null!;

    public IReadOnlyList<PlayerDrawLayer> ChildrenBefore => childrenBefore;

    public IReadOnlyList<PlayerDrawLayer> ChildrenAfter => childrenAfter;

    public virtual bool IsHeadLayer => false;

    public void Hide() => Visible = false;

    internal void AddChildBefore(PlayerDrawLayer child) => childrenBefore.Add(child);

    internal void AddChildAfter(PlayerDrawLayer child) => childrenAfter.Add(child);

    internal void ClearChildren()
    {
        childrenBefore.Clear();
        childrenAfter.Clear();
    }

    public virtual bool GetDefaultVisibility(PlayerDrawSet drawInfo) => true;

    public abstract Position GetDefaultPosition();

    internal void ResetVisibility(PlayerDrawSet drawInfo)
    {
        foreach (PlayerDrawLayer child in childrenBefore) child.ResetVisibility(drawInfo);
        Visible = GetDefaultVisibility(drawInfo);
        foreach (PlayerDrawLayer child in childrenAfter) child.ResetVisibility(drawInfo);
    }

    protected abstract void Draw(ref PlayerDrawSet drawInfo);

    public void DrawWithTransformationAndChildren(ref PlayerDrawSet drawInfo)
    {
        if (!Visible) return;
        Transform?.PreDrawRecursive(ref drawInfo);
        foreach (PlayerDrawLayer child in childrenBefore) child.DrawWithTransformationAndChildren(ref drawInfo);
        Draw(ref drawInfo);
        foreach (PlayerDrawLayer child in childrenAfter) child.DrawWithTransformationAndChildren(ref drawInfo);
        Transform?.PostDrawRecursive(ref drawInfo);
    }

    protected sealed override void Register() => PlayerDrawLayerLoader.Add(this);

    public sealed override void SetupContent() => SetStaticDefaults();

    public override string ToString() => Name;
}

[Autoload(false)]
public class PlayerDrawLayerSlot : PlayerDrawLayer
{
    readonly int slot;

    public PlayerDrawLayer Layer { get; }

    public Multiple.Condition Condition { get; }

    public override string Name => $"{Layer.Name}_slot{slot}";

    internal PlayerDrawLayerSlot(PlayerDrawLayer layer, Multiple.Condition condition, int slot)
    {
        Layer = layer;
        Mod = layer.Mod;
        Condition = condition;
        this.slot = slot;
        AddChildAfter(layer);
    }

    public override Position GetDefaultPosition() => throw new System.NotImplementedException();

    protected override void Draw(ref PlayerDrawSet drawInfo) { }

    public override bool GetDefaultVisibility(PlayerDrawSet drawInfo) => Condition(drawInfo);
}
