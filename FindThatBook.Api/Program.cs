using FindThatBook.Api.Clients;
using FindThatBook.Api.Services;

const string WebAppCorsPolicy = "FindThatBookWeb";

var builder = WebApplication.CreateBuilder(args);

// GeminiClient is built lazily, so without this check a bad key only surfaces on first search.
if (string.IsNullOrWhiteSpace(builder.Configuration["Gemini:ApiKey"]))
{
    throw new InvalidOperationException(
        "Gemini:ApiKey is missing or empty. Set it with: " +
        "dotnet user-secrets set \"Gemini:ApiKey\" \"<your-key>\" --project FindThatBook.Api");
}

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddHttpClient<IOpenLibraryClient, OpenLibraryClient>();
builder.Services.AddHttpClient<IGeminiClient, GeminiClient>();

builder.Services.AddScoped<IQueryParser, QueryParser>();
builder.Services.AddScoped<IBookMatcher, BookMatcher>();
builder.Services.AddScoped<IExplanationService, ExplanationService>();
builder.Services.AddScoped<ISearchService, SearchService>();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy(WebAppCorsPolicy, policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod());
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// CORS before the HTTPS redirect: a preflight that gets a 307 instead of CORS headers fails.
app.UseCors(WebAppCorsPolicy);
app.UseHttpsRedirection();

// The front end pings this on load, waking the free-tier instance while the user types.
app.MapGet("/health", () => Results.Ok());

app.MapControllers();

app.Run();
