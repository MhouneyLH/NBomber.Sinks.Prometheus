using Microsoft.Extensions.Configuration;
using NBomber.Contracts;
using NBomber.Contracts.Metrics;
using NBomber.Contracts.Stats;
using OpenTelemetry;
using OpenTelemetry.Metrics;

namespace NBomber.Sinks.Otel;

/// <summary>
/// OpenTelemetry (Otel) Sink for NBomber that exports metrics to an OTLP endpoint.
/// 
/// Inspired by: https://github.com/icm-aero/NBomber.Sinks.Otel
/// </summary>
public sealed class OtelSink : IReportingSink
{
    public string SinkName => AppDiagnostics.Meter.Name;

    private MeterProvider? _meterProvider;
    private IBaseContext? _context;
    private OtelSinkConfig _sinkConfig = OtelSinkConfig.Default;

    private const int TestInfoTagsLength = 6;
    private const int AdditionalTagsLength = 2; // scenario_name and step_name

    /// <summary>
    /// Gets the custom tags attached to every metric sent to OpenTelemetry.
    /// </summary>
    public CustomTag[] CustomTags => _sinkConfig.CustomTags;

    /// <summary>
    /// Initializes a new instance of the OtelSink with the specified configuration in a programmatic way. (there is also an option to use the IConfiguration way)
    /// </summary>
    /// <param name="config">The configuration settings for the Otel Sink.</param>
    public OtelSink(OtelSinkConfig? config = null)
    {
        _sinkConfig = config ?? OtelSinkConfig.Default;
    }

    private KeyValuePair<string, object?>[] GetCustomTagsAsKeyValuePairs()
        => _sinkConfig.CustomTags.Select(tag => new KeyValuePair<string, object?>(tag.Key, tag.Value)).ToArray();

    public void Dispose()
    {
        _meterProvider?.Dispose();
    }

    public Task Init(IBaseContext context, IConfiguration infraConfig)
    {
        _context = context;

        if (_sinkConfig == OtelSinkConfig.Default)
        {
            var sinkConfigSection = infraConfig.GetSection("NBomber.Sinks.Otel");
            var configFromIConfig = sinkConfigSection.Get<OtelSinkConfig>();
            if (configFromIConfig is not null)
            {
                _sinkConfig = configFromIConfig;
            }
            else
            {
                // Try getting from root if section doesn't exist
                configFromIConfig = infraConfig.Get<OtelSinkConfig>();
                if (configFromIConfig is not null)
                {
                    _sinkConfig = configFromIConfig;
                }
            }
        }

        _context.Logger.Information("Initializing {SinkName} with configuration: {Config}", SinkName, _sinkConfig);

        var meterProviderBuilder = Sdk.CreateMeterProviderBuilder()
            .AddMeter(AppDiagnostics.Meter.Name);

        switch (_sinkConfig.ExporterType)
        {
            case ExporterTypes.Otlp:
                meterProviderBuilder.AddOtlpExporter(options =>
                {
                    options.Endpoint = new Uri(_sinkConfig.OtlpExportEndpoint);
                });
                break;
            case ExporterTypes.File:
                if (string.IsNullOrWhiteSpace(_sinkConfig.FilePath))
                {
                    throw new InvalidOperationException("FilePath must be specified when using File exporter.");
                }
                meterProviderBuilder.AddReader(new PeriodicExportingMetricReader(new FileExporter(_sinkConfig.FilePath), exportIntervalMilliseconds: 5000));
                break;
            default:
                throw new InvalidOperationException($"Unsupported exporter type: {_sinkConfig.ExporterType}");
        }

        _meterProvider = meterProviderBuilder.Build();

        _context.Logger.Information("Configured {SinkName} successfully.", SinkName);

        return Task.CompletedTask;
    }

    public Task Start(SessionStartInfo sessionInfo)
    {
        var nodeInfo = _context?.GetNodeInfo() ?? throw new InvalidOperationException("NodeInfo is not available in the current context.");

        // exclude scenario_name and step_name
        // we don't have them in the start phase where in which  
        var testInfoTags = GetCompleteArrayOfTestInfoTags()[..^2];

        AppDiagnostics.NodeCount.Set(1, testInfoTags);
        AppDiagnostics.CpuCount.Set(nodeInfo.CoresCount, testInfoTags);

        return Task.CompletedTask;
    }

    public Task SaveRealtimeStats(ScenarioStats[] stats) =>
        SaveScenarioStats(stats);

    public Task SaveFinalStats(NodeStats stats) =>
        SaveScenarioStats(stats.ScenarioStats);

    // inspired by: https://github.com/PragmaticFlow/NBomber.Sinks.InfluxDB/blob/dev/src/NBomber.Sinks.InfluxDB/InfluxDBSink.cs#L221
    public Task SaveRealtimeMetrics(MetricStats metrics)
    {
        foreach (var counter in metrics.Counters)
        {
            MapCounterMetric(counter);
        }

        foreach (var gauge in metrics.Gauges)
        {
            MapGaugeMetric(gauge);
        }

        return Task.CompletedTask;
    }

    private void MapCounterMetric(CounterStats counter)
    {
        var tags = BuildMetricTags(counter.ScenarioName, counter.MetricName, counter.UnitOfMeasure, counter.Value);
        AppDiagnostics.SetTotalRequestsCount(counter.Value, tags);
    }

    private void MapGaugeMetric(GaugeStats gauge)
    {
        var tags = BuildMetricTags(gauge.ScenarioName, gauge.MetricName, gauge.UnitOfMeasure, gauge.Value);
        AppDiagnostics.SetUsersCount(gauge.Value, tags);
    }

