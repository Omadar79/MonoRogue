namespace MonoRogue.Core;

/// <summary>
/// A struct that represents a command that the game can process. It is used to decouple the input processing from the game logic.
/// </summary>
public readonly struct InputCommand
{
    public InputType Type { get; }
    public SadRogue.Primitives.Point Delta { get; }

    public InputCommand(InputType type, SadRogue.Primitives.Point delta)
    {
        Type = type;
        Delta = delta;
    }
}