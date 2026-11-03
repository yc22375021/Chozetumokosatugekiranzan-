namespace Chozetumokosatugekiranzan;

internal enum RoundPhase { Title, Fighting, Result }
internal enum FighterAction { Idle, Walking, Crouching, Attack, Hitstun, Guardstun, Knockout }
internal enum RoundResult { None, PlayerWin, CpuWin, Draw }

internal readonly record struct FrameInput(
    int Horizontal = 0,
    bool Down = false,
    bool JumpPressed = false,
    bool LightPressed = false,
    bool HeavyPressed = false,
    bool StartPressed = false,
    bool MediumPressed = false,
    bool GuardHeld = false,
    bool UpHeld = false);

internal readonly record struct RectF(float X, float Y, float Width, float Height)
{
    public float Right => X + Width;
    public float Bottom => Y + Height;
    public bool Intersects(RectF other) =>
        Width > 0 && Height > 0 && other.Width > 0 && other.Height > 0 &&
        X < other.Right && Right > other.X && Y < other.Bottom && Bottom > other.Y;
}

internal sealed class Fighter(string name, bool isCpu, float x)
{
    public string Name { get; } = name;
    public bool IsCpu { get; } = isCpu;
    public float X { get; set; } = x;
    public float Altitude { get; set; }
    public float VerticalSpeed { get; set; }
    public int AirborneFrames { get; set; }
    public int Facing { get; set; } = 1;
    public bool Grounded { get; set; } = true;
    public bool Crouching { get; set; }
    public bool IsGuarding { get; set; }
    public int Health { get; set; } = GameData.MaximumHealth;
    public int Meter { get; set; }
    public FighterAction Action { get; set; }
    public int CurrentMove { get; set; }
    public int MoveFrame { get; set; }
    public int StunFramesRemaining { get; set; }
    public bool AttackHasHit { get; set; }
    public FrameInput CurrentInput { get; set; }
    public FrameInput PreviousInput { get; set; }
    public SwordState SwordState { get; set; } = SwordState.Sheathed;
    public MoveExecution? Execution { get; set; }
    public CommandInput Commands { get; } = new();
    public MoveRequestResult LastMoveResult { get; set; }
    public int LastRequestedMoveId { get; set; }
    public int BreathFramesRemaining { get; set; }
    public int EvadeStopRemaining { get; set; }
    public bool CounterTipContact { get; set; }
    public int InputFrame { get; set; }
    public int BodyHeight => Crouching ? GameData.CrouchingHeight : GameData.StandingHeight;
    public RectF PushBox => new(X - GameData.FighterWidth / 2f,
        GameData.GroundY - Altitude - BodyHeight, GameData.FighterWidth, BodyHeight);
    public RectF HurtBox => new(X - GameData.FighterWidth * 0.375f,
        GameData.GroundY - Altitude - BodyHeight + 4, GameData.FighterWidth * 0.75f, BodyHeight - 4);
}

internal sealed class CpuController(int seed)
{
    private readonly Random _random = new(seed);
    private int _nextDecisionFrame;
    public FrameInput GetInput(int frame, Fighter cpu, Fighter opponent)
    {
        var approach = Math.Abs(opponent.X - cpu.X) > GameData.CpuApproachDistance;
        if (frame < _nextDecisionFrame)
            return new(Horizontal: approach ? Math.Sign(opponent.X - cpu.X) : 0);
        _nextDecisionFrame = frame + _random.Next(GameData.CpuDecisionMinFrames, GameData.CpuDecisionMaxFrames + 1);
        if (approach) return new(Horizontal: Math.Sign(opponent.X - cpu.X));
        return _random.Next(6) switch
        {
            0 => new(GuardHeld: true),
            1 => new(GuardHeld: true, Down: true),
            2 => new(MediumPressed: true),
            3 when cpu.SwordState == SwordState.Sheathed => new(HeavyPressed: true),
            4 when cpu.SwordState == SwordState.Drawn => new(LightPressed: true, HeavyPressed: true),
            _ => new(LightPressed: true)
        };
    }
}

