using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using OpenTelemetry.Logs;

var builder = WebApplication.CreateBuilder(args);

var otelCollectorUrl = builder.Configuration["OTEL_COLLECTOR_URL"] ?? "http://signoz-otel-collector.monitoring.svc.cluster.local:4317";

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;    
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService("backend")
        .AddAttributes(new Dictionary<string, object>
        {
            ["deployment.environment"] = builder.Environment.EnvironmentName
        }))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation(options =>
        {
            options.EnrichWithHttpRequest = (activity, request) =>
            {
                var ip = request.HttpContext.Connection.RemoteIpAddress?.ToString();
                activity.SetTag("http.client_ip", ip);
            };
        })
        .AddHttpClientInstrumentation()
        .AddOtlpExporter(options => {
            options.Endpoint = new Uri(otelCollectorUrl);
        }))
    .WithMetrics(metrics => metrics
        .AddRuntimeInstrumentation()
        .AddProcessInstrumentation()
        .AddOtlpExporter(options => {
            options.Endpoint = new Uri(otelCollectorUrl);
        })
    );
        
builder.Logging.AddOpenTelemetry(logging =>
{
    logging.IncludeFormattedMessage = true;
    logging.IncludeScopes = true;
    logging.AddOtlpExporter(options =>
    {
        options.Endpoint = new Uri(otelCollectorUrl);
    });
});


builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

builder.Services.AddHealthChecks();

var app = builder.Build();
app.UseForwardedHeaders();

app.UseCors();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapHealthChecks("/health");
app.MapGet("/debug-ip", (HttpContext context) => 
    new { 
        RemoteIp = context.Connection.RemoteIpAddress?.ToString(),
        ForwardedFor = context.Request.Headers["X-Forwarded-For"].ToString()
    });
    
app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

app.MapPost("/stress-cpu", () =>
{
    var duration = TimeSpan.FromSeconds(10);
    var end = DateTime.UtcNow.Add(duration);
    
    while (DateTime.UtcNow < end)
    {
        // CPU-intensive calculation
        Math.Sqrt(Random.Shared.NextDouble());
    }
    
    return "CPU stress test completed";
});

app.MapPost("/stress-memory", () =>
{
    var list = new List<byte[]>();
    for (int i = 0; i < 7; i++)
    {
        list.Add(new byte[10_000_000]); // 10MB each
    }
    Thread.Sleep(10000);
    return "Memory stress completed";
});

app.MapPost("/stress-disk", () =>
{
    var path = Path.GetTempFileName();
    using (var fs = new FileStream(path, FileMode.Create))
    {
        var buffer = new byte[1024 * 1024]; // 1MB
        for (int i = 0; i < 200; i++)
        {
            fs.Write(buffer, 0, buffer.Length);
        }
    }
    File.Delete(path);
    return "Disk stress completed";
});

app.MapPost("/stress-io", async () =>
{
    var tasks = new List<Task>();
    for (int i = 0; i < 10; i++)
    {
        tasks.Add(Task.Run(async () => {
            using var client = new HttpClient();
            await client.GetStringAsync("https://httpbin.org/delay/1");
        }));
    }
    await Task.WhenAll(tasks);
    return "IO stress completed";
});


app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
