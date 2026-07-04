using BlazorApp1.Components;
using BlazorApp1.Services;
using DotNetEnv;
using Radzen;

// Muat file .env ke environment variables (menelusuri folder ke atas
// sampai ketemu .env). Harus dipanggil SEBELUM CreateBuilder agar
// nilainya sudah ada saat konfigurasi dibangun.
Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(options => options.DetailedErrors = true);

builder.Services.AddRadzenComponents();
builder.Services.AddScoped<SapQueryService>();
builder.Services.AddScoped<DbManagerService>();
builder.Services.AddScoped<UserService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