internal sealed class Battle
{
    public Battle() => CommandDefinitions.Validate(MoveRegistry.Default);

    private CpuController _cpuController = new(1337);
    private int _remainingFrames;
    public RoundPhase Phase { get; private set; } = RoundPhase.Title;
    public RoundResult Result { get; private set; }
    public Fighter Player { get; private set; } = new("P1", false, 250);
    public Fighter Cpu { get; private set; } = new("CPU", true, 710);
    public int Frame { get; private set; }
    public int RemainingFrames => _remainingFrames;
    public int RemainingSeconds => Math.Max(0, (int)Math.Ceiling(_remainingFrames / (double)GameData.FramesPerSecond));

    public void StartRound()
    {
        Player = new("P1", false, 250);
        Cpu = new("CPU", true, 710) { Facing = -1 };
        _cpuController = new(1337);
        Frame = 0;
        _remainingFrames = GameData.MatchTimeSeconds * GameData.FramesPerSecond;
        Result = RoundResult.None;
        Phase = RoundPhase.Fighting;
    }

    public void Tick(FrameInput playerInput)
    {
        if (Phase != RoundPhase.Fighting)
        {
            if (playerInput.StartPressed) StartRound();
            return;
        }
        Frame++;
        Player.Facing = Player.X <= Cpu.X ? 1 : -1;
        Cpu.Facing = -Player.Facing;
        var cpuInput = _cpuController.GetInput(Frame, Cpu, Player);
        Player.PreviousInput = Player.CurrentInput;
        Cpu.PreviousInput = Cpu.CurrentInput;
        Player.CurrentInput = playerInput;
        Cpu.CurrentInput = cpuInput;
        MoveController.ProcessInput(Player, playerInput, Frame);
        MoveController.ProcessInput(Cpu, cpuInput, Frame);
        if (Player.EvadeStopRemaining > 0 || Cpu.EvadeStopRemaining > 0)
        {
            Player.EvadeStopRemaining = Math.Max(0, Player.EvadeStopRemaining - 1);
            Cpu.EvadeStopRemaining = Math.Max(0, Cpu.EvadeStopRemaining - 1);
            return;
        }
        _remainingFrames = Math.Max(0, _remainingFrames - 1);
        MoveFighter(Player);
        MoveFighter(Cpu);
        AdvanceAction(Player);
        AdvanceAction(Cpu);
        ResolvePushApart(Player, Cpu);
        ResolveAttacks();
        if (Player.Health <= 0 || Cpu.Health <= 0) FinishRound();
        else if (_remainingFrames == 0)
        {
            Result = Player.Health == Cpu.Health ? RoundResult.Draw :
                Player.Health > Cpu.Health ? RoundResult.PlayerWin : RoundResult.CpuWin;
            Phase = RoundPhase.Result;
        }
    }

    public void ClearTransientInput()
    {
        Player.Commands.Clear();
        Cpu.Commands.Clear();
        Player.CurrentInput = default;
        Cpu.CurrentInput = default;
        Player.PreviousInput = default;
        Cpu.PreviousInput = default;
    }

    public static bool TryStartMove(Fighter fighter, int moveId) =>
        MoveController.Request(fighter, moveId) == MoveRequestResult.Started;

    public static RectF GetAttackBox(Fighter fighter)
    {
        var active = MoveController.Active(fighter);
        if (active is null || !active.IsAttack) return default;
        return active.AttackBox(fighter);
    }

    public static RectF GetCounterTip(Fighter fighter)
    {
        return IsActive(fighter) ? MoveController.Active(fighter)!.CounterTip(fighter) : default;
    }