    private KeyValuePair<string, object?>[] BuildMetricTags(string scenarioName, string metricName, string unit, double value)
    {
        var baseTags = GetCustomTagsAsKeyValuePairs();
        var tags = new List<KeyValuePair<string, object?>>(baseTags)
        {
            new("scenario_name", scenarioName),
            new("metric_name", metricName),
            new("unit", unit),
            new("value", value)
        };
        return tags.ToArray();
    }

    public Task Stop()
    {
        _meterProvider?.ForceFlush();
        _meterProvider?.Shutdown();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Retrieves the complete array of test information tags, including custom tags.
    /// </summary>
    private Task SaveScenarioStats(ScenarioStats[] stats)
    {
        var statsSpan = stats.AsSpan();
        for (int i = 0; i < statsSpan.Length; i++)
        {
            var stat = statsSpan[i];
            MapStats(stat);
        }

        return Task.CompletedTask;
    }

    private void MapStats(ScenarioStats scenarioStats)
    {
        if (scenarioStats.StepStats.Length == 0)
        {
            RecordScenarioStats(scenarioStats);
            return;
        }

        RecordStepStats(scenarioStats);
    }

    private void RecordScenarioStats(ScenarioStats scenarioStats)
    {
        var okStats = scenarioStats.Ok;
        var failStats = scenarioStats.Fail;

        RecordStats(scenarioStats, okStats, failStats);
    }

    private void RecordStepStats(ScenarioStats scenarioStats)
    {
        var stepStats = scenarioStats.StepStats.AsSpan();
        for (int i = 0; i < stepStats.Length; i++)
        {
            var step = stepStats[i];

            var okStats = step.Ok;
            var failStats = step.Fail;

            RecordStats(scenarioStats, okStats, failStats, step);
        }
    }

    private void RecordStats(
        ScenarioStats scenarioStats,
        MeasurementStats okStats,
        MeasurementStats failStats,
        StepStats? step = null)
    {
        var allTags = GetAllTags(scenarioStats, step);

        AppDiagnostics.SetUsersCount(scenarioStats.LoadSimulationStats.Value, allTags);

        AppDiagnostics.SetTotalRps(okStats.Request.RPS + failStats.Request.RPS, allTags);
        AppDiagnostics.SetSuccessfulRps(okStats.Request.RPS, allTags);
        AppDiagnostics.SetFailedRps(failStats.Request.RPS, allTags);

        AppDiagnostics.SetTotalRequestsCount(okStats.Request.Count + failStats.Request.Count, allTags);
        AppDiagnostics.SetSuccessfulRequestsCount(okStats.Request.Count, allTags);
        AppDiagnostics.SetFailedRequestsCount(failStats.Request.Count, allTags);

        AppDiagnostics.SuccessfulRequestLatency.Record(okStats.Latency.Percent50, allTags);
        AppDiagnostics.SuccessfulRequestLatency.Record(okStats.Latency.Percent75, allTags);
        AppDiagnostics.SuccessfulRequestLatency.Record(okStats.Latency.Percent95, allTags);
        AppDiagnostics.SuccessfulRequestLatency.Record(okStats.Latency.Percent99, allTags);

        AppDiagnostics.FailedRequestLatency.Record(failStats.Latency.Percent50, allTags);
        AppDiagnostics.FailedRequestLatency.Record(failStats.Latency.Percent75, allTags);
        AppDiagnostics.FailedRequestLatency.Record(failStats.Latency.Percent95, allTags);
        AppDiagnostics.FailedRequestLatency.Record(failStats.Latency.Percent99, allTags);
    }

    private KeyValuePair<string, object?>[] GetAllTags(ScenarioStats scenarioStats, StepStats? step = null)
    {
        KeyValuePair<string, object?>[] testInfoTags = GetCompleteArrayOfTestInfoTags();

        testInfoTags[TestInfoTagsLength + CustomTags.Length] =
            new KeyValuePair<string, object?>("scenario_name", scenarioStats.ScenarioName);

        if (step is null)
        {
            return testInfoTags[..^1];
        }

        testInfoTags[TestInfoTagsLength + CustomTags.Length + 1] =
            new KeyValuePair<string, object?>("step_name", step.StepName);

        return testInfoTags;
    }

    private KeyValuePair<string, object?>[] GetCompleteArrayOfTestInfoTags()
    {
        var nodeInfo = _context?.GetNodeInfo() ?? throw new InvalidOperationException("NodeInfo is not available in the current context.");
        var testInfo = _context?.TestInfo ?? throw new InvalidOperationException("TestInfo is not available in the current context.");

        var customTags = GetCustomTagsAsKeyValuePairs();
        var testInfoTags = new KeyValuePair<string, object?>[TestInfoTagsLength + AdditionalTagsLength + customTags.Length];
        testInfoTags[0] = new KeyValuePair<string, object?>("session_id", testInfo.SessionId);
        testInfoTags[1] = new KeyValuePair<string, object?>("current_operation", nodeInfo.CurrentOperation.ToString().ToLower());
        testInfoTags[2] = new KeyValuePair<string, object?>("node_type", nodeInfo.NodeType.ToString());
        testInfoTags[3] = new KeyValuePair<string, object?>("test_suite", testInfo.TestSuite);
        testInfoTags[4] = new KeyValuePair<string, object?>("test_name", testInfo.TestName);
        testInfoTags[5] = new KeyValuePair<string, object?>("cluster_id", testInfo.ClusterId);

        // add custom tags after the test info tags
        customTags.AsSpan().CopyTo(((Span<KeyValuePair<string, object?>>)testInfoTags)[TestInfoTagsLength..]);

        return testInfoTags;
    }
}
