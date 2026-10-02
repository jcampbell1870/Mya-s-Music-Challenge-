namespace MyasMusicChallenge.Core.Assessment;

/// <summary>Mýa's coaching voice: converts raw scores into the feedback she gives after each song.</summary>
public static class MyaFeedback
{
    private static readonly (int Minimum, string Grade, string Title)[] Grades =
    [
        (90, "S", "Superstar"),
        (80, "A", "Headliner"),
        (70, "B", "Rising Star"),
        (55, "C", "Showcase Contender"),
        (40, "D", "Open Mic Brave Heart"),
        (0, "E", "Studio Rookie"),
    ];

    public static string CategoryName(AssessmentCategory category) => category switch
    {
        AssessmentCategory.PitchAccuracy => "Pitch Accuracy",
        AssessmentCategory.PitchStability => "Pitch Stability",
        AssessmentCategory.RhythmAndTiming => "Rhythm & Timing",
        AssessmentCategory.VocalRange => "Vocal Range",
        AssessmentCategory.DynamicsControl => "Dynamics Control",
        AssessmentCategory.BreathSupport => "Breath Support",
        _ => category.ToString(),
    };

    public static (string Grade, string Title) GradeFor(int overallScore)
    {
        foreach (var (minimum, grade, title) in Grades)
        {
            if (overallScore >= minimum)
            {
                return (grade, title);
            }
        }

        return (Grades[^1].Grade, Grades[^1].Title);
    }

    public static VocalAssessment Build(
        AssessmentContext context,
        int overallScore,
        IReadOnlyDictionary<AssessmentCategory, int> scores,
        PerformanceStatistics statistics)
    {
        var (grade, title) = GradeFor(overallScore);
        var player = string.IsNullOrWhiteSpace(context.PlayerName) ? "superstar" : context.PlayerName.Trim();
        var song = context.SongTitle;

        var categories = Enum.GetValues<AssessmentCategory>()
            .Select(c => new CategoryScore(c, CategoryName(c), scores[c], Comment(c, scores[c], statistics)))
            .ToList();

        var strengths = categories
            .Where(c => c.Score >= 70)
            .OrderByDescending(c => c.Score)
            .Take(2)
            .Select(c => c.Comment)
            .ToList();

        if (statistics.VibratoDetected)
        {
            strengths.Add("And I heard some beautiful natural vibrato in there. That's a pro touch!");
        }

        var improvements = categories
            .Where(c => c.Score < 70)
            .OrderBy(c => c.Score)
            .Take(2)
            .Select(c => c.Comment)
            .ToList();

        var weakest = categories.OrderBy(c => c.Score).First();
        var tip = weakest.Score >= 85
            ? "Coach Mýa's tip: protect that instrument. Hydrate, rest your voice and warm up before every show."
            : CoachingTip(weakest.Category);

        var headline = grade switch
        {
            "S" => $"{player}, that was a headline-worthy performance of \"{song}\"! I'd share a stage with you any day.",
            "A" => $"Wow! You really brought \"{song}\" to life. That's a record-ready vocal, {player}.",
            "B" => $"Nice work on \"{song}\", {player}! The talent is clearly there. Now let's polish it.",
            "C" => $"Good effort on \"{song}\". You've got a foundation we can build on, {player}.",
            "D" => $"Thanks for giving \"{song}\" your all, {player}. Let's work on the fundamentals together.",
            _ => $"Every star starts somewhere, {player}. Let's get you warmed up and try \"{song}\" again!",
        };

        var signOff = grade switch
        {
            "S" or "A" => "Keep shining, and I'll see you at the top of the charts!",
            "B" or "C" => "Keep practicing every day. Consistency is what makes a pro.",
            _ => "Don't give up. Every great singer was a beginner once. Run it back!",
        };

        return new VocalAssessment(
            overallScore,
            grade,
            title,
            headline,
            categories,
            strengths,
            improvements,
            tip,
            signOff,
            statistics);
    }

