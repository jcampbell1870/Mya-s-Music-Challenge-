namespace MyasMusicChallenge.Core.Rewards;

/// <summary>A perfect score earns the Gold Record achievement.</summary>
public static class GoldRecordReward
{
    public static bool IsEarned(int score) => score == 100;
}
