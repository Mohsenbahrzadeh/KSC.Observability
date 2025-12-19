using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using System.Diagnostics.Metrics;

namespace KSC.Observability.Extensions;

    public static class OpenTelemetryExtensions
    {
        // نام meter برای custom metrics (مثل تعداد کاربران فعال)
        private static readonly Meter ActiveUsersMeter = new Meter("KSC.ActiveUsers", "1.0.0");
        public static Gauge<int> ActiveUsersGauge { get; } = ActiveUsersMeter.CreateGauge<int>("active_users", "count", "Number of active users");

        public static IHostBuilder AddKscObservability(this IHostBuilder hostBuilder, string serviceName, string otlpEndpoint = "http://localhost:4317")
        {
            hostBuilder.ConfigureServices(services =>
            {
                // Resource برای شناسایی سرویس (نام اپ، نسخه و غیره)
                var resourceBuilder = ResourceBuilder.CreateDefault()
                    .AddService(serviceName: serviceName, serviceVersion: "1.0.0");

                services.AddOpenTelemetry()
                    .ConfigureResource(resource => resource.AddService(serviceName))
                    .WithMetrics(metrics => metrics
                        // اضافه کردن instrumentation برای runtime و process
                        .AddRuntimeInstrumentation()  // متریک‌ها: process.cpu.time, process.memory.usage, process.threads.count, GC stats و غیره
                        .AddProcessInstrumentation()  // متریک‌های اضافی process مثل CPU time
                        .AddAspNetCoreInstrumentation()  // متریک‌های ASP.NET: http.server.request.duration, active requests و غیره
                        .AddHttpClientInstrumentation()  // برای HTTP calls
                        .AddMeter(ActiveUsersMeter.Name)  // اضافه کردن meter custom برای active users

                        // Export به OTLP (gRPC پیش‌فرض، اما می‌تونی http بذاری)
                        .AddOtlpExporter(options =>
                        {
                            options.Endpoint = new Uri(otlpEndpoint);  // مثلاً "http://localhost:4317" برای gRPC
                            options.Protocol = OtlpExportProtocol.Grpc;  // یا HttpProtobuf اگر http می‌خوای (پورت 4318)
                        })
                    );
            });

            return hostBuilder;
        }
    }