    public static VocalAssessment NotEnoughSinging(AssessmentContext context, double duration, double voicedSeconds)
    {
        var statistics = new PerformanceStatistics(duration, voicedSeconds, null, null, 0, null, 0, 0, false);
        var categories = Enum.GetValues<AssessmentCategory>()
            .Select(c => new CategoryScore(c, CategoryName(c), 0, "Not enough singing was detected to judge this."))
            .ToList();

        return new VocalAssessment(
            0,
            "E",
            "Mic Check Needed",
            $"Hmm, I couldn't hear much singing on \"{context.SongTitle}\". Check that your microphone is connected and selected, then give it another go!",
            categories,
            [],
            ["Sing out with confidence, and keep the mic a hand's width from your mouth."],
            "Coach Mýa's tip: use headphones for the backing track so the mic only hears your voice.",
            "I'm ready when you are. Let's hear that voice!",
            statistics);
    }

    private static string Comment(AssessmentCategory category, int score, PerformanceStatistics stats)
    {
        var tier = score >= 80 ? 2 : score >= 55 ? 1 : 0;
        var range = stats.LowestNote is not null && stats.HighestNote is not null
            ? $"{stats.LowestNote} to {stats.HighestNote}"
            : "your comfort zone";

        return category switch
        {
            AssessmentCategory.PitchAccuracy => tier switch
            {
                2 => $"Your intonation was right on the money. You landed your notes dead center (about {stats.AverageCentsOff:0} cents off on average).",
                1 => $"Your pitch was mostly there, but a few notes drifted (about {stats.AverageCentsOff:0} cents off on average). Listen closely to the track and lock in.",
                _ => "Pitch is our big homework. Hum the melody slowly first, then sing it. Your ear will catch up.",
            },
            AssessmentCategory.PitchStability => tier switch
            {
                2 => "Your sustained notes were steady and controlled. That's real vocal discipline.",
                1 => "Your held notes wobbled a little. Support them from the diaphragm, not the throat.",
                _ => "Your voice shook on the long notes. Practice long tones on 'ooh' to steady it.",
            },
            AssessmentCategory.RhythmAndTiming => tier switch
            {
                2 => "Your timing was in the pocket. You rode that groove like a pro.",
                1 => "You came in early or late on a few lines. Count the bars and breathe before each entrance.",
                _ => "We lost the beat in spots. Tap the rhythm and speak the words in time before you sing them.",
            },
            AssessmentCategory.VocalRange => tier switch
            {
                2 => $"You showed off an impressive range, from {range}!",
                1 => $"Nice range, from {range}. Keep stretching it with gentle daily scales.",
                _ => $"You stayed in a narrow zone ({range}). Don't be afraid to explore your higher and lower notes.",
            },
            AssessmentCategory.DynamicsControl => tier switch
            {
                2 => "Great dynamics. You knew when to pull back and when to let it soar.",
                1 => "Play more with dynamics. Soft verses make the big chorus hit harder.",
                _ => "Your volume control needs work. Keep the mic distance steady and shape each phrase on purpose.",
            },
            AssessmentCategory.BreathSupport => tier switch
            {
                2 => $"Your breath support carried long phrases beautifully. Your longest was {stats.LongestPhraseSeconds:0.0} seconds!",
                1 => "Your phrasing was OK, but plan your breaths so you don't run out mid-line.",
                _ => "Short, choppy phrases tell me you're running out of air. Breathe low into your belly before each line.",
            },
            _ => string.Empty,
        };
    }

    private static string CoachingTip(AssessmentCategory weakest) => weakest switch
    {
        AssessmentCategory.PitchAccuracy => "Coach Mýa's tip: record yourself, then sing along with the original and match every note. Ear training is everything.",
        AssessmentCategory.PitchStability => "Coach Mýa's tip: five minutes of lip trills and long tones every morning will make those notes rock-solid.",
        AssessmentCategory.RhythmAndTiming => "Coach Mýa's tip: practice with a metronome and dance it out first. The groove lives in your body.",
        AssessmentCategory.VocalRange => "Coach Mýa's tip: warm up with sirens from your lowest to your highest note, and never force it.",
        AssessmentCategory.DynamicsControl => "Coach Mýa's tip: mark the soft and loud moments on your lyric sheet and perform the story.",
        AssessmentCategory.BreathSupport => "Coach Mýa's tip: inhale for four, hold for four, hiss out for eight. Build that breath stamina.",
        _ => "Coach Mýa's tip: warm up before every performance.",
    };
}
