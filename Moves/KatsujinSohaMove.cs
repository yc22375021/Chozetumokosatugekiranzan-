namespace Chozetumokosatugekiranzan;

internal sealed class KatsujinSohaMove : CombatMove
{
    public override MoveDefinition Performance { get; } = Prototype("BLADE RUSH", 12, 7, 6, 18, 110);
    public override SwordState? RequiredState => SwordState.Drawn;
    public override float ForwardSpeed => 5;
    public override RectF CounterTip(Fighter fighter)
    {
        var box = AttackBox(fighter);
        return new(fighter.Facing > 0 ? box.Right - 12 : box.X, box.Y, 12, box.Height);
    }
    // Counter contact is exposed separately; its conditions/reward are not finalized.
}
