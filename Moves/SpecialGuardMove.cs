namespace Chozetumokosatugekiranzan;

internal sealed class SpecialGuardMove : CombatMove
{
    public override MoveDefinition Performance { get; } = Prototype("SPECIAL GUARD", 0, 1, 0, 17, 0);
    public override bool IsAttack => false;
    public override bool Guards => true;
    public override void Start(Fighter fighter) => fighter.SwordState = SwordState.Sheathed;
}
