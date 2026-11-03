namespace Chozetumokosatugekiranzan;

internal sealed class NikukotsuDanzaiMove : CombatMove
{
    public override MoveDefinition Performance { get; } = Prototype("ARMORED SLASH", 16, 10, 4, 20, 85);
    public override bool BranchOnly => true;
    public override SwordState? RequiredState => SwordState.Drawn;
    public override bool SuperArmor => true;
}
