using System.Collections.Generic;
using System.Linq;
using Terraria.ModLoader;
using static Terraria.ModLoader.PlayerDrawLayer;

namespace Terraria.DataStructures;

[Autoload(false)]
internal sealed class VanillaPlayerDrawTransform : Transformation
{
    public delegate void LayerFunction(ref PlayerDrawSet info);

    readonly LayerFunction preDraw, postDraw;
    readonly Transformation parent;

    public VanillaPlayerDrawTransform(LayerFunction preDraw, LayerFunction postDraw, Transformation parent = null!)
    {
        this.preDraw = preDraw;
        this.postDraw = postDraw;
        this.parent = parent;
    }

    public override Transformation Parent => parent;

    protected override void PreDraw(ref PlayerDrawSet drawInfo) => preDraw(ref drawInfo);

    protected override void PostDraw(ref PlayerDrawSet drawInfo) => postDraw(ref drawInfo);
}

[Autoload(false)]
internal sealed class VanillaPlayerDrawLayer : PlayerDrawLayer
{
    public delegate void DrawFunc(ref PlayerDrawSet info);

    public delegate bool Condition(PlayerDrawSet info);

    readonly string name;
    readonly DrawFunc drawFunc;
    readonly Transformation transform;
    readonly bool isHeadLayer;
    readonly Condition? condition;
    readonly Position? position;

    public VanillaPlayerDrawLayer(string name, DrawFunc drawFunc, Transformation transform = null!, bool isHeadLayer = false, Condition? condition = null, Position? position = null)
    {
        this.name = name;
        this.drawFunc = drawFunc;
        this.transform = transform;
        this.isHeadLayer = isHeadLayer;
        this.condition = condition;
        this.position = position;
    }

    public override string Name => name;

    public override Transformation Transform => transform;

    public override bool IsHeadLayer => isHeadLayer;

    public override bool GetDefaultVisibility(PlayerDrawSet drawInfo) => condition?.Invoke(drawInfo) ?? true;

    public override Position GetDefaultPosition()
    {
        if (position != null) return position;
        IReadOnlyList<PlayerDrawLayer> fixedLayers = PlayerDrawLayers.FixedVanillaLayers;
        int index = 0;
        while (fixedLayers[index] != this) index++;
        return new Between(index > 0 ? fixedLayers[index - 1] : null!, index < fixedLayers.Count - 1 ? fixedLayers[index + 1] : null!);
    }

    protected override void Draw(ref PlayerDrawSet drawInfo) => drawFunc(ref drawInfo);
}

