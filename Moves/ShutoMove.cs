namespace Chozetumokosatugekiranzan;

internal sealed class ShutoMove : CombatMove
{
    public override MoveDefinition Performance { get; } = Prototype("HAND CHOP", 5, 2, 2, 12, 38);
    public override SwordState? RequiredState => SwordState.Sheathed;
    public override int[] BranchTargets => [MoveIds.KaitenKenbu];
    public override bool CanCancel(Fighter fighter, CombatMove target) => true;
    public override bool CanCancelToMovement => true;
}
