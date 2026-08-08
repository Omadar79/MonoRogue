using SadConsole;
using SadConsole.Configuration;
using MonoRogue.Visuals;


Settings.WindowTitle = "My MonoRogue Game";

Builder
    .GetBuilder()
    .SetWindowSizeInCells(120, 38)
    .ConfigureFonts(true)
    .SetStartingScreen<RootScreen>()
    .IsStartingScreenFocused(true)
    .Run();

