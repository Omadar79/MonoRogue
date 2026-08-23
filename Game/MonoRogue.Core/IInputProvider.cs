namespace MonoRogue.Core;

public interface IInputProvider
{
    // Either a snapshot of currently pressed keys, or a method to translate a UI keyboard into InputCommands.
    IEnumerable<InputCommand> ConsumeCommands();
}