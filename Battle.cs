namespace Chozetumokosatugekiranzan;

internal enum RoundPhase
{
    Title,
    Fighting,
    Result
}

internal enum FighterAction
{
    Idle,
    Walking,
    Crouching,
    Attack,
    Hitstun,
    Guardstun,
    Knockout
}

internal enum RelativeDirection
{
    Neutral,
    Forward,
    Back,
    Down,
    DownForward,
    DownBack,
    Up,
    UpForward,
    UpBack
}

internal enum RoundResult
{
    None,
    PlayerWin,
    CpuWin,
    Draw
}

internal readonly record struct FrameInput(
    int Horizontal = 0,
    bool Down = false,
    bool JumpPressed = false,
    bool LightPressed = false,
    bool HeavyPressed = false,
    bool DebugSpecialPressed = false,
    bool StartPressed = false);

internal readonly record struct RectF(float X, float Y, float Width, float Height)
{
    public float Right => X + Width;
    public float Bottom => Y + Height;

    public bool Intersects(RectF other) =>
        X < other.Right && Right > other.X && Y < other.Bottom && Bottom > other.Y;
}

internal sealed class Fighter
{
    public Fighter(string name, bool isCpu, float x)
    {
        Name = name;
        IsCpu = isCpu;
        X = x;
    }

    public string Name { get; }
    public bool IsCpu { get; }
    public float X { get; set; }
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
    public MoveId CurrentMove { get; set; }
    public int MoveFrame { get; set; }
    public int StunFramesRemaining { get; set; }
    public bool AttackHasHit { get; set; }
    public FrameInput CurrentInput { get; set; }
    public InputBuffer History { get; } = new(GameData.InputBufferCapacity);

    public int BodyHeight => Crouching ? GameData.CrouchingHeight : GameData.StandingHeight;

    public RectF PushBox => new(
        X - GameData.FighterWidth / 2f,
        GameData.GroundY - Altitude - BodyHeight,
        GameData.FighterWidth,
        BodyHeight);

    public RectF HurtBox => new(
        X - GameData.FighterWidth * 0.375f,
        GameData.GroundY - Altitude - BodyHeight + 4,
        GameData.FighterWidth * 0.75f,
        BodyHeight - 4);
}

internal sealed class InputBuffer
{
    private readonly int _capacity;
    private readonly List<BufferedInput> _entries = [];
    private RelativeDirection _previousDirection;
    private int _nextSerial;

    public InputBuffer(int capacity)
    {
        _capacity = capacity;
    }

    public int Count => _entries.Count;

    public static RelativeDirection ResolveDirection(int horizontal, bool down, int facing)
    {
        horizontal = Math.Clamp(horizontal, -1, 1);
        var relativeHorizontal = horizontal * (facing >= 0 ? 1 : -1);
        if (down)
        {
            return relativeHorizontal switch
            {
                > 0 => RelativeDirection.DownForward,
                < 0 => RelativeDirection.DownBack,
                _ => RelativeDirection.Down
            };
        }

        return relativeHorizontal switch
        {
            > 0 => RelativeDirection.Forward,
            < 0 => RelativeDirection.Back,
            _ => RelativeDirection.Neutral
        };
    }

    public void Sample(int frame, RelativeDirection direction, bool lightPressed, bool heavyPressed)
    {
        var changed = direction != _previousDirection;
        if (changed || lightPressed || heavyPressed)
        {
            _entries.Add(new BufferedInput(
                ++_nextSerial,
                frame,
                direction,
                changed,
                lightPressed,
                heavyPressed));
        }

        _previousDirection = direction;
        if (_entries.Count > _capacity)
        {
            _entries.RemoveRange(0, _entries.Count - _capacity);
        }
    }

