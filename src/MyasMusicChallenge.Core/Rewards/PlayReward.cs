namespace MyasMusicChallenge.Core.Rewards;

/// <summary>"Just for playing" reward rules: every finished song earns A1870, regardless of the score.</summary>
public static class PlayReward
{
    public const string Mode = "mya-karaoke";

    public static bool IsEligible(double playedSeconds, RewardTreasuryOptions options) =>
        options.Enabled && playedSeconds >= Math.Max(0, options.MinimumPlaySeconds);

    public static RewardGameProof CreateProof(string songId, int score, DateTime completedAtUtc) => new()
    {
        GameId = $"mya-{songId}-{Guid.NewGuid():N}",
        Mode = Mode,
        PlayerScore = Math.Clamp(score, 0, 100),
        OpponentScore = 0,
        DifficultyLevel = "Karaoke",
        CompletedAt = completedAtUtc,
        PlayerWon = true,
    };
}
