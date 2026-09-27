// O TileRestingInfo do tModLoader: onde e como a entidade senta ou deita.
class TileRestingInfo {
    constructor(restingEntity, anchorX, anchorY, visualOffset, targetDirection, directionOffset = 0, finalOffset = null) {
        this.RestingEntity = restingEntity;
        this.AnchorTilePosition = { X: anchorX, Y: anchorY };
        this.VisualOffset = visualOffset || Vector2.new(0, 0);
        this.TargetDirection = targetDirection;
        this.DirectionOffset = directionOffset;
        this.FinalOffset = finalOffset || Vector2.new(0, 0);
        this.ExtraInfo = { IsAToilet: false };
    }
}