    public static void ResolvePushApart(Fighter left, Fighter right)
    {
        if (!left.PushBox.Intersects(right.PushBox)) return;
        if (left.X > right.X) (left, right) = (right, left);
        var distance = (left.PushBox.Width + right.PushBox.Width) / 2f;
        var overlap = distance - (right.X - left.X);
        if (overlap <= 0) return;
        left.X -= overlap / 2f;
        right.X += overlap / 2f;
        ClampToStage(left);
        ClampToStage(right);
        var remaining = distance - (right.X - left.X);
        if (remaining > 0)
        {
            var leftCanMove = left.X > GameData.StageLeft + GameData.FighterWidth / 2f;
            var rightCanMove = right.X < GameData.StageRight - GameData.FighterWidth / 2f;
            if (leftCanMove && rightCanMove) { left.X -= remaining / 2f; right.X += remaining / 2f; }
            else if (leftCanMove) left.X -= remaining;
            else if (rightCanMove) right.X += remaining;
            ClampToStage(left);
            ClampToStage(right);
        }
    }

    private static void MoveFighter(Fighter fighter)
    {
        var canMove = MoveController.CanMove(fighter);
        var guarding = fighter.Grounded && fighter.CurrentInput.GuardHeld;
        fighter.IsGuarding = (guarding && ((canMove && fighter.Action != FighterAction.Attack) ||
            fighter.Action == FighterAction.Guardstun)) ||
            MoveController.Active(fighter)?.Guards == true;
        if (canMove && fighter.Grounded)
        {
            var input = fighter.CurrentInput;
            var previous = fighter.PreviousInput;
            var attackPending = fighter.Commands.Count > 0;
            var cancel = !attackPending && fighter.MoveFrame > 0 && (input.JumpPressed ||
                (input.Down && !previous.Down) || input.Horizontal != previous.Horizontal ||
                (input.GuardHeld && !previous.GuardHeld));
            if (fighter.Action != FighterAction.Attack || cancel)
            {
                if (fighter.Action == FighterAction.Attack) MoveController.End(fighter);
                fighter.IsGuarding = guarding;
                if (input.JumpPressed && !attackPending)
                {
                    fighter.Grounded = false;
                    fighter.Crouching = false;
                    fighter.VerticalSpeed = GameData.JumpSpeed;
                    fighter.AirborneFrames = 0;
                    fighter.Action = FighterAction.Idle;
                }
                else
                {
                    fighter.Crouching = input.Down && !fighter.IsGuarding;
                    if (fighter.IsGuarding) fighter.Action = FighterAction.Idle;
                    else if (fighter.Crouching) fighter.Action = FighterAction.Crouching;
                    else if (input.Horizontal != 0)
                    {
                        fighter.X += Math.Clamp(input.Horizontal, -1, 1) * GameData.WalkSpeed * SpeedMultiplier(fighter);
                        fighter.Action = FighterAction.Walking;
                    }
                    else fighter.Action = FighterAction.Idle;
                }
            }
        }
        var move = MoveController.Active(fighter);
        if (move is not null && fighter.MoveFrame < move.Performance.StartupFrames + move.Performance.ActiveFrames)
            fighter.X += fighter.Facing * move.ForwardSpeed * SpeedMultiplier(fighter);
        if (!fighter.Grounded)
        {
            fighter.AirborneFrames++;
            fighter.Altitude += fighter.VerticalSpeed;
            fighter.VerticalSpeed -= GameData.Gravity;
            if (fighter.Altitude <= 0)
            {
                fighter.Altitude = 0;
                fighter.VerticalSpeed = 0;
                fighter.Grounded = true;
                fighter.Crouching = false;
                fighter.AirborneFrames = 0;
            }
        }
        ClampToStage(fighter);
    }

    private static float SpeedMultiplier(Fighter fighter) => fighter.BreathFramesRemaining > 0
        ? CombatTuning.BreathSpeedMultiplier : 1;

    internal static void AdvanceAction(Fighter fighter)
    {
        if (fighter.BreathFramesRemaining > 0) fighter.BreathFramesRemaining--;
        if (MoveController.Active(fighter) is { } move)
        {
            fighter.MoveFrame++;
            move.Tick(fighter);
            if (fighter.MoveFrame >= move.Performance.TotalFrames) MoveController.End(fighter);
        }
        else if (fighter.Action is FighterAction.Hitstun or FighterAction.Guardstun)
        {
            if (--fighter.StunFramesRemaining <= 0) MoveController.End(fighter);
        }
    }

