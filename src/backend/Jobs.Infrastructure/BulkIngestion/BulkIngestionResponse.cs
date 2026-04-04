namespace Jobs.Infrastructure.BulkIngestion;

/// <summary>Summary response for a bulk ingestion request.</summary>
public sealed class BulkIngestionResponse
{
    /// <summary>Total number of items received in the request.</summary>
    public int Received { get; set; }

    /// <summary>Number of items that were successfully processed (inserted + updated + duplicates).</summary>
    public int Processed { get; set; }

    /// <summary>Number of new job postings inserted.</summary>
    public int Inserted { get; set; }

    /// <summary>Number of existing job postings updated.</summary>
    public int Updated { get; set; }

    /// <summary>Number of items that matched an existing posting with no changes needed.</summary>
    public int Duplicates { get; set; }

    /// <summary>Number of items that failed validation and were not processed.</summary>
    public int Invalid { get; set; }

    /// <summary>Per-item validation and processing errors.</summary>
    public List<BulkIngestionItemError> Errors { get; set; } = new();
}

/// <summary>Error detail for a single item that failed during bulk ingestion.</summary>
public sealed class BulkIngestionItemError
{
    /// <summary>Zero-based index of the item in the original request.</summary>
    public int Index { get; set; }

    /// <summary>SourceJobId of the failed item, if available.</summary>
    public string? SourceJobId { get; set; }

    /// <summary>Human-readable error message.</summary>
    public string Message { get; set; } = string.Empty;
}
