using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using LinkedArtNet;
using LinkedArtNet.Dates;
using LinkedArtNet.Dates.Canonical;
using LinkedArtNet.Dates.Periods;
using LinkedArtNet.Dates.Realisation;
using LinkedArtNet.Dates.Service;
using LinkedArtNet.Dates.Text;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.RateLimiting;
using Scalar.AspNetCore;

// `--healthcheck`: used by the container HEALTHCHECK, since the chiseled image has no curl.
if (args.Contains("--healthcheck"))
{
    try
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        var port = Environment.GetEnvironmentVariable("ASPNETCORE_HTTP_PORTS")?.Split(';')[0] ?? "8080";
        var response = await client.GetAsync($"http://localhost:{port}/healthz");
        return response.IsSuccessStatusCode ? 0 : 1;
    }
    catch { return 1; }
}

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ParserOptions>(builder.Configuration.GetSection("Parser"));
builder.Services.Configure<CaptureOptions>(builder.Configuration.GetSection("Capture"));
builder.Services.AddSingleton<ParserHost>();
builder.Services.AddSingleton<CaptureWriter>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<CaptureWriter>());
builder.Services.Configure<JsonOptions>(o =>
{
    o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    o.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
    o.SerializerOptions.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
});
builder.Services.AddOpenApi("v1", o => o.AddDocumentTransformer((doc, _, _) =>
{
    doc.Info.Title = "LinkedArtNet.Dates service";
    doc.Info.Description = "Free-text date strings to Linked Art TimeSpans. Deterministic: the same input, profile and parser version always give the same answer.";
    return Task.CompletedTask;
}));
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().WithMethods("GET", "POST").AllowAnyHeader().WithExposedHeaders("ETag")));
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    var perMinute = builder.Configuration.GetSection("Parser").GetValue<int?>("RequestsPerMinute") ?? 600;
    o.AddPolicy("per-ip", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = perMinute, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
builder.Services.AddHealthChecks().AddCheck<ParserHealthCheck>("parser");

var app = builder.Build();
var host = app.Services.GetRequiredService<ParserHost>();
var capture = app.Services.GetRequiredService<CaptureWriter>();
var limits = host.Options;

app.UseCors();
app.UseRateLimiter();
app.MapOpenApi("/openapi/{documentName}.json");
app.MapScalarApiReference("/docs", o => o.WithTitle("LinkedArtNet.Dates service").WithOpenApiRoutePattern("/openapi/{documentName}.json"));
app.MapHealthChecks("/healthz");

var v1 = app.MapGroup("/v1").RequireRateLimiting("per-ip");

// ---- GET /v1/timespan?s=...&intent=&profile=&shape=&llm= ------------------------------------------------------------
v1.MapGet("/timespan", (HttpContext http, string? s, string? intent, string? profile, string? shape, bool? llm, bool? emitInnerBounds, string? labelPolicy) =>
{
    if (string.IsNullOrWhiteSpace(s)) return Results.BadRequest(new ErrorResponse("Missing query parameter 's'."));
    if (s.Length > limits.MaxInputLength) return Results.BadRequest(new ErrorResponse($"Input longer than {limits.MaxInputLength} characters."));
    if (!Parsing.TryIntent(intent, out var intentValue)) return Results.BadRequest(new ErrorResponse($"Unknown intent '{intent}'. Use auto, single or pair."));
    if (!host.TryGetProfile(profile, out var profileValue)) return Results.NotFound(new ErrorResponse($"Unknown profile '{profile}'.", "See /v1/profiles."));
    if (!Parsing.TryLabelPolicy(labelPolicy, out var labelPolicyValue)) return Results.BadRequest(new ErrorResponse($"Unknown labelPolicy '{labelPolicy}'. Use label, labelAndName or name."));
    shape ??= "full";
    if (shape is not ("full" or "timespan")) return Results.BadRequest(new ErrorResponse($"Unknown shape '{shape}'. Use full or timespan."));

    var parser = host.ParserFor(profileValue, emitInnerBounds, labelPolicyValue);
    var result = host.Parse(parser, s, intentValue);
    capture.Consider(result);
    var dto = TimeSpanResultDto.From(result, null);
    if (llm == true) dto = dto with { Notes = dto.Notes.Append("llm=true was ignored: this service is deterministic by design; the LLM interpreter proposes readings for review in the corpus trainer, not at request time.").ToList() };

    var etag = ParserHost.ETag(parser.Profile.Name, intentValue, s, shape);
    if (http.Request.Headers.IfNoneMatch.Any(v => v == etag)) return Results.StatusCode(StatusCodes.Status304NotModified);
    http.Response.Headers.ETag = etag;
    http.Response.Headers.CacheControl = $"public, max-age={limits.CacheMaxAgeSeconds}";
    return shape == "timespan" ? Results.Json(dto.SpansOnly()) : Results.Ok(dto);
})
.WithName("ParseOne")
.WithSummary("Parse one date string")
.WithDescription("Returns the parse result; unreadable text gives status 'unparsed' with HTTP 200. Only malformed requests are 4xx.")
.Produces<TimeSpanResultDto>()
.Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
.Produces<ErrorResponse>(StatusCodes.Status404NotFound);

// ---- POST /v1/timespan -----------------------------------------------------------------------------------------------
v1.MapPost("/timespan", (HttpContext http, BatchRequest request) =>
{
    http.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>()?.MaxRequestBodySize = limits.MaxRequestBodyBytes;
    if (request.Inputs is null || request.Inputs.Count == 0) return Results.BadRequest(new ErrorResponse("'inputs' must hold at least one string or object."));
    if (request.Inputs.Count > limits.MaxInputsPerRequest) return Results.BadRequest(new ErrorResponse($"At most {limits.MaxInputsPerRequest} inputs per request."));
    if (!Parsing.TryIntent(request.Intent, out var defaultIntent)) return Results.BadRequest(new ErrorResponse($"Unknown intent '{request.Intent}'."));
    if (!host.TryGetProfile(request.Profile, out var defaultProfile)) return Results.NotFound(new ErrorResponse($"Unknown profile '{request.Profile}'.", "See /v1/profiles."));
    if (!Parsing.TryLabelPolicy(request.Options?.LabelPolicy, out var labelPolicy)) return Results.BadRequest(new ErrorResponse($"Unknown labelPolicy '{request.Options?.LabelPolicy}'."));

    var results = new List<TimeSpanResultDto>(request.Inputs.Count);
    var index = 0;
    foreach (var item in request.Inputs)
    {
        string? text; string? reference = null; var itemIntent = defaultIntent; var itemProfile = defaultProfile;
        switch (item.ValueKind)
        {
            case JsonValueKind.String:
                text = item.GetString();
                break;
            case JsonValueKind.Object:
                text = item.TryGetProperty("s", out var sEl) && sEl.ValueKind == JsonValueKind.String ? sEl.GetString() : null;
                if (item.TryGetProperty("ref", out var refEl) && refEl.ValueKind == JsonValueKind.String) reference = refEl.GetString();
                if (item.TryGetProperty("intent", out var inEl) && inEl.ValueKind == JsonValueKind.String && !Parsing.TryIntent(inEl.GetString(), out itemIntent))
                    return Results.BadRequest(new ErrorResponse($"inputs[{index}]: unknown intent '{inEl.GetString()}'."));
                if (item.TryGetProperty("profile", out var prEl) && prEl.ValueKind == JsonValueKind.String && !host.TryGetProfile(prEl.GetString(), out itemProfile))
                    return Results.NotFound(new ErrorResponse($"inputs[{index}]: unknown profile '{prEl.GetString()}'."));
                break;
            default:
                return Results.BadRequest(new ErrorResponse($"inputs[{index}] must be a string or an object with 's'."));
        }
        if (text is null) return Results.BadRequest(new ErrorResponse($"inputs[{index}] has no 's'."));
        if (text.Length > limits.MaxInputLength) return Results.BadRequest(new ErrorResponse($"inputs[{index}] is longer than {limits.MaxInputLength} characters."));

        var parser = host.ParserFor(itemProfile, request.Options?.EmitInnerBounds, labelPolicy);
        var result = host.Parse(parser, text, itemIntent);
        capture.Consider(result);
        results.Add(TimeSpanResultDto.From(result, reference));
        index++;
    }
    return Results.Ok(new BatchResponse(results));
})
.WithName("ParseMany")
.WithSummary("Parse many date strings")
.WithDescription("Body: { profile?, intent?, options?: { emitInnerBounds?, labelPolicy? }, inputs: [ \"1984\", { \"s\": \"1850-1920\", \"intent\": \"pair\", \"ref\": \"row-17\" } ] }. Results come back in input order with 'ref' echoed.")
.Accepts<BatchRequest>("application/json")
.Produces<BatchResponse>()
.Produces<ErrorResponse>(StatusCodes.Status400BadRequest);

// ---- POST /v1/advise -------------------------------------------------------------------------------------------------
v1.MapPost("/advise", async (HttpContext http, CancellationToken ct) =>
{
    http.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>()?.MaxRequestBodySize = limits.MaxAdviseBodyBytes;
    IReadOnlyList<string>? inputs; IReadOnlyList<string>? languages = null;
    if (http.Request.ContentType?.StartsWith("text/plain", StringComparison.OrdinalIgnoreCase) == true)
    {
        using var reader = new StreamReader(http.Request.Body);
        inputs = (await reader.ReadToEndAsync(ct)).Split('\n').Select(l => l.TrimEnd('\r')).ToList();
    }
    else
    {
        AdviseRequest? request;
        try { request = await http.Request.ReadFromJsonAsync<AdviseRequest>(ct); }
        catch (JsonException ex) { return Results.BadRequest(new ErrorResponse("Body must be JSON { \"inputs\": [...] } or text/plain with one string per line.", ex.Message)); }
        inputs = request?.Inputs; languages = request?.Languages;
    }
    inputs = inputs?.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
    if (inputs is null || inputs.Count == 0) return Results.BadRequest(new ErrorResponse("'inputs' must hold at least one string."));
    if (inputs.Count > limits.MaxAdviseInputs) return Results.BadRequest(new ErrorResponse($"At most {limits.MaxAdviseInputs} strings per request."));
    if (languages is { Count: > 0 } && languages.FirstOrDefault(l => !Lexicon.Exists(l)) is { } unknown)
        return Results.BadRequest(new ErrorResponse($"Unknown language '{unknown}'.", "Lexicons: " + string.Join(", ", Lexicon.Codes)));
    var kept = inputs.Where(s => s.Length <= limits.MaxInputLength).ToList();
    var report = ProfileAdvisor.Analyse(kept, languages is { Count: > 0 } ? languages : null);
    return Results.Ok(AdviceDto.From(report, inputs.Count - kept.Count));
})
.WithName("Advise")
.WithSummary("Recommend a profile for a dataset")
.WithDescription("Looks at a column of date strings before parsing and reports the conventions in use (band words, hedges, era words, century notations, numeric date order, two-digit years, brackets, seasons, open ends, other languages), each with a count and example strings, then recommendations and a suggested profile stated as changes from 'default'. Body: { \"inputs\": [ ... ], \"languages\"?: [ \"en\", \"fr\" ] } or text/plain, one string per line. Nothing is stored.")
.Accepts<AdviseRequest>("application/json", "text/plain")
.Produces<AdviceDto>()
.Produces<ErrorResponse>(StatusCodes.Status400BadRequest);

// ---- GET /v1/agent?s=...&profile= ---------------------------------------------------------------------------------------
v1.MapGet("/agent", (HttpContext http, string? s, string? profile, bool? agent) =>
{
    if (string.IsNullOrWhiteSpace(s)) return Results.BadRequest(new ErrorResponse("Missing query parameter 's'."));
    if (s.Length > limits.MaxInputLength) return Results.BadRequest(new ErrorResponse($"Input longer than {limits.MaxInputLength} characters."));
    if (!host.TryAgentParser(profile, out var parser)) return Results.NotFound(new ErrorResponse($"Unknown agent profile '{profile}'.", "See /v1/agent/profiles."));
    var parsed = parser.Parse(s);
    var etag = ParserHost.ETag("agent:" + parser.Profile.Name, Intent.Auto, s, agent == false ? "parts" : "full");
    if (http.Request.Headers.IfNoneMatch.Any(v => v == etag)) return Results.StatusCode(StatusCodes.Status304NotModified);
    http.Response.Headers.ETag = etag;
    http.Response.Headers.CacheControl = $"public, max-age={limits.CacheMaxAgeSeconds}";
    return Results.Ok(LinkedArtNet.Agents.AgentResultDto.From(parsed, withAgent: agent != false));
})
.WithName("ParseAgent")
.WithSummary("Read an agent string: name, dates, honorifics, roles, nationality")
.WithDescription("\"Grierson, Herbert John Clifford [Sir, 1866-1960] (preface by)\", \"Dutch painter, 1606-1669\", \"attributed to Rembrandt\". The date part is read by the dates parser with intent pair; 'agent=false' leaves out the Linked Art Person or Group JSON. Profiles: default, dscribe, marc, ulan, ead, wellcome.")
.Produces<LinkedArtNet.Agents.AgentResultDto>()
.Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
.Produces<ErrorResponse>(StatusCodes.Status404NotFound);

v1.MapGet("/agent/profiles", () => Results.Ok(LinkedArtNet.Agents.AgentProfileCatalog.BuiltIn.Profiles.Values
        .OrderBy(p => p.Name == "default" ? "" : p.Name, StringComparer.Ordinal)
        .Select(p => new { p.Name, p.Description, p.DatesProfile, p.NameRequired, p.SemicolonForm })))
    .WithName("ListAgentProfiles").WithSummary("List the agent-string profiles");

// ---- profiles --------------------------------------------------------------------------------------------------------
v1.MapGet("/profiles", () => Results.Ok(host.Catalog.Profiles.Values
        .OrderBy(p => p.Name == "default" ? "" : p.Name, StringComparer.Ordinal)
        .Select(p => new ProfileSummary(p.Name, p.Description, p.Languages, TimeSpanResultDto.Camel(p.CenturyConvention.ToString())))))
    .WithName("ListProfiles").WithSummary("List the available parse profiles");

v1.MapGet("/profiles/{name}", (string name) => host.TryGetProfile(name, out var p)
        ? Results.Content(p.ToJson(), "application/json")
        : Results.NotFound(new ErrorResponse($"Unknown profile '{name}'.")))
    .WithName("GetProfile").WithSummary("A profile's effective settings");

// ---- GET /v1/canonical?e=... -----------------------------------------------------------------------------------------
v1.MapGet("/canonical", (HttpContext http, string? e, string? intent, string? profile) =>
{
    if (string.IsNullOrWhiteSpace(e)) return Results.BadRequest(new ErrorResponse("Missing query parameter 'e' (a canonical expression)."));
    if (e.Length > limits.MaxInputLength) return Results.BadRequest(new ErrorResponse($"Expression longer than {limits.MaxInputLength} characters."));
    if (!Parsing.TryIntent(intent, out var intentValue)) return Results.BadRequest(new ErrorResponse($"Unknown intent '{intent}'."));
    if (!host.TryGetProfile(profile, out var profileValue)) return Results.NotFound(new ErrorResponse($"Unknown profile '{profile}'."));
    if (!CanonicalReader.TryParse(e, out var expression, out var error, host.Periods))
        return Results.BadRequest(new ErrorResponse("Not a canonical expression.", error));
    var result = new TimeSpanRealiser(profileValue).Realise(expression!, e, intentValue);
    http.Response.Headers.CacheControl = $"public, max-age={limits.CacheMaxAgeSeconds}";
    return Results.Ok(TimeSpanResultDto.From(result, null));
})
.WithName("RealiseCanonical").WithSummary("Realise a canonical expression without parsing free text")
.Produces<TimeSpanResultDto>().Produces<ErrorResponse>(StatusCodes.Status400BadRequest);

// ---- /version --------------------------------------------------------------------------------------------------------
app.MapGet("/version", () =>
{
    var service = typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";
    var model = typeof(LinkedArtObject).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";
    var git = Environment.GetEnvironmentVariable("GIT_SHA");
    if (git is null && service.Contains('+')) git = service[(service.IndexOf('+') + 1)..];
    return Results.Ok(new VersionInfo(service, TimeSpanParser.Version, model, git, host.Catalog.Profiles.Keys.Order(StringComparer.Ordinal).ToList(), JsonNamedPeriodSource.BuiltIn.Tables.Count, host.PeriodOVersion));
}).WithName("Version").WithSummary("Service, parser and model versions");

app.MapGet("/", () => Results.Redirect("/docs")).ExcludeFromDescription();

app.Run();
return 0;

/// <summary>Exposed for WebApplicationFactory in the tests.</summary>
public partial class Program;

/// <summary>Liveness: the parser reads "1984" and the profile catalog is loaded.</summary>
public sealed class ParserHealthCheck(ParserHost host) : Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck
{
    public Task<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult> CheckHealthAsync(Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var ok = host.TryGetProfile("default", out var profile) && host.Parse(host.ParserFor(profile, null, null), "1984", Intent.Auto).Status == ParseStatus.Parsed;
        return Task.FromResult(ok
            ? Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy($"{host.Catalog.Profiles.Count} profiles")
            : Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy("The parser could not read '1984'."));
    }
}