    public bool TryConsumeQuarterCircle(int frame, int windowFrames, out int attackSerial)
    {
        for (var attackIndex = _entries.Count - 1; attackIndex >= 0; attackIndex--)
        {
            var attack = _entries[attackIndex];
            if (!attack.LightPressed || attack.Consumed || attack.Direction != RelativeDirection.Forward)
            {
                continue;
            }

            if (frame - attack.Frame > windowFrames)
            {
                continue;
            }

            var forwardIndex = -1;
            for (var index = attackIndex; index >= 0; index--)
            {
                if (_entries[index].DirectionChanged &&
                    _entries[index].Direction == RelativeDirection.Forward)
                {
                    forwardIndex = index;
                    break;
                }
            }

            if (forwardIndex < 0)
            {
                continue;
            }

            var expected = RelativeDirection.DownForward;
            var downFound = false;
            var commandStartFrame = attack.Frame;
            for (var index = forwardIndex - 1; index >= 0; index--)
            {
                var entry = _entries[index];
                if (!entry.DirectionChanged || entry.Direction == RelativeDirection.Neutral)
                {
                    continue;
                }

                if (entry.Direction != expected)
                {
                    break;
                }

                if (entry.Frame < frame - windowFrames)
                {
                    break;
                }

                commandStartFrame = entry.Frame;
                if (expected == RelativeDirection.DownForward)
                {
                    expected = RelativeDirection.Down;
                }
                else
                {
                    downFound = true;
                    break;
                }
            }

            if (!downFound || frame - commandStartFrame > windowFrames)
            {
                continue;
            }

            _entries[attackIndex] = attack with { Consumed = true };
            attackSerial = attack.Serial;
            return true;
        }

        attackSerial = 0;
        return false;
    }

    public void Clear()
    {
        _entries.Clear();
        _previousDirection = RelativeDirection.Neutral;
    }

    private readonly record struct BufferedInput(
        int Serial,
        int Frame,
        RelativeDirection Direction,
        bool DirectionChanged,
        bool LightPressed,
        bool HeavyPressed,
        bool Consumed = false);
}

internal sealed class CpuController
{
    private readonly Random _random;
    private int _nextDecisionFrame;
    private int _specialPhase;
    private int _chosenHorizontal;
    private bool _chosenGuard;
    private bool _lightPulse;
    private bool _heavyPulse;

    public CpuController(int seed)
    {
        _random = new Random(seed);
    }

    public FrameInput GetInput(int frame, Fighter cpu, Fighter opponent)
    {
        if (_specialPhase > 0)
        {
            var forward = cpu.Facing;
            var input = _specialPhase switch
            {
                1 => new FrameInput(Down: true),
                2 => new FrameInput(Horizontal: forward, Down: true),
                _ => new FrameInput(Horizontal: forward, LightPressed: true)
            };
            _specialPhase++;
            if (_specialPhase > 3)
            {
                _specialPhase = 0;
                _nextDecisionFrame = frame + GameData.CpuSpecialCooldownFrames;
            }

            return input;
        }

        if (frame >= _nextDecisionFrame)
        {
            _nextDecisionFrame = frame + _random.Next(
                GameData.CpuDecisionMinFrames,
                GameData.CpuDecisionMaxFrames + 1);
            _chosenHorizontal = Math.Abs(opponent.X - cpu.X) > GameData.CpuApproachDistance
                ? Math.Sign(opponent.X - cpu.X)
                : 0;
            _chosenGuard = _chosenHorizontal == 0 && _random.Next(0, GameData.CpuGuardOdds) == 0;
            _lightPulse = false;
            _heavyPulse = false;

            if (_chosenHorizontal == 0 && !_chosenGuard)
            {
                if (cpu.Meter >= GameData.SpecialMeterCost &&
                    _random.Next(0, GameData.CpuSpecialOdds) == 0)
                {
                    _specialPhase = 1;
                }
                else if (_random.Next(0, GameData.CpuHeavyOdds) == 0)
                {
                    _heavyPulse = true;
                }
                else
                {
                    _lightPulse = true;
                }
            }
        }

        var horizontal = _chosenHorizontal == 0
            ? (_chosenGuard ? -cpu.Facing : 0)
            : _chosenHorizontal;
        var down = _chosenGuard;
        var result = new FrameInput(
            Horizontal: horizontal,
            Down: down,
            LightPressed: _lightPulse,
            HeavyPressed: _heavyPulse);
        _lightPulse = false;
        _heavyPulse = false;
        return result;
    }
}

