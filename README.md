# NBomber Otel Sink

[![CI](https://github.com/MhouneyLH/NBomber.Sinks.Otel/actions/workflows/ci-cd.yml/badge.svg)](https://github.com/MhouneyLH/NBomber.Sinks.Otel/actions/workflows/ci-cd.yml)
[![NuGet Version](https://img.shields.io/nuget/v/NBomber.Sinks.Otel.svg)](https://www.nuget.org/packages/NBomber.Sinks.Otel/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/NBomber.Sinks.Otel.svg)](https://www.nuget.org/packages/NBomber.Sinks.Otel/)
[![GitHub stars](https://img.shields.io/github/stars/MhouneyLH/NBomber.Sinks.Otel.svg)](https://github.com/MhouneyLH/NBomber.Sinks.Otel/stargazers)

NBomber Otel Sink is a custom sink for NBomber load-testing framework. It integrates with OpenTelemetry, a popular observability standard, to export load test metrics to various backends, including Prometheus and others.

**NBomber.Sinks.Otel** utilizes OpenTelemetry to export metrics based on the configured exporter type. During the execution of your load test, metrics are collected and exported according to the specified configuration.

## Features

- Integrates NBomber with OpenTelemetry for monitoring load test metrics.
- Supports multiple exporters: OTLP and File.
- Provides predefined metrics for request latency, request counts, RPS, and more.
- Supports custom tags for fine-grained metric grouping.
- Easy setup and configuration.

## Installation

To install the NBomber Otel Sink, use the following command:

```bash
dotnet add package NBomber.Sinks.Otel
```

## Usage

To use the NBomber Otel Sink, follow these steps:

1. Set up your load test scenario using NBomber.
2. Configure NBomber to use the Otel sink with your desired exporter.
3. Run your load test.

For more details on configuring and using the NBomber Otel Sink, refer to the [samples](samples).

## Configuration

The Otel Sink supports two types of exporters:

### OTLP Exporter (Default)

Exports metrics via OTLP protocol to an OpenTelemetry Collector or compatible backend.

```json
{
  "NBomber.Sinks.Otel": {
    "ExporterType": "Otlp",
    "OtlpExportEndpoint": "http://localhost:9500",
    "CustomTags": [
      {
        "Key": "Environment",
        "Value": "Local"
      }
    ]
  }
}
```

### File Exporter

Writes metrics to a text file for simple logging and analysis.

```json
{
  "NBomber.Sinks.Otel": {
    "ExporterType": "File",
    "FilePath": "metrics.log",
    "CustomTags": [
      {
        "Key": "Environment",
        "Value": "Local"
      }
    ]
  }
}
```

## Code Samples

Here's an example of how to set up a load test scenario with the NBomber Otel Sink:

```csharp
using NBomber.CSharp;
using NBomber.Sinks.Otel;

var scenario = Scenario.Create("hello_world_scenario", async context =>
{
    await Task.Delay(1000);
    return Response.Ok();
})
.WithoutWarmUp()
.WithLoadSimulations(
    Simulation.RampingInject(rate: 150, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromMinutes(1)),
    Simulation.Inject(rate: 150, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(30)),
    Simulation.RampingInject(rate: 0, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromMinutes(1))
);

NBomberRunner
    .RegisterScenarios(scenario)
    .WithReportingInterval(TimeSpan.FromSeconds(5))
    .WithReportingSinks(new OtelSink())
    .LoadInfraConfig("infra-config-otlp.json")
    .Run();
```

For more code samples and examples, please refer to the [samples](samples) directory.

## Known Issues & Limitations

When using Rider and want or any other ide and try to start with a "green play button" it will not work when using `LoadInfraConfig`, please create Program.cs with Main method and run from terminal using `dotnet run`

The export interval is currently fixed to the OtelExporter default of `60` seconds and cannot be changed via configuration (the only thing that can be changed is the reporting interval in NBomber using `WithReportingInterval` which is independent from the export interval)

## Contributing

Contributions are welcome! If you find a bug or want to add a new feature, please open an issue or submit a pull request. Happy coding! 🎉

## License

This project is licensed under the MIT License. See the [LICENSE](LICENSE.txt) file for details.
