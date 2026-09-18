using FindThatBook.Api.Clients;
using FindThatBook.Api.Services;

const string WebAppCorsPolicy = "FindThatBookWeb";

var builder = WebApplication.CreateBuilder(args);

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