internal sealed class Battle
{
    private CpuController _cpuController = new(1337);
    private int _remainingFrames;

    public RoundPhase Phase { get; private set; } = RoundPhase.Title;
    public RoundResult Result { get; private set; }
    public Fighter Player { get; private set; } = new("P1", false, 250);
    public Fighter Cpu { get; private set; } = new("CPU", true, 710);
    public int Frame { get; private set; }
    public int RemainingSeconds => Math.Max(0, (int)Math.Ceiling(_remainingFrames / (double)GameData.FramesPerSecond));

    public void StartRound()
    {
        Player = new Fighter("P1", false, 250);
        Cpu = new Fighter("CPU", true, 710) { Facing = -1 };
        _cpuController = new CpuController(1337);
        Frame = 0;
        _remainingFrames = GameData.MatchTimeSeconds * GameData.FramesPerSecond;
        Result = RoundResult.None;
        Phase = RoundPhase.Fighting;
    }

    public void Tick(FrameInput playerInput)
    {
        if (Phase == RoundPhase.Title || Phase == RoundPhase.Result)
        {
            if (playerInput.StartPressed)
            {
                StartRound();
            }

            return;
        }

        Frame++;
        _remainingFrames = Math.Max(0, _remainingFrames - 1);
        Player.Facing = Player.X <= Cpu.X ? 1 : -1;
        Cpu.Facing = -Player.Facing;

        var cpuInput = _cpuController.GetInput(Frame, Cpu, Player);
        ProcessInput(Player, playerInput, Frame);
        ProcessInput(Cpu, cpuInput, Frame);
        Player.CurrentInput = playerInput;
        Cpu.CurrentInput = cpuInput;

        MoveFighter(Player);
        MoveFighter(Cpu);
        AdvanceAction(Player);
        AdvanceAction(Cpu);
        ResolvePushApart(Player, Cpu);
        ResolveAttacks();

        if (Player.Health <= 0 || Cpu.Health <= 0)
        {
            FinishRound();
        }
        else if (_remainingFrames == 0)
        {
            Result = Player.Health == Cpu.Health
                ? RoundResult.Draw
                : Player.Health > Cpu.Health ? RoundResult.PlayerWin : RoundResult.CpuWin;
            Phase = RoundPhase.Result;
        }
    }

    public void ClearTransientInput()
    {
        Player.History.Clear();
        Cpu.History.Clear();
        Player.CurrentInput = default;
        Cpu.CurrentInput = default;
    }

    public static RectF GetAttackBox(Fighter fighter)
    {
        if (fighter.Action != FighterAction.Attack || !GameData.Moves.TryGetValue(fighter.CurrentMove, out var move))
        {
            return default;
        }

        var y = GameData.GroundY - fighter.Altitude - fighter.BodyHeight + (fighter.Crouching ? 10 : 24);
        var x = fighter.Facing > 0
            ? fighter.X + GameData.FighterWidth / 2f
            : fighter.X - GameData.FighterWidth / 2f - move.Range;
        return new RectF(x, y, move.Range, move.HitHeight);
    }

    public static bool TryStartMove(Fighter fighter, MoveId moveId)
    {
        if (!CanAct(fighter) || !fighter.Grounded ||
            !GameData.Moves.TryGetValue(moveId, out var move) || fighter.Meter < move.MeterCost)
        {
            return false;
        }

        fighter.Meter -= move.MeterCost;
        fighter.Action = FighterAction.Attack;
        fighter.CurrentMove = moveId;
        fighter.MoveFrame = 0;
        fighter.AttackHasHit = false;
        fighter.Crouching = false;
        return true;
    }

