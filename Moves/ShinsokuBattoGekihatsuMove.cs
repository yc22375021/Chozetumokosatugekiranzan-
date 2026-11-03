namespace Chozetumokosatugekiranzan;

internal sealed class ShinsokuBattoGekihatsuMove : CombatMove
{
    public override MoveDefinition Performance { get; } = Prototype("QUICK DRAW", 15, 9, 4, 17, 82);
    public override SwordState? RequiredState => SwordState.Sheathed;
    public override bool DrawsSword => true;
    public override float ForwardSpeed => 4;
}
