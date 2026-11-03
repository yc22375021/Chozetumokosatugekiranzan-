using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Chozetumokosatugekiranzan;

internal sealed class GameSession
{
    public static readonly Rectangle CheckboxBounds = new(190, 342, 580, 42);
    public static readonly Rectangle PreviousPageBounds = new(214, 276, 150, 40);
    public static readonly Rectangle NextPageBounds = new(596, 276, 150, 40);
    public static readonly Rectangle ContinueBounds = new(300, 424, 360, 44);

    private KeyboardState _previousKeyboard;
    private MouseState _previousMouse;
    private bool _wasActive = true;
    private bool _suppressUntilNeutral;

    public GameSession(InstructionSettings settings)
    {
        Settings = settings;
        InstructionsVisible = !settings.SkipInstructions;
        if (settings.SkipInstructions)
        {
            Battle.StartRound();
        }
    }

    public Battle Battle { get; } = new();
    public InstructionSettings Settings { get; }
    public bool InstructionsVisible { get; private set; }
    public int InstructionPage { get; private set; }
    public bool DebugOverlay { get; private set; }

    public void Update(KeyboardState keyboard, MouseState mouse, bool active)
    {
        if (!active || !_wasActive)
        {
            if (_wasActive != active)
            {
                Battle.ClearTransientInput();
            }

            _wasActive = active;
            _suppressUntilNeutral = true;
            RememberInput(keyboard, mouse);
            return;
        }

        if (_suppressUntilNeutral)
        {
            _suppressUntilNeutral = !ControlsReleased(keyboard, mouse);
            RememberInput(keyboard, mouse);
            return;
        }

        var start = Pressed(keyboard, GameData.Controls.Start);
        var help = Pressed(keyboard, GameData.Controls.Help);
        var click = mouse.LeftButton == ButtonState.Pressed &&
                    _previousMouse.LeftButton == ButtonState.Released;
        if (InstructionsVisible)
        {
            if (Pressed(keyboard, GameData.Controls.ToggleInstructions) ||
                (click && CheckboxBounds.Contains(mouse.Position)))
            {
                Settings.SetSkipInstructions(!Settings.SkipInstructions);
            }
            else if (start || (click && ContinueBounds.Contains(mouse.Position)) ||
                     (help && Battle.Phase == RoundPhase.Fighting))
            {
                InstructionsVisible = false;
                if (Battle.Phase != RoundPhase.Fighting)
                {
                    Battle.StartRound();
                }

                SuppressInput();
            }
            else if (Pressed(keyboard, Keys.Left) ||
                     (click && PreviousPageBounds.Contains(mouse.Position)))
            {
                InstructionPage = Math.Max(0, InstructionPage - 1);
            }
            else if (Pressed(keyboard, Keys.Right) ||
                     (click && NextPageBounds.Contains(mouse.Position)))
            {
                InstructionPage = Math.Min(2, InstructionPage + 1);
            }
        }
        else if ((help && Battle.Phase == RoundPhase.Fighting) ||
                 (start && Battle.Phase != RoundPhase.Fighting && !Settings.SkipInstructions))
        {
            InstructionsVisible = true;
            SuppressInput();
        }
        else
        {
            if (Pressed(keyboard, GameData.Controls.DebugOverlay))
            {
                DebugOverlay = !DebugOverlay;
            }

            Battle.Tick(new FrameInput(
                Horizontal: (keyboard.IsKeyDown(GameData.Controls.Right) || keyboard.IsKeyDown(Keys.Right) ? 1 : 0) -
                            (keyboard.IsKeyDown(GameData.Controls.Left) || keyboard.IsKeyDown(Keys.Left) ? 1 : 0),
                Down: keyboard.IsKeyDown(GameData.Controls.Down) || keyboard.IsKeyDown(Keys.Down),
                JumpPressed: Pressed(keyboard, GameData.Controls.Jump) || Pressed(keyboard, Keys.Up),
                UpHeld: keyboard.IsKeyDown(GameData.Controls.Jump) || keyboard.IsKeyDown(Keys.Up),
                LightPressed: Pressed(keyboard, GameData.Controls.Light),
                HeavyPressed: Pressed(keyboard, GameData.Controls.Heavy),
                MediumPressed: Pressed(keyboard, GameData.Controls.Medium),
                GuardHeld: keyboard.IsKeyDown(GameData.Controls.Guard),
                StartPressed: start));
        }

        RememberInput(keyboard, mouse);
    }

    private void SuppressInput()
    {
        Battle.ClearTransientInput();
        _suppressUntilNeutral = true;
    }

    private bool Pressed(KeyboardState keyboard, Keys key) =>
        keyboard.IsKeyDown(key) && _previousKeyboard.IsKeyUp(key);

    private void RememberInput(KeyboardState keyboard, MouseState mouse)
    {
        _previousKeyboard = keyboard;
        _previousMouse = mouse;
    }

    private static bool ControlsReleased(KeyboardState keyboard, MouseState mouse) =>
        mouse.LeftButton == ButtonState.Released &&
        new[]
        {
            GameData.Controls.Left, GameData.Controls.Right, GameData.Controls.Jump,
            GameData.Controls.Down, GameData.Controls.Light, GameData.Controls.Heavy,
            GameData.Controls.Medium, GameData.Controls.Guard, Keys.Left, Keys.Right, Keys.Up, Keys.Down,
            GameData.Controls.DebugOverlay,
            GameData.Controls.Start, GameData.Controls.Help, GameData.Controls.ToggleInstructions
        }.All(keyboard.IsKeyUp);
}
