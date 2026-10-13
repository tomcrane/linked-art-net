using LinkedArtNet.ExamplesSite.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.Configure<ExamplesOptions>(builder.Configuration.GetSection("Examples"));
builder.Services.Configure<TimeSpanServiceOptions>(builder.Configuration.GetSection("TimeSpanService"));
builder.Services.AddHttpClient();
builder.Services.AddSingleton<ExampleIndex>();
builder.Services.AddSingleton<DateClient>();
builder.Services.AddSingleton<CorpusIndex>();
builder.Services.AddRazorPages();
builder.Services.AddResponseCaching();

var app = builder.Build();
if (!app.Environment.IsDevelopment()) app.UseExceptionHandler("/error");
app.UseStaticFiles();
app.UseResponseCaching();
app.MapRazorPages();
app.MapGet("/healthz", (ExampleIndex index) => Results.Ok(new { examples = index.Total, implemented = index.Implemented }));
app.Run();

public partial class Program;
