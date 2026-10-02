namespace MyasMusicChallenge.Scenes;

/// <summary>A game screen. Scenes draw onto the 1280×720 virtual canvas.</summary>
public abstract class Scene
{
    protected Scene(GameForm game)
    {
        Game = game;
    }

    protected GameForm Game { get; }

    protected GameContext Context => Game.Context;

    /// <summary>Seconds since the scene was entered.</summary>
    protected double Time { get; private set; }

    public virtual void Enter()
    {
    }

    public virtual void Leave()
    {
    }

    public virtual void Update(double deltaSeconds) => Time += deltaSeconds;

    public abstract void Draw(Graphics g);

    public virtual void OnKey(Keys key)
    {
    }
}
