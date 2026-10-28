using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Chozetumokosatugekiranzan;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Contains("--self-test", StringComparer.Ordinal))
        {
            BattleVerification.Run();
            return;
        }

        using var game = new FightingGame();
        game.Run();
    }
}

internal sealed class FightingGame : Game
{
    private static readonly Color Background = new(17, 22, 38);
    private static readonly Color Floor = new(47, 54, 72);
    private static readonly Color PlayerColor = new(72, 196, 255);
    private static readonly Color CpuColor = new(255, 117, 121);
    private static readonly Color MeterColor = new(255, 208, 74);
    private static readonly Dictionary<char, string[]> Glyphs = CreateGlyphs();

    private readonly GraphicsDeviceManager _graphics;
    private readonly Battle _battle = new();
    private readonly Dictionary<string, Texture2D> _sprites = new(StringComparer.OrdinalIgnoreCase);
    private SpriteBatch _spriteBatch = null!;
    private Texture2D _pixel = null!;
    private KeyboardState _previousKeyboard;
    private bool _wasActive = true;
    private bool _suppressUntilNeutral;
    private bool _debug;

    public FightingGame()
    {
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = GameData.ScreenWidth,
            PreferredBackBufferHeight = GameData.ScreenHeight,
            SynchronizeWithVerticalRetrace = true
        };
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        IsFixedTimeStep = true;
        TargetElapsedTime = TimeSpan.FromSeconds(1d / GameData.FramesPerSecond);
        Window.Title = "Fighter - CPU Battle";
    }

    protected override void Initialize()
    {
        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData([Color.White]);

        var assetsDirectory = Path.Combine(AppContext.BaseDirectory, "Assets");
        foreach (var animation in GameData.SpriteAnimations.Values
                     .GroupBy(item => item.FileName, StringComparer.OrdinalIgnoreCase)
                     .Select(group => group.First()))
        {
            if (animation.Frames.Length == 0)
            {
                continue;
            }

            var path = Path.Combine(assetsDirectory, animation.FileName);
            if (!File.Exists(path))
            {
                continue;
            }

            try
            {
                using var stream = File.OpenRead(path);
                var texture = Texture2D.FromStream(GraphicsDevice, stream);
                if (animation.Frames.All(frame =>
                    frame.Source.X >= 0 && frame.Source.Y >= 0 &&
                    frame.Source.Right <= texture.Width && frame.Source.Bottom <= texture.Height))
                {
                    _sprites[animation.FileName] = texture;
                }
                else
                {
                    texture.Dispose();
                }
            }
            catch (IOException)
            {
            }
            catch (InvalidOperationException)
            {
            }
        }
    }

    protected override void Update(GameTime gameTime)
    {
        if (!IsActive)
        {
            if (_wasActive)
            {
                _battle.ClearTransientInput();
            }

            _wasActive = false;
            _previousKeyboard = default;
            _suppressUntilNeutral = true;
            base.Update(gameTime);
            return;
        }

        var current = Keyboard.GetState();
        if (!_wasActive)
        {
            _wasActive = true;
            _battle.ClearTransientInput();
            _previousKeyboard = current;
            _suppressUntilNeutral = true;
            base.Update(gameTime);
            return;
        }

        if (current.IsKeyDown(Keys.Escape))
        {
            Exit();
        }

        if (_suppressUntilNeutral)
        {
            _previousKeyboard = current;
            if (ControlsReleased(current))
            {
                _suppressUntilNeutral = false;
            }

            base.Update(gameTime);
            return;
        }

        if (Pressed(current, Keys.F3))
        {
            _debug = !_debug;
        }

        var horizontal = (current.IsKeyDown(Keys.D) ? 1 : 0) - (current.IsKeyDown(Keys.A) ? 1 : 0);
        var input = new FrameInput(
            Horizontal: horizontal,
            Down: current.IsKeyDown(Keys.S),
            JumpPressed: Pressed(current, Keys.W),
            LightPressed: Pressed(current, Keys.J),
            HeavyPressed: Pressed(current, Keys.K),
            DebugSpecialPressed: Pressed(current, Keys.F2),
            StartPressed: Pressed(current, Keys.Enter));
        _battle.Tick(input);
        _previousKeyboard = current;
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Background);
        _spriteBatch.Begin(samplerState: SamplerState.PointClamp);

        if (_battle.Phase != RoundPhase.Title)
        {
            DrawStage();
            DrawHud();
            DrawFighter(_battle.Player);
            DrawFighter(_battle.Cpu);
            if (_debug)
            {
                DrawDebug();
            }
        }

        switch (_battle.Phase)
        {
            case RoundPhase.Title:
                DrawCentered("FIGHTER", 192, 4, Color.White);
                DrawCentered("CPU BATTLE", 246, 2, PlayerColor);
                DrawCentered("ENTER START", 320, 2, Color.White);
                DrawCentered("A D MOVE  W JUMP  S CROUCH", 366, 1, Color.LightGray);
                DrawCentered("J LIGHT  K HEAVY  BACK GUARD", 390, 1, Color.LightGray);
                DrawCentered("DOWN DOWN-FORWARD FORWARD J SPECIAL", 414, 1, Color.LightGray);
                DrawCentered("F2 DEBUG SPECIAL  F3 DEBUG VIEW", 438, 1, Color.LightGray);
                break;
            case RoundPhase.Result:
                DrawRect(new Rectangle(220, 188, 520, 160), new Color(5, 8, 18, 220));
                DrawCentered(ResultText(), 220, 4, _battle.Result switch
                {
                    RoundResult.PlayerWin => PlayerColor,
                    RoundResult.CpuWin => CpuColor,
                    _ => Color.White
                });
                DrawCentered("ENTER REMATCH", 292, 2, Color.White);
                break;
        }

        if (!IsActive)
        {
            DrawRect(new Rectangle(315, 220, 330, 80), new Color(5, 8, 18, 220));
            DrawCentered("PAUSED", 250, 3, Color.White);
        }

        _spriteBatch.End();
        base.Draw(gameTime);
    }

    protected override void UnloadContent()
    {
        foreach (var texture in _sprites.Values)
        {
            texture.Dispose();
        }

        _pixel.Dispose();
        _spriteBatch.Dispose();
        base.UnloadContent();
    }

    private bool Pressed(KeyboardState current, Keys key) =>
        current.IsKeyDown(key) && _previousKeyboard.IsKeyUp(key);

    private static bool ControlsReleased(KeyboardState state) =>
        new[] { Keys.A, Keys.D, Keys.W, Keys.S, Keys.J, Keys.K, Keys.F2, Keys.Enter }
            .All(key => state.IsKeyUp(key));

    private string ResultText() => _battle.Result switch
    {
        RoundResult.PlayerWin => "PLAYER WIN",
        RoundResult.CpuWin => "CPU WIN",
        _ => "DRAW"
    };

    private void DrawStage()
    {
        DrawRect(new Rectangle(0, GameData.GroundY, GameData.ScreenWidth, GameData.ScreenHeight - GameData.GroundY), Floor);
        DrawRect(new Rectangle(0, GameData.GroundY - 5, GameData.ScreenWidth, 5), new Color(115, 126, 151));
        DrawRect(new Rectangle(34, 186, 892, 2), new Color(42, 51, 72));
        DrawRect(new Rectangle((int)GameData.StageLeft, GameData.GroundY - 18, 2, 20), new Color(87, 100, 125));
        DrawRect(new Rectangle((int)GameData.StageRight - 2, GameData.GroundY - 18, 2, 20), new Color(87, 100, 125));
    }

    private void DrawHud()
    {
        DrawText("P1", 42, 18, 2, PlayerColor);
        DrawText("CPU", 840, 18, 2, CpuColor);
        DrawBar(42, 42, 390, 22, _battle.Player.Health, GameData.MaximumHealth, PlayerColor);
        DrawBar(528, 42, 390, 22, _battle.Cpu.Health, GameData.MaximumHealth, CpuColor, reverse: true);
        DrawBar(42, 70, 260, 8, _battle.Player.Meter, GameData.MaximumMeter, MeterColor);
        DrawBar(658, 70, 260, 8, _battle.Cpu.Meter, GameData.MaximumMeter, MeterColor, reverse: true);
        DrawCentered(_battle.RemainingSeconds.ToString("00"), 24, 2, Color.White);
        if (_battle.Phase == RoundPhase.Fighting)
        {
            DrawText("P1 HP", 42, 88, 1, Color.LightGray);
            DrawText("CPU HP", 840, 88, 1, Color.LightGray);
        }
    }

    private void DrawBar(int x, int y, int width, int height, int value, int maximum, Color color, bool reverse = false)
    {
        DrawRect(new Rectangle(x, y, width, height), new Color(48, 54, 70));
        var fill = (int)Math.Round(width * Math.Clamp(value / (float)maximum, 0f, 1f));
        var fillX = reverse ? x + width - fill : x;
        DrawRect(new Rectangle(fillX, y, fill, height), color);
        DrawRect(new Rectangle(x, y, width, 2), Color.White * 0.18f);
    }

    private void DrawFighter(Fighter fighter)
    {
        var kind = fighter.Action switch
        {
            FighterAction.Attack => AnimationKind.Attack,
            FighterAction.Hitstun => AnimationKind.Hit,
            FighterAction.Guardstun => AnimationKind.Guard,
            _ when !fighter.Grounded => AnimationKind.Airborne,
            _ when fighter.Crouching => AnimationKind.Crouch,
            _ when Math.Abs(fighter.CurrentInput.Horizontal) > 0 => AnimationKind.Walk,
            _ => AnimationKind.Idle
        };
        if (!TryDrawSprite(fighter, kind))
        {
            DrawFallbackFighter(fighter);
        }

        var feetY = (int)(GameData.GroundY - fighter.Altitude);
        var marker = fighter.IsCpu ? CpuColor : PlayerColor;
        DrawRect(new Rectangle((int)fighter.X - 23, feetY + 4, 46, 4), marker);
        DrawCenteredAt(fighter.Name, (int)fighter.X, feetY - fighter.BodyHeight - 22, 1, marker);

        if (_debug)
        {
            DrawOutline(ToRectangle(fighter.BodyBox), Color.LimeGreen);
            var attack = Battle.GetAttackBox(fighter);
            if (attack.Width > 0)
            {
                DrawOutline(ToRectangle(attack), Color.Red);
            }
        }
    }

    private bool TryDrawSprite(Fighter fighter, AnimationKind kind)
    {
        var animation = GameData.SpriteAnimations[kind];
        if (animation.Frames.Length == 0 || !_sprites.TryGetValue(animation.FileName, out var texture))
        {
            return false;
        }

        var totalFrames = animation.Frames.Sum(frame => Math.Max(1, frame.DurationFrames));
        var animationFrame = fighter.Action == FighterAction.Attack ? fighter.MoveFrame : _battle.Frame / 6;
        var frameNumber = animation.Loop ? animationFrame % totalFrames : Math.Min(animationFrame, totalFrames - 1);
        var selected = animation.Frames[0];
        foreach (var frame in animation.Frames)
        {
            if (frameNumber < Math.Max(1, frame.DurationFrames))
            {
                selected = frame;
                break;
            }

            frameNumber -= Math.Max(1, frame.DurationFrames);
        }

        var tint = fighter.Action switch
        {
            FighterAction.Hitstun => Color.LightPink,
            FighterAction.Guardstun => Color.LightBlue,
            _ => Color.White
        };
        _spriteBatch.Draw(
            texture,
            new Vector2(fighter.X, GameData.GroundY - fighter.Altitude),
            selected.Source,
            tint,
            0f,
            selected.Origin,
            GameData.SpriteScale,
            fighter.Facing < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None,
            0f);
        return true;
    }

    private void DrawFallbackFighter(Fighter fighter)
    {
        var centerX = (int)Math.Round(fighter.X);
        var feetY = (int)Math.Round(GameData.GroundY - fighter.Altitude);
        var tint = fighter.IsCpu ? CpuColor : PlayerColor;
        if (fighter.Action == FighterAction.Hitstun)
        {
            tint = Color.White;
        }
        else if (fighter.Action == FighterAction.Guardstun || fighter.IsGuarding)
        {
            tint = new Color(135, 181, 255);
        }

        var crouch = fighter.Crouching ? 18 : 0;
        DrawRect(new Rectangle(centerX - 11, feetY - 54 + crouch, 22, 36 - crouch), tint);
        DrawRect(new Rectangle(centerX - 10, feetY - 78 + crouch, 20, 20), new Color(247, 213, 177));
        var walkOffset = fighter.Action == FighterAction.Idle &&
                         Math.Abs(fighter.CurrentInput.Horizontal) > 0
            ? (int)(Math.Sin(_battle.Frame / 4d) * 5)
            : 0;
        DrawRect(new Rectangle(centerX - 13, feetY - 23 + crouch, 10, 21 - crouch / 2 + walkOffset), tint);
        DrawRect(new Rectangle(centerX + 3, feetY - 23 + crouch, 10, 21 - crouch / 2 - walkOffset), tint);

        var armY = feetY - 47 + crouch;
        var armReach = fighter.Action == FighterAction.Attack
            ? Math.Min(38, 15 + fighter.MoveFrame * 3)
            : fighter.IsGuarding ? 24 : 14;
        var armX = fighter.Facing > 0 ? centerX + 7 : centerX - armReach - 7;
        DrawRect(new Rectangle(armX, armY, armReach, 9), tint);
        DrawRect(new Rectangle(centerX - 4, armY - 2, 9, 8), new Color(247, 213, 177));

        if (fighter.Action == FighterAction.Attack && fighter.CurrentMove == MoveId.Special &&
            fighter.MoveFrame >= GameData.Special.StartupFrames)
        {
            var effectX = fighter.Facing > 0 ? centerX + 26 : centerX - 66;
            DrawRect(new Rectangle(effectX, feetY - 50, 40, 16), new Color(137, 113, 255, 220));
        }
    }

    private void DrawDebug()
    {
        DrawRect(new Rectangle(320, 96, 320, 70), new Color(5, 8, 18, 210));
        DrawCenteredAt($"{_battle.Player.Action} {_battle.Player.CurrentMove} {_battle.Player.MoveFrame}",
            480, 108, 1, Color.LimeGreen);
        DrawCenteredAt($"{_battle.Cpu.Action} {_battle.Cpu.CurrentMove} {_battle.Cpu.MoveFrame}",
            480, 132, 1, Color.Orange);
    }

    private void DrawRect(Rectangle rectangle, Color color)
    {
        if (rectangle.Width > 0 && rectangle.Height > 0)
        {
            _spriteBatch.Draw(_pixel, rectangle, color);
        }
    }

    private void DrawOutline(Rectangle rect, Color color)
    {
        DrawRect(new Rectangle(rect.X, rect.Y, rect.Width, 2), color);
        DrawRect(new Rectangle(rect.X, rect.Bottom - 2, rect.Width, 2), color);
        DrawRect(new Rectangle(rect.X, rect.Y, 2, rect.Height), color);
        DrawRect(new Rectangle(rect.Right - 2, rect.Y, 2, rect.Height), color);
    }

    private void DrawCentered(string text, int y, int scale, Color color) =>
        DrawCenteredAt(text, GameData.ScreenWidth / 2, y, scale, color);

    private void DrawCenteredAt(string text, int centerX, int y, int scale, Color color)
    {
        var width = Math.Max(0, text.Length * 6 * scale - scale);
        DrawText(text, centerX - width / 2, y, scale, color);
    }

    private void DrawText(string text, int x, int y, int scale, Color color)
    {
        var cursor = x;
        foreach (var character in text.ToUpperInvariant())
        {
            if (Glyphs.TryGetValue(character, out var rows))
            {
                for (var row = 0; row < rows.Length; row++)
                {
                    for (var column = 0; column < rows[row].Length; column++)
                    {
                        if (rows[row][column] == '1')
                        {
                            DrawRect(new Rectangle(cursor + column * scale, y + row * scale, scale, scale), color);
                        }
                    }
                }
            }

            cursor += 6 * scale;
        }
    }

    private static Rectangle ToRectangle(RectF box) => new(
        (int)Math.Floor(box.X),
        (int)Math.Floor(box.Y),
        (int)Math.Ceiling(box.Width),
        (int)Math.Ceiling(box.Height));

    private static Dictionary<char, string[]> CreateGlyphs() => new()
    {
        ['A'] = ["01110", "10001", "10001", "11111", "10001", "10001", "10001"],
        ['B'] = ["11110", "10001", "10001", "11110", "10001", "10001", "11110"],
        ['C'] = ["01111", "10000", "10000", "10000", "10000", "10000", "01111"],
        ['D'] = ["11110", "10001", "10001", "10001", "10001", "10001", "11110"],
        ['E'] = ["11111", "10000", "10000", "11110", "10000", "10000", "11111"],
        ['F'] = ["11111", "10000", "10000", "11110", "10000", "10000", "10000"],
        ['G'] = ["01111", "10000", "10000", "10111", "10001", "10001", "01111"],
        ['H'] = ["10001", "10001", "10001", "11111", "10001", "10001", "10001"],
        ['I'] = ["111", "010", "010", "010", "010", "010", "111"],
        ['J'] = ["00111", "00010", "00010", "00010", "00010", "10010", "01100"],
        ['K'] = ["10001", "10010", "10100", "11000", "10100", "10010", "10001"],
        ['L'] = ["10000", "10000", "10000", "10000", "10000", "10000", "11111"],
        ['M'] = ["10001", "11011", "10101", "10101", "10001", "10001", "10001"],
        ['N'] = ["10001", "11001", "11001", "10101", "10011", "10011", "10001"],
        ['O'] = ["01110", "10001", "10001", "10001", "10001", "10001", "01110"],
        ['P'] = ["11110", "10001", "10001", "11110", "10000", "10000", "10000"],
        ['Q'] = ["01110", "10001", "10001", "10001", "10101", "10010", "01101"],
        ['R'] = ["11110", "10001", "10001", "11110", "10100", "10010", "10001"],
        ['S'] = ["01111", "10000", "10000", "01110", "00001", "00001", "11110"],
        ['T'] = ["11111", "00100", "00100", "00100", "00100", "00100", "00100"],
        ['U'] = ["10001", "10001", "10001", "10001", "10001", "10001", "01110"],
        ['V'] = ["10001", "10001", "10001", "10001", "10001", "01010", "00100"],
        ['W'] = ["10001", "10001", "10001", "10101", "10101", "11011", "10001"],
        ['X'] = ["10001", "10001", "01010", "00100", "01010", "10001", "10001"],
        ['Y'] = ["10001", "10001", "01010", "00100", "00100", "00100", "00100"],
        ['Z'] = ["11111", "00001", "00010", "00100", "01000", "10000", "11111"],
        ['0'] = ["01110", "10001", "10011", "10101", "11001", "10001", "01110"],
        ['1'] = ["010", "110", "010", "010", "010", "010", "111"],
        ['2'] = ["01110", "10001", "00001", "00010", "00100", "01000", "11111"],
        ['3'] = ["11110", "00001", "00001", "01110", "00001", "00001", "11110"],
        ['4'] = ["00010", "00110", "01010", "10010", "11111", "00010", "00010"],
        ['5'] = ["11111", "10000", "10000", "11110", "00001", "00001", "11110"],
        ['6'] = ["01110", "10000", "10000", "11110", "10001", "10001", "01110"],
        ['7'] = ["11111", "00001", "00010", "00100", "01000", "01000", "01000"],
        ['8'] = ["01110", "10001", "10001", "01110", "10001", "10001", "01110"],
        ['9'] = ["01110", "10001", "10001", "01111", "00001", "00001", "01110"],
        [':'] = ["000", "010", "010", "000", "010", "010", "000"],
        ['-'] = ["000", "000", "000", "11111", "000", "000", "000"],
        ['/'] = ["00001", "00010", "00010", "00100", "01000", "01000", "10000"],
        ['!'] = ["010", "010", "010", "010", "010", "000", "010"]
    };
}