    private static bool IsActive(Fighter fighter)
    {
        var move = MoveController.Active(fighter);
        return move is { IsAttack: true } && !fighter.AttackHasHit &&
            fighter.MoveFrame >= move.Performance.StartupFrames &&
            fighter.MoveFrame < move.Performance.StartupFrames + move.Performance.ActiveFrames;
    }

    internal void ResolveAttacks()
    {
        var playerHits = IsActive(Player) && GetAttackBox(Player).Intersects(Cpu.HurtBox);
        var cpuHits = IsActive(Cpu) && GetAttackBox(Cpu).Intersects(Player.HurtBox);
        Player.CounterTipContact = IsActive(Cpu) && GetCounterTip(Player).Intersects(GetAttackBox(Cpu));
        Cpu.CounterTipContact = IsActive(Player) && GetCounterTip(Cpu).Intersects(GetAttackBox(Player));
        // Snapshot both attacks before applying either hit, preserving simultaneous trades.
        var playerMove = MoveController.Active(Player);
        var cpuMove = MoveController.Active(Cpu);
        var playerDamage = Player.Execution?.DamageMultiplier ?? 1;
        var cpuDamage = Cpu.Execution?.DamageMultiplier ?? 1;
        var playerInvincible = playerMove?.Invincible(Player) == true;
        var cpuInvincible = cpuMove?.Invincible(Cpu) == true;
        if (playerHits && cpuInvincible)
        {
            cpuMove!.ThreatAvoided(Cpu);
            Player.AttackHasHit = true;
            playerHits = false;
        }
        if (cpuHits && playerInvincible)
        {
            playerMove!.ThreatAvoided(Player);
            Cpu.AttackHasHit = true;
            cpuHits = false;
        }
        if (playerHits) ApplyHit(Player, Cpu, playerMove!.Performance, playerDamage);
        if (cpuHits) ApplyHit(Cpu, Player, cpuMove!.Performance, cpuDamage);
    }

    private static void ApplyHit(Fighter attacker, Fighter defender, MoveDefinition move, float multiplier)
    {
        attacker.AttackHasHit = true;
        var armor = MoveController.Active(defender)?.SuperArmor == true;
        if (defender.IsGuarding)
        {
            defender.Health = Math.Max(0, defender.Health - move.GuardDamage);
            MoveController.End(defender);
            defender.Action = FighterAction.Guardstun;
            defender.StunFramesRemaining = move.GuardstunFrames;
            defender.Meter = Math.Min(GameData.MaximumMeter, defender.Meter + 3);
            attacker.Meter = Math.Min(GameData.MaximumMeter, attacker.Meter + move.MeterGain / 2);
        }
        else
        {
            defender.Health = Math.Max(0, defender.Health - (int)MathF.Round(move.Damage * multiplier));
            if (!armor)
            {
                MoveController.End(defender);
                defender.Action = FighterAction.Hitstun;
                defender.StunFramesRemaining = move.HitstunFrames;
            }
            defender.Meter = Math.Min(GameData.MaximumMeter, defender.Meter + 5);
            attacker.Meter = Math.Min(GameData.MaximumMeter, attacker.Meter + move.MeterGain);
            if (!defender.Grounded) defender.VerticalSpeed = Math.Max(defender.VerticalSpeed, 3.5f);
        }
        defender.Crouching = false;
        if (!armor) defender.X += attacker.Facing * move.Pushback;
        ClampToStage(defender);
    }

    private void FinishRound()
    {
        Player.Action = Player.Health <= 0 ? FighterAction.Knockout : Player.Action;
        Cpu.Action = Cpu.Health <= 0 ? FighterAction.Knockout : Cpu.Action;
        Result = Player.Health <= 0 && Cpu.Health <= 0 ? RoundResult.Draw :
            Player.Health <= 0 ? RoundResult.CpuWin : RoundResult.PlayerWin;
        Phase = RoundPhase.Result;
    }

    private static void ClampToStage(Fighter fighter) => fighter.X = Math.Clamp(fighter.X,
        GameData.StageLeft + GameData.FighterWidth / 2f, GameData.StageRight - GameData.FighterWidth / 2f);
}
