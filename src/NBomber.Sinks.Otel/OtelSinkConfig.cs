namespace NBomber.Sinks.Otel;

/// <summary>
/// Configuration settings for the OpenTelemetry (Otel) Sink in NBomber.
/// </summary>
public sealed record OtelSinkConfig
{
    /// <summary>
    /// The OTLP endpoint where the Otel Sink exports metrics, to be scraped by Prometheus
    /// or any other metrics backend using an Otel Collector.
    /// </summary>
    public string OtlpExportEndpoint { get; set; } = "http://localhost:9464/metrics";

    /// <summary>
    /// The type of exporter to use for metrics.
    /// </summary>
    public string ExporterType { get; set; } = "Otlp";

    /// <summary>
    /// The file path for file exporter. Only used when ExporterType is File.
    /// </summary>
    public string? FilePath { get; set; }

    /// <summary>
    /// All custom tags that will be added to each metric exported by the Otel Sink.
    /// </summary>
    public CustomTag[] CustomTags { get; set; } = [];

    /// <summary>
    /// The default configuration for the Otel Sink. Used for figure out if the user provided custom config or not.
    /// </summary>
    public static OtelSinkConfig Default { get; } = new();
}

/// <summary>
/// Enum for different exporter types supported by the Otel Sink.
/// </summary>
public static class ExporterTypes
{
    /// <summary>
    /// Export metrics via OTLP protocol.
    /// </summary>
    public const string Otlp = "Otlp";

    /// <summary>
    /// Export metrics to a text file.
    /// </summary>
    public const string File = "File";
}
