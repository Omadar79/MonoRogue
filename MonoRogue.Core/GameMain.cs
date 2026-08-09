
namespace MonoRogue.Core;

public class GameMain
{
    
    public GameState CurrentState { get; private set; } 


    public GameMain()
    {
        CurrentState = GameState.MainMenu;
    }

}

public enum GameState
{
    MainMenu,
    Playing,
    Paused,
    GameOver
}
