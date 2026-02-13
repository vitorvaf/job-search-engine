public sealed class WorkerRunOptions
{
    public bool RunOnce { get; init; }
    public string? SourceFilter { get; init; }
    public int? MaxItems { get; init; }
    public int? MaxDetail { get; init; }
}
