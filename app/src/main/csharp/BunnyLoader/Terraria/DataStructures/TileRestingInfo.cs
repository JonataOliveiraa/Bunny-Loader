using Microsoft.Xna.Framework;
using Terraria.GameContent;

namespace Terraria.DataStructures;

public struct TileRestingInfo
{
    public Entity RestingEntity;

    public Point AnchorTilePosition;

    public Vector2 VisualOffset;

    public int TargetDirection;

    public int DirectionOffset;

    public Vector2 FinalOffset;

    public ExtraSeatInfo ExtraInfo;

    public TileRestingInfo(Entity restingEntity, Point anchorTilePosition, Vector2 visualOffset, int targetDirection, int directionOffset = 0, Vector2 finalOffset = default, ExtraSeatInfo extraInfo = default)
    {
        RestingEntity = restingEntity;
        AnchorTilePosition = anchorTilePosition;
        VisualOffset = visualOffset;
        TargetDirection = targetDirection;
        DirectionOffset = directionOffset;
        FinalOffset = finalOffset;
        ExtraInfo = extraInfo;
    }

    public void Deconstruct(out Entity restingEntity, out Point anchorTilePosition, out Vector2 visualOffset, out int targetDirection, out int directionOffset, out Vector2 finalOffset, out ExtraSeatInfo extraInfo)
    {
        restingEntity = RestingEntity;
        anchorTilePosition = AnchorTilePosition;
        visualOffset = VisualOffset;
        targetDirection = TargetDirection;
        directionOffset = DirectionOffset;
        finalOffset = FinalOffset;
        extraInfo = ExtraInfo;
    }
}
