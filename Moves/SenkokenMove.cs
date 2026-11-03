namespace Chozetumokosatugekiranzan;

internal sealed class SenkokenMove : CombatMove
{
    public override MoveDefinition Performance => default;
    public override SwordState? RequiredState => SwordState.Drawn;
    public override bool Implemented => false;
}