    public static void ResolvePushApart(Fighter left, Fighter right)
    {
        if (!left.PushBox.Intersects(right.PushBox))
        {
            return;
        }

        if (left.X > right.X)
        {
            (left, right) = (right, left);
        }

        var minimumDistance = (left.PushBox.Width + right.PushBox.Width) / 2f;
        var overlap = minimumDistance - (right.X - left.X);
        if (overlap <= 0)
        {
            return;
        }

        var leftMovement = overlap / 2f;
        var rightMovement = overlap - leftMovement;
        left.X -= leftMovement;
        right.X += rightMovement;
        ClampToStage(left);
        ClampToStage(right);

        var remaining = minimumDistance - (right.X - left.X);
        if (remaining > 0)
        {
            var leftCanMove = left.X > GameData.StageLeft + GameData.FighterWidth / 2f;
            var rightCanMove = right.X < GameData.StageRight - GameData.FighterWidth / 2f;
            if (leftCanMove && rightCanMove)
            {
                left.X -= remaining / 2f;
                right.X += remaining / 2f;
            }
            else if (leftCanMove)
            {
                left.X -= remaining;
            }
            else if (rightCanMove)
            {
                right.X += remaining;
            }

            ClampToStage(left);
            ClampToStage(right);
        }
    }

    private static void ProcessInput(Fighter fighter, FrameInput input, int frame)
    {
        var direction = InputBuffer.ResolveDirection(input.Horizontal, input.Down, fighter.Facing);
        fighter.History.Sample(frame, direction, input.LightPressed, input.HeavyPressed);

        var specialCommand = fighter.History.TryConsumeQuarterCircle(
            frame,
            GameData.CommandWindowFrames,
            out _);
        if (specialCommand)
        {
            if (CanAct(fighter) && fighter.Grounded)
            {
                TryStartMove(fighter, MoveId.Special);
            }

            return;
        }

        if (!CanAct(fighter) || !fighter.Grounded)
        {
            return;
        }

        if (input.DebugSpecialPressed)
        {
            TryStartMove(fighter, MoveId.Special);
            return;
        }

        if (input.HeavyPressed)
        {
            TryStartMove(fighter, MoveId.Heavy);
        }
        else if (input.LightPressed)
        {
            TryStartMove(fighter, MoveId.Light);
        }
    }

