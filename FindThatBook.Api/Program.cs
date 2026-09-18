using FindThatBook.Api.Clients;
using FindThatBook.Api.Services;

const string WebAppCorsPolicy = "FindThatBookWeb";

var builder = WebApplication.CreateBuilder(args);

// GeminiClient is only constructed on the first search, so check the key here
// or a misconfigured app starts fine and fails on first use.
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

builder.Services.AddCors(options =>
{
    options.AddPolicy(WebAppCorsPolicy, policy => policy
        .WithOrigins("http://localhost:5173")
        .AllowAnyHeader()
        .AllowAnyMethod());
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// CORS goes before the HTTPS redirect: a browser preflight that gets a 307
// instead of the CORS headers fails the whole request.
app.UseCors(WebAppCorsPolicy);
app.UseHttpsRedirection();

app.MapControllers();

app.Run();
