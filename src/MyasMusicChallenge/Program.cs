namespace MyasMusicChallenge;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        using var context = GameContext.Create(AppContext.BaseDirectory);
        Application.Run(new GameForm(context));
    }
}
