using OpenTelemetry;
using OpenTelemetry.Metrics;
using System.Text;

namespace NBomber.Sinks.Otel;

/// <summary>
/// A simple file exporter for OpenTelemetry metrics that writes metrics to a text file.
/// </summary>
internal sealed class FileExporter : BaseExporter<Metric>
{
    private readonly string _filePath;

    public FileExporter(string filePath)
    {
        _filePath = filePath;
    }

    public override ExportResult Export(in Batch<Metric> batch)
    {
        try
        {
            using var writer = new StreamWriter(_filePath, append: true);
            var sb = new StringBuilder();

            sb.AppendLine($"[{DateTime.UtcNow:O}] Metrics Export:");

            foreach (var metric in batch)
            {
                sb.AppendLine($"  Metric: {metric.Name} ({metric.Description})");
                sb.AppendLine("  Data Points:");
                foreach (var dataPoint in metric.GetMetricPoints())
                {
                    sb.Append("    - ");

                    // Handle different metric types
                    try
                    {
                        switch (metric.MetricType)
                        {
                            case MetricType.LongSum:
                            case MetricType.LongSumNonMonotonic:
                                sb.Append($"Sum: {dataPoint.GetSumLong()} | ");
                                break;
                            case MetricType.DoubleSum:
                            case MetricType.DoubleSumNonMonotonic:
                                sb.Append($"Sum: {dataPoint.GetSumDouble()} | ");
                                break;
                            case MetricType.LongGauge:
                                sb.Append($"Gauge: {dataPoint.GetGaugeLastValueLong()} | ");
                                break;
                            case MetricType.DoubleGauge:
                                sb.Append($"Gauge: {dataPoint.GetGaugeLastValueDouble()} | ");
                                break;
                            case MetricType.Histogram:
                                sb.Append($"Histogram - Count: {dataPoint.GetHistogramCount()}, Sum: {dataPoint.GetHistogramSum()} | ");
                                break;
                            default:
                                sb.Append($"Unknown metric type: {metric.MetricType} | ");
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        sb.Append($"Error getting value: {ex.Message} | ");
                    }

                    sb.Append("Tags: ");
                    foreach (var tag in dataPoint.Tags)
                    {
                        sb.Append($"{tag.Key}={tag.Value}, ");
                    }
                    if (dataPoint.Tags.Count > 0)
                    {
                        sb.Length -= 2; // Remove trailing comma and space
                    }
                    sb.AppendLine();
                }
                sb.AppendLine("  Metrics exported");
            }

            writer.Write(sb.ToString());
            return ExportResult.Success;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error exporting metrics to file: {ex.Message}");
            return ExportResult.Failure;
        }
    }
}