    private static void MoveFighter(Fighter fighter)
    {
        if (fighter.Grounded && CanAct(fighter))
        {
            if (fighter.CurrentInput.JumpPressed)
            {
                fighter.Grounded = false;
                fighter.Crouching = false;
                fighter.VerticalSpeed = GameData.JumpSpeed;
                fighter.AirborneFrames = 0;
                fighter.Action = FighterAction.Idle;
            }
        }

        fighter.IsGuarding = fighter.Grounded &&
            fighter.CurrentInput.Horizontal == -fighter.Facing &&
            fighter.Action is FighterAction.Idle or FighterAction.Walking or
                FighterAction.Crouching or FighterAction.Guardstun;

        if (CanAct(fighter) && fighter.Grounded)
        {
            fighter.Crouching = fighter.CurrentInput.Down && !fighter.IsGuarding;
            if (fighter.IsGuarding)
            {
                fighter.Action = FighterAction.Idle;
            }
            else if (fighter.Crouching)
            {
                fighter.Action = FighterAction.Crouching;
            }
            else if (fighter.CurrentInput.Horizontal != 0)
            {
                fighter.X += Math.Clamp(fighter.CurrentInput.Horizontal, -1, 1) * GameData.WalkSpeed;
                fighter.Action = FighterAction.Walking;
            }
            else
            {
                fighter.Action = FighterAction.Idle;
            }
        }

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

    private static void AdvanceAction(Fighter fighter)
    {
        switch (fighter.Action)
        {
            case FighterAction.Attack:
                fighter.MoveFrame++;
                if (fighter.MoveFrame >= GameData.Moves[fighter.CurrentMove].TotalFrames)
                {
                    fighter.Action = FighterAction.Idle;
                    fighter.CurrentMove = MoveId.None;
                    fighter.MoveFrame = 0;
                    fighter.AttackHasHit = false;
                }

                break;
            case FighterAction.Hitstun:
            case FighterAction.Guardstun:
                fighter.StunFramesRemaining--;
                if (fighter.StunFramesRemaining <= 0)
                {
                    fighter.Action = FighterAction.Idle;
                    fighter.MoveFrame = 0;
                }

                break;
        }
    }

    private void ResolveAttacks()
    {
        var playerHitsCpu = IsAttackHit(Player, Cpu);
        var cpuHitsPlayer = IsAttackHit(Cpu, Player);

        if (playerHitsCpu)
        {
            ApplyHit(Player, Cpu);
        }

        if (cpuHitsPlayer)
        {
            ApplyHit(Cpu, Player);
        }
    }

    private static bool IsAttackHit(Fighter attacker, Fighter defender)
    {
        if (attacker.Action != FighterAction.Attack || attacker.AttackHasHit ||
            attacker.MoveFrame < GameData.Moves[attacker.CurrentMove].StartupFrames ||
            attacker.MoveFrame >= GameData.Moves[attacker.CurrentMove].StartupFrames +
            GameData.Moves[attacker.CurrentMove].ActiveFrames)
        {
            return false;
        }

        return GetAttackBox(attacker).Intersects(defender.HurtBox);
    }

    private static void ApplyHit(Fighter attacker, Fighter defender)
    {
        var move = GameData.Moves[attacker.CurrentMove];
        attacker.AttackHasHit = true;
        if (defender.IsGuarding)
        {
            defender.Health = Math.Max(0, defender.Health - move.GuardDamage);
            defender.Action = FighterAction.Guardstun;
            defender.StunFramesRemaining = move.GuardstunFrames;
            defender.Meter = Math.Min(GameData.MaximumMeter, defender.Meter + 3);
            attacker.Meter = Math.Min(GameData.MaximumMeter, attacker.Meter + move.MeterGain / 2);
        }
        else
        {
            defender.Health = Math.Max(0, defender.Health - move.Damage);
            defender.Action = FighterAction.Hitstun;
            defender.StunFramesRemaining = move.HitstunFrames;
            defender.Meter = Math.Min(GameData.MaximumMeter, defender.Meter + 5);
            attacker.Meter = Math.Min(GameData.MaximumMeter, attacker.Meter + move.MeterGain);
            if (!defender.Grounded)
            {
                defender.VerticalSpeed = Math.Max(defender.VerticalSpeed, 3.5f);
            }
        }

        defender.Crouching = false;
        defender.X += attacker.Facing * move.Pushback;
        ClampToStage(defender);
    }

    private void FinishRound()
    {
        Player.Action = Player.Health <= 0 ? FighterAction.Knockout : Player.Action;
        Cpu.Action = Cpu.Health <= 0 ? FighterAction.Knockout : Cpu.Action;
        Result = Player.Health <= 0 && Cpu.Health <= 0
            ? RoundResult.Draw
            : Player.Health <= 0 ? RoundResult.CpuWin : RoundResult.PlayerWin;
        Phase = RoundPhase.Result;
    }

    private static void ClampToStage(Fighter fighter)
    {
        fighter.X = Math.Clamp(
            fighter.X,
            GameData.StageLeft + GameData.FighterWidth / 2f,
            GameData.StageRight - GameData.FighterWidth / 2f);
    }

    private static bool CanAct(Fighter fighter) =>
        fighter.Action is FighterAction.Idle or FighterAction.Walking or FighterAction.Crouching;
}

internal static class BattleVerification
{
    public static void Run()
    {
        var checks = 0;
        void Check(bool condition, string name)
        {
            if (!condition)
            {
                throw new InvalidOperationException($"Self-test failed: {name}");
            }

            checks++;
        }

        var history = new InputBuffer(8);
        history.Sample(1, RelativeDirection.Down, false, false);
        history.Sample(2, RelativeDirection.DownForward, false, false);
        history.Sample(3, RelativeDirection.Forward, true, false);
        Check(history.TryConsumeQuarterCircle(3, 16, out _), "quarter-circle command");
        Check(!history.TryConsumeQuarterCircle(3, 16, out _), "command input consumed once");
        Check(InputBuffer.ResolveDirection(-1, true, -1) == RelativeDirection.DownForward, "left-facing relative forward");
        var boundedHistory = new InputBuffer(3);
        for (var i = 1; i <= 5; i++)
        {
            boundedHistory.Sample(i, (RelativeDirection)(i % 4), false, false);
        }

        Check(boundedHistory.Count == 3, "input history capacity");

        var player = new Fighter("P1", false, 100) { Meter = 0 };
        Check(!Battle.TryStartMove(player, MoveId.Special), "special rejected without meter");
        player.Meter = GameData.MaximumMeter;
        player.Action = FighterAction.Hitstun;
        Check(!Battle.TryStartMove(player, MoveId.Light), "move rejected during hitstun");

        var specialBattle = new Battle();
        specialBattle.StartRound();
        specialBattle.Player.X = 800;
        specialBattle.Cpu.X = 500;
        specialBattle.Player.Meter = GameData.MaximumMeter;
        specialBattle.Tick(new FrameInput(Down: true));
        specialBattle.Tick(new FrameInput(Horizontal: -1, Down: true));
        specialBattle.Tick(new FrameInput(Horizontal: -1, LightPressed: true));
        Check(specialBattle.Player.Facing == -1 &&
              specialBattle.Player.Action == FighterAction.Attack &&
              specialBattle.Player.CurrentMove == MoveId.Special &&
              specialBattle.Player.Meter == GameData.MaximumMeter - GameData.SpecialMeterCost,
            "left-facing command starts special without light attack");
        for (var i = 0; i < 8; i++)
        {
            specialBattle.Tick(new FrameInput(Horizontal: -1));
        }

        Check(specialBattle.Player.CurrentMove == MoveId.Special &&
              specialBattle.Player.Meter == GameData.MaximumMeter - GameData.SpecialMeterCost,
            "special does not repeat from buffered history");

        var insufficientBattle = new Battle();
        insufficientBattle.StartRound();
        insufficientBattle.Player.Meter = GameData.SpecialMeterCost - 1;
        insufficientBattle.Tick(new FrameInput(Down: true));
        insufficientBattle.Tick(new FrameInput(Horizontal: 1, Down: true));
        insufficientBattle.Tick(new FrameInput(Horizontal: 1, LightPressed: true));
        Check(insufficientBattle.Player.CurrentMove == MoveId.None &&
              insufficientBattle.Player.Meter == GameData.SpecialMeterCost - 1,
            "failed special command does not fall through to light");

        var bufferedBattle = new Battle();
        bufferedBattle.StartRound();
        bufferedBattle.Player.Action = FighterAction.Hitstun;
        bufferedBattle.Player.StunFramesRemaining = 4;
        bufferedBattle.Player.Meter = GameData.SpecialMeterCost;
        bufferedBattle.Tick(new FrameInput(Down: true));
        bufferedBattle.Tick(new FrameInput(Horizontal: 1, Down: true));
        bufferedBattle.Tick(new FrameInput(Horizontal: 1, LightPressed: true));
        for (var i = 0; i < 3; i++)
        {
            bufferedBattle.Tick(default);
        }

        Check(bufferedBattle.Player.Action == FighterAction.Idle &&
              bufferedBattle.Player.CurrentMove == MoveId.None,
            "special command during stun is not queued");

        var guardBattle = new Battle();
        guardBattle.StartRound();
        guardBattle.Player.X = 400;
        guardBattle.Cpu.X = 490;
        Battle.TryStartMove(guardBattle.Cpu, MoveId.Light);
        for (var i = 0; i < 8; i++)
        {
            guardBattle.Tick(new FrameInput(Horizontal: -1));
        }

        Check(guardBattle.Player.Health == GameData.MaximumHealth - GameData.Light.GuardDamage &&
              guardBattle.Player.Action == FighterAction.Guardstun,
            "ground guard damage and guardstun");

        var attackRight = new Fighter("P1", false, 300) { Facing = 1 };
        var attackLeft = new Fighter("CPU", true, 500) { Facing = -1 };
        Battle.TryStartMove(attackRight, MoveId.Light);
        Battle.TryStartMove(attackLeft, MoveId.Light);
        Check(Battle.GetAttackBox(attackRight).X < Battle.GetAttackBox(attackLeft).X, "attack boxes mirror with facing");

        var battle = new Battle();
        battle.StartRound();
        battle.Player.X = 400;
        battle.Cpu.X = 490;
        Battle.TryStartMove(battle.Player, MoveId.Light);
        Battle.TryStartMove(battle.Cpu, MoveId.Light);
        for (var i = 0; i < 20; i++)
        {
            battle.Tick(default);
        }

        Check(battle.Player.Health < GameData.MaximumHealth && battle.Cpu.Health < GameData.MaximumHealth, "simultaneous hit resolution");
        Check(battle.Player.Health >= GameData.MaximumHealth - GameData.Light.Damage &&
              battle.Cpu.Health >= GameData.MaximumHealth - GameData.Light.Damage, "single-hit-per-swing");

        var boundaryBattle = new Battle();
        boundaryBattle.StartRound();
        boundaryBattle.Player.X = GameData.StageLeft + GameData.FighterWidth / 2f;
        for (var i = 0; i < 5; i++)
        {
            boundaryBattle.Tick(new FrameInput(Horizontal: -1));
        }

        Check(boundaryBattle.Player.X >= GameData.StageLeft + GameData.FighterWidth / 2f, "stage boundary");
        boundaryBattle.Tick(new FrameInput(JumpPressed: true));
        for (var i = 0; i < 90; i++)
        {
            boundaryBattle.Tick(default);
        }

        Check(boundaryBattle.Player.Grounded && boundaryBattle.Player.Altitude == 0, "jump landing");
        var left = new Fighter("L", false, 400);
        var right = new Fighter("R", true, 410);
        Battle.ResolvePushApart(left, right);
        Check(Math.Abs(right.X - left.X) >= GameData.FighterWidth - 0.01f, "push-apart collision");
        var airborne = new Fighter("AIR", true, 410)
        {
            Grounded = false,
            Altitude = GameData.StandingHeight + 1
        };
        var grounded = new Fighter("GROUND", false, 400);
        Battle.ResolvePushApart(grounded, airborne);
        Check(Math.Abs(airborne.X - grounded.X) == 10, "push boxes do not collide vertically");
        var wallLeft = new Fighter("L", false, GameData.StageRight - GameData.FighterWidth / 2f);
        var wallRight = new Fighter("R", true, wallLeft.X - 10);
        Battle.ResolvePushApart(wallLeft, wallRight);
        Check(Math.Abs(wallRight.X - wallLeft.X) >= GameData.FighterWidth - 0.01f &&
              wallLeft.X <= GameData.StageRight - GameData.FighterWidth / 2f,
            "push-apart at stage wall");

        var drawBattle = new Battle();
        drawBattle.StartRound();
        drawBattle.Player.X = 400;
        drawBattle.Cpu.X = 490;
        drawBattle.Player.Health = 1;
        drawBattle.Cpu.Health = 1;
        Battle.TryStartMove(drawBattle.Player, MoveId.Light);
        Battle.TryStartMove(drawBattle.Cpu, MoveId.Light);
        for (var i = 0; i < 20 && drawBattle.Phase == RoundPhase.Fighting; i++)
        {
            drawBattle.Tick(default);
        }

        Check(drawBattle.Phase == RoundPhase.Result && drawBattle.Result == RoundResult.Draw, "simultaneous knockout");
        drawBattle.Player.History.Sample(1, RelativeDirection.Forward, true, false);
        drawBattle.StartRound();
        Check(drawBattle.Player.Health == GameData.MaximumHealth && drawBattle.Player.Meter == 0 &&
              drawBattle.Player.History.Count == 0 && !drawBattle.Player.AttackHasHit,
            "rematch state reset");

        var cpuBattle = new Battle();
        cpuBattle.StartRound();
        for (var i = 0; i < GameData.MatchTimeSeconds * GameData.FramesPerSecond &&
             cpuBattle.Phase == RoundPhase.Fighting; i++)
        {
            cpuBattle.Tick(default);
        }

        Check(cpuBattle.Phase == RoundPhase.Result &&
              cpuBattle.Result is RoundResult.CpuWin or RoundResult.Draw,
            "CPU can complete a match");

        Console.WriteLine($"Battle self-test passed ({checks} checks).");
    }
}
