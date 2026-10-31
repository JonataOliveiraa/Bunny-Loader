using System;
using BunnyLoader.Interop;
using Terraria.GameContent;

namespace Terraria.ModLoader;

public sealed class NPCInteractionDatabase
{
    public static NPCInteraction CloseButton = new DedicatedButton("CloseChat");
    public static NPCInteraction HappinessButton = new DedicatedButton("ReportHappiness");
    public static NPCInteraction HousingButton = new DedicatedButton("RequestHome");
    public static NPCInteraction PetButton = new DedicatedButton("PetAnimal");

    internal sealed class DedicatedButton : NPCInteraction
    {
        static readonly int[] Layout = { -1 };
        readonly GameClass type;
        readonly GameHandle pin;
        readonly IntPtr pointer;
        MethodRef constructor, condition, text, interaction;
        internal DedicatedButton(string name)
        {
            type = new GameClass("Terraria.GameContent", "NPCInteractions", "Actions/" + name);
            pointer = GameInterop.NewObject(type);
            pin = new GameHandle(pointer);
            Invoke(ref constructor, "void .ctor()");
        }
        long Invoke(ref MethodRef method, string signature)
        {
            ref readonly var native = ref GameInterop.Method(ref method, type, signature, Layout);
            var call = GameCall.Returning(RetKind.Int);
            call.Arg(pointer);
            call.Arg(native.Info);
            return call.Invoke(native.Fn);
        }
        public override bool Condition() => Invoke(ref condition, "bool Condition()") != 0;
        public override string GetText() => GameString.Read((IntPtr)Invoke(ref text, "string GetText()")) ?? "";
        public override void Interact() => Invoke(ref interaction, "void Interact()");
    }
}
