namespace Chozetumokosatugekiranzan;

internal sealed class ToppaMove : CombatMove
{
    public override MoveDefinition Performance { get; } = Prototype("DIRECTIONAL THRUST", 11, 6, 4, 16, 95);
    public override SwordState? RequiredState => SwordState.Drawn;
    public override RectF AttackBox(Fighter fighter)
    {
        var execution = fighter.Execution!;
        var startY = GameData.GroundY - fighter.Altitude - fighter.BodyHeight + 24 + Performance.HitHeight / 2f;
        var endX = fighter.X + execution.DirectionX * Performance.Range;
        var endY = startY - execution.DirectionY * Performance.Range;
        return new(Math.Min(fighter.X, endX) - 8, Math.Min(startY, endY) - 8,
            Math.Abs(endX - fighter.X) + 16, Math.Abs(endY - startY) + 16);
    }
    public override void Start(Fighter fighter)
    {
        fighter.Execution!.DirectionX = fighter.Facing;
    }
}