public readonly partial struct PlayerDrawLayers
{
    static void TorsoPlus(ref PlayerDrawSet info) => DrawPlayer_extra_TorsoPlus(info);

    static void TorsoMinus(ref PlayerDrawSet info) => DrawPlayer_extra_TorsoMinus(info);

    static void MountPlus(ref PlayerDrawSet info) => DrawPlayer_extra_MountPlus(info);

    static void MountMinus(ref PlayerDrawSet info) => DrawPlayer_extra_MountMinus(info);

    static bool Robed(PlayerDrawSet info) => info.drawPlayer.wearsRobe && info.drawPlayer.body != 166;

    static void TorsoWhen(ref PlayerDrawSet info, bool torso, VanillaPlayerDrawLayer.DrawFunc draw)
    {
        if (torso) TorsoPlus(ref info);
        draw(ref info);
        if (torso) TorsoMinus(ref info);
    }

    public static readonly Transformation TorsoGroup = new VanillaPlayerDrawTransform(TorsoPlus, TorsoMinus);

    public static readonly Transformation MountGroup = new VanillaPlayerDrawTransform(MountPlus, MountMinus, TorsoGroup);

    public static readonly PlayerDrawLayer JimsCloak = new VanillaPlayerDrawLayer(nameof(JimsCloak), (ref PlayerDrawSet i) => DrawPlayer_01_2_JimsCloak(i), TorsoGroup);

    public static readonly PlayerDrawLayer MountBack = new VanillaPlayerDrawLayer(nameof(MountBack), (ref PlayerDrawSet i) => DrawPlayer_02_MountBehindPlayer(i));

    public static readonly PlayerDrawLayer Carpet = new VanillaPlayerDrawLayer(nameof(Carpet), (ref PlayerDrawSet i) => DrawPlayer_03_Carpet(i));

    public static readonly PlayerDrawLayer PortableStool = new VanillaPlayerDrawLayer(nameof(PortableStool), (ref PlayerDrawSet i) => DrawPlayer_03_PortableStool(i));

    public static readonly PlayerDrawLayer ElectrifiedDebuffBack = new VanillaPlayerDrawLayer(nameof(ElectrifiedDebuffBack), (ref PlayerDrawSet i) => DrawPlayer_04_ElectrifiedDebuffBack(i), TorsoGroup);

    public static readonly PlayerDrawLayer ForbiddenSetRing = new VanillaPlayerDrawLayer(nameof(ForbiddenSetRing), (ref PlayerDrawSet i) => DrawPlayer_05_ForbiddenSetRing(i), TorsoGroup);

    public static readonly PlayerDrawLayer SafemanSun = new VanillaPlayerDrawLayer(nameof(SafemanSun), (ref PlayerDrawSet i) => DrawPlayer_05_2_SafemanSun(i), TorsoGroup);

    public static readonly PlayerDrawLayer WebbedDebuffBack = new VanillaPlayerDrawLayer(nameof(WebbedDebuffBack), (ref PlayerDrawSet i) => DrawPlayer_06_WebbedDebuffBack(i), TorsoGroup);

    public static readonly PlayerDrawLayer LeinforsHairShampoo = new VanillaPlayerDrawLayer(nameof(LeinforsHairShampoo), (ref PlayerDrawSet i) => DrawPlayer_07_LeinforsHairShampoo(i), TorsoGroup, isHeadLayer: true);

    public static readonly PlayerDrawLayer Backpacks = new VanillaPlayerDrawLayer(nameof(Backpacks), (ref PlayerDrawSet i) => DrawPlayer_08_Backpacks(i));

    public static readonly PlayerDrawLayer Tails = new VanillaPlayerDrawLayer(nameof(Tails), (ref PlayerDrawSet i) => DrawPlayer_08_1_Tails(i), TorsoGroup);

    public static readonly PlayerDrawLayer Wings = new VanillaPlayerDrawLayer(nameof(Wings), (ref PlayerDrawSet i) => DrawPlayer_09_Wings(i));

    public static readonly PlayerDrawLayer HairBack = new VanillaPlayerDrawLayer(nameof(HairBack), (ref PlayerDrawSet i) => DrawPlayer_01_BackHair(i), TorsoGroup, isHeadLayer: true);

    public static readonly PlayerDrawLayer BackAcc = new VanillaPlayerDrawLayer(nameof(BackAcc), (ref PlayerDrawSet i) => DrawPlayer_10_BackAcc(i), TorsoGroup);

    public static readonly PlayerDrawLayer HeadBack = new VanillaPlayerDrawLayer(nameof(HeadBack), (ref PlayerDrawSet i) => DrawPlayer_01_3_BackHead(i), TorsoGroup, isHeadLayer: true);

    public static readonly PlayerDrawLayer BalloonAcc = new VanillaPlayerDrawLayer(nameof(BalloonAcc), (ref PlayerDrawSet i) => DrawPlayer_11_Balloons(i));

    public static readonly PlayerDrawLayer ArmorBackCoat = new VanillaPlayerDrawLayer(nameof(ArmorBackCoat), (ref PlayerDrawSet i) => DrawPlayer_13_ArmorBackCoat(i));

    public static readonly PlayerDrawLayer Skin = new VanillaPlayerDrawLayer(nameof(Skin), (ref PlayerDrawSet i) => DrawPlayer_12_Skin(i));

    public static readonly PlayerDrawLayer Leggings = new VanillaPlayerDrawLayer(nameof(Leggings), (ref PlayerDrawSet i) => DrawPlayer_13_Leggings(i), condition: i => !Robed(i));

    public static readonly PlayerDrawLayer Shoes = new VanillaPlayerDrawLayer(nameof(Shoes), (ref PlayerDrawSet i) => DrawPlayer_14_Shoes(i));

    public static readonly PlayerDrawLayer Robe = new VanillaPlayerDrawLayer(nameof(Robe), (ref PlayerDrawSet i) => DrawPlayer_13_Leggings(i), condition: Robed);

    public static readonly PlayerDrawLayer SkinLongCoat = new VanillaPlayerDrawLayer(nameof(SkinLongCoat), (ref PlayerDrawSet i) => DrawPlayer_15_SkinLongCoat(i), TorsoGroup);

    public static readonly PlayerDrawLayer ArmorLongCoat = new VanillaPlayerDrawLayer(nameof(ArmorLongCoat), (ref PlayerDrawSet i) => DrawPlayer_16_ArmorLongCoat(i), TorsoGroup);

    public static readonly PlayerDrawLayer Torso = new VanillaPlayerDrawLayer(nameof(Torso), (ref PlayerDrawSet i) => DrawPlayer_17_Torso(i), TorsoGroup);

    public static readonly PlayerDrawLayer OffhandAcc = new VanillaPlayerDrawLayer(nameof(OffhandAcc), (ref PlayerDrawSet i) => DrawPlayer_18_OffhandAcc(i), TorsoGroup);

    public static readonly PlayerDrawLayer WaistAcc = new VanillaPlayerDrawLayer(nameof(WaistAcc), (ref PlayerDrawSet i) => DrawPlayer_19_WaistAcc(i), TorsoGroup);

    public static readonly PlayerDrawLayer NeckAcc = new VanillaPlayerDrawLayer(nameof(NeckAcc), (ref PlayerDrawSet i) => DrawPlayer_20_NeckAcc(i), TorsoGroup);

    public static readonly PlayerDrawLayer Head = new VanillaPlayerDrawLayer(nameof(Head), (ref PlayerDrawSet i) => DrawPlayer_21_Head(i), TorsoGroup, isHeadLayer: true, condition: i => !i.mountHandlesHeadDraw);

    public static readonly PlayerDrawLayer Magiluminescence = new VanillaPlayerDrawLayer(nameof(Magiluminescence), (ref PlayerDrawSet i) => DrawPlayer_21_1_Magiluminescence(i), TorsoGroup);

    public static readonly PlayerDrawLayer FaceAcc = new VanillaPlayerDrawLayer(nameof(FaceAcc), (ref PlayerDrawSet i) => DrawPlayer_22_FaceAcc(i), TorsoGroup, isHeadLayer: true, condition: i => !i.mountHandlesHeadDraw);

    public static readonly PlayerDrawLayer MountFront = new VanillaPlayerDrawLayer(nameof(MountFront), (ref PlayerDrawSet i) => DrawPlayer_23_MountFront(i), TorsoGroup);

    public static readonly PlayerDrawLayer Pulley = new VanillaPlayerDrawLayer(nameof(Pulley), (ref PlayerDrawSet i) => DrawPlayer_24_Pulley(i), TorsoGroup);

    public static readonly PlayerDrawLayer JimsDroneRadio = new VanillaPlayerDrawLayer(nameof(JimsDroneRadio), (ref PlayerDrawSet i) => DrawPlayer_JimsDroneRadio(i), TorsoGroup);

    public static readonly PlayerDrawLayer FrontAccBack = new VanillaPlayerDrawLayer(nameof(FrontAccBack), (ref PlayerDrawSet i) => DrawPlayer_32_FrontAcc_BackPart(i), TorsoGroup);

    public static readonly PlayerDrawLayer Shield = new VanillaPlayerDrawLayer(nameof(Shield), (ref PlayerDrawSet i) => DrawPlayer_25_Shield(i), TorsoGroup);

    public static readonly PlayerDrawLayer SolarShield = new VanillaPlayerDrawLayer(nameof(SolarShield), (ref PlayerDrawSet i) => DrawPlayer_26_SolarShield(i), MountGroup);

    public static readonly PlayerDrawLayer ArmOverItem = new VanillaPlayerDrawLayer(nameof(ArmOverItem), (ref PlayerDrawSet i) => DrawPlayer_28_ArmOverItem(i), TorsoGroup);

    public static readonly PlayerDrawLayer HandOnAcc = new VanillaPlayerDrawLayer(nameof(HandOnAcc), (ref PlayerDrawSet i) => DrawPlayer_29_OnhandAcc(i), TorsoGroup);

    public static readonly PlayerDrawLayer BladedGlove = new VanillaPlayerDrawLayer(nameof(BladedGlove), (ref PlayerDrawSet i) => DrawPlayer_30_BladedGlove(i), TorsoGroup);

    public static readonly PlayerDrawLayer ProjectileOverArm = new VanillaPlayerDrawLayer(nameof(ProjectileOverArm), (ref PlayerDrawSet i) => DrawPlayer_31_ProjectileOverArm(i));

    public static readonly PlayerDrawLayer FrozenOrWebbedDebuff = new VanillaPlayerDrawLayer(nameof(FrozenOrWebbedDebuff), (ref PlayerDrawSet i) => DrawPlayer_33_FrozenOrWebbedDebuff(i));

    public static readonly PlayerDrawLayer ElectrifiedDebuffFront = new VanillaPlayerDrawLayer(nameof(ElectrifiedDebuffFront), (ref PlayerDrawSet i) => DrawPlayer_34_ElectrifiedDebuffFront(i));

    public static readonly PlayerDrawLayer IceBarrier = new VanillaPlayerDrawLayer(nameof(IceBarrier), (ref PlayerDrawSet i) => DrawPlayer_35_IceBarrier(i));

    public static readonly PlayerDrawLayer CaptureTheGem = new VanillaPlayerDrawLayer(nameof(CaptureTheGem), (ref PlayerDrawSet i) => DrawPlayer_36_CTG(i));

    public static readonly PlayerDrawLayer BeetleBuff = new VanillaPlayerDrawLayer(nameof(BeetleBuff), (ref PlayerDrawSet i) => DrawPlayer_37_BeetleBuff(i));

    public static readonly PlayerDrawLayer EyebrellaCloud = new VanillaPlayerDrawLayer(nameof(EyebrellaCloud), (ref PlayerDrawSet i) => DrawPlayer_38_EyebrellaCloud(i));

    public static readonly PlayerDrawLayer FrontAccFront = new VanillaPlayerDrawLayer(nameof(FrontAccFront),
        (ref PlayerDrawSet i) => TorsoWhen(ref i, !i.drawFrontAccInNeckAccLayer, static (ref PlayerDrawSet d) => DrawPlayer_32_FrontAcc_FrontPart(d)),
        position: new Multiple
        {
            { new Between(FaceAcc, MountFront), i => i.drawFrontAccInNeckAccLayer && !i.mountHandlesHeadDraw },
            { new Between(BladedGlove, ProjectileOverArm), i => !i.drawFrontAccInNeckAccLayer },
        });

    public static readonly PlayerDrawLayer HeldItem = new VanillaPlayerDrawLayer(nameof(HeldItem),
        (ref PlayerDrawSet i) => TorsoWhen(ref i, i.weaponDrawOrder == WeaponDrawOrder.BehindFrontArm, static (ref PlayerDrawSet d) => DrawPlayer_27_HeldItem(d)),
        position: new Multiple
        {
            { new Between(BalloonAcc, ArmorBackCoat), i => i.weaponDrawOrder == WeaponDrawOrder.BehindBackArm },
            { new Between(SolarShield, ArmOverItem), i => i.weaponDrawOrder == WeaponDrawOrder.BehindFrontArm },
            { new Between(BladedGlove, ProjectileOverArm), i => i.weaponDrawOrder == WeaponDrawOrder.OverFrontArm },
        });

    internal static IReadOnlyList<PlayerDrawLayer> FixedVanillaLayers { get; } = new[]
    {
        JimsCloak, MountBack, Carpet, PortableStool, ElectrifiedDebuffBack, ForbiddenSetRing, SafemanSun, WebbedDebuffBack,
        LeinforsHairShampoo, Backpacks, Tails, Wings, HairBack, BackAcc, HeadBack, BalloonAcc, ArmorBackCoat, Skin, Leggings, Shoes,
        Robe, SkinLongCoat, ArmorLongCoat, Torso, OffhandAcc, WaistAcc, NeckAcc, Head, Magiluminescence, FaceAcc, MountFront,
        Pulley, JimsDroneRadio, FrontAccBack, Shield, SolarShield, ArmOverItem, HandOnAcc, BladedGlove, ProjectileOverArm,
        FrozenOrWebbedDebuff, ElectrifiedDebuffFront, IceBarrier, CaptureTheGem, BeetleBuff, EyebrellaCloud,
    };

    internal static IReadOnlyList<PlayerDrawLayer> VanillaLayers { get; } = FixedVanillaLayers.Concat(new[] { FrontAccFront, HeldItem }).ToArray();

    public static PlayerDrawLayer FirstVanillaLayer => FixedVanillaLayers[0];

    public static PlayerDrawLayer LastVanillaLayer => FixedVanillaLayers[^1];

    public static Between BeforeFirstVanillaLayer => new(null!, FirstVanillaLayer);

    public static Between AfterLastVanillaLayer => new(LastVanillaLayer, null!);
}
