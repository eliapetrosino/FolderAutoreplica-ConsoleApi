public class Config
{
    public required string Source { get; init; }
    public required string Destination { get; init; }
    public int MaxRetries { get; init; }
    public int RetryDelayMillis { get; init; }
}