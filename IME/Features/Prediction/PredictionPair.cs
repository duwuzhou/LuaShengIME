namespace IME.Features.Prediction;

internal readonly struct PredictionPair
{
    public string Previous { get; }
    public string Next { get; }

    public PredictionPair(string previous, string next)
    {
        Previous = previous ?? string.Empty;
        Next = next ?? string.Empty;
    }
}
