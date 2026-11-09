using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using SalePlius.Client;
using SalePlius.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBaseUrl = builder.Configuration["ApiBaseUrl"]
    ?? throw new InvalidOperationException("ApiBaseUrl is missing in wwwroot/appsettings.json");

// Auth State Provider & Authorization.
// Singleton on purpose: HttpClient message handlers (AuthHeaderHandler) are resolved in their own
// DI scope, so a scoped provider would give them a separate copy that never sees the login.
builder.Services.AddSingleton<CustomAuthStateProvider>();
builder.Services.AddSingleton<AuthenticationStateProvider>(sp => sp.GetRequiredService<CustomAuthStateProvider>());
builder.Services.AddAuthorizationCore();

// HTTP Client with Auth Header DelegatingHandler
builder.Services.AddTransient<AuthHeaderHandler>();
builder.Services.AddScoped<SavedStore>();
builder.Services.AddScoped<CategoryStore>();

// Refreshing tokens uses its own client WITHOUT AuthHeaderHandler, so a refresh never triggers another refresh.
builder.Services.AddHttpClient("auth", client => client.BaseAddress = new Uri(apiBaseUrl));
builder.Services.AddSingleton<TokenRefresher>();
builder.Services.AddHttpClient<ApiService>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
}).AddHttpMessageHandler<AuthHeaderHandler>();

await builder.Build().RunAsync();
