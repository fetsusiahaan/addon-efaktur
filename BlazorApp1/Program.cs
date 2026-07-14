using System.Security.Claims;
using BlazorApp1.Components;
using BlazorApp1.Services;
using DotNetEnv;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
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
builder.Services.AddScoped<PajakKeluaranService>();
builder.Services.AddScoped<WebDbService>();
builder.Services.AddScoped<EFakturExportService>();
builder.Services.AddScoped<DbManagerService>();
builder.Services.AddScoped<UserService>();

// ── Autentikasi & Otorisasi ────────────────────────────────────────────────
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "EFaktur.Auth";
        options.Cookie.HttpOnly = true;                       // tak bisa dibaca JS (anti-XSS)
        options.Cookie.SameSite = SameSiteMode.Lax;           // anti-CSRF, tetap jalan di HTTP
        // SameAsRequest: cookie ditandai Secure hanya bila diakses via HTTPS,
        // sehingga tetap berfungsi saat deploy HTTP (mis. IIS http://host:5343).
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/denied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<AuthenticationStateProvider, ServerAuthStateProvider>();

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

app.UseAuthentication();

// ── Pelacakan "halaman aktif terakhir" + redirect /login untuk user login ──
// Dijalankan di level HTTP (bukan komponen) sehingga redirect bersih tanpa
// NavigationException. Untuk user yang SUDAH login:
//   - membuka /login  -> dialihkan ke halaman aktif terakhir (atau default role)
//   - setiap GET halaman biasa disimpan ke cookie "efaktur_last"
app.Use(async (ctx, next) =>
{
    var authed = ctx.User.Identity?.IsAuthenticated == true;
    var isGet = HttpMethods.IsGet(ctx.Request.Method);
    var path = ctx.Request.Path.Value ?? "/";

    if (authed && isGet)
    {
        // Sudah login lalu membuka /login -> kembali ke halaman sebelumnya.
        if (path.Equals("/login", StringComparison.OrdinalIgnoreCase))
        {
            var last = ctx.Request.Cookies["efaktur_last"];
            var target = IsSafeLocalPath(last) ? last! : DefaultFor(ctx.User);
            ctx.Response.Redirect(target);
            return;
        }

        // Rekam halaman navigasi terakhir — hanya bila render SUKSES (200 HTML),
        // supaya percobaan ke halaman terlarang (302) / 404 tidak ikut tersimpan.
        if (IsNavigablePage(path))
        {
            var full = path + ctx.Request.QueryString;
            ctx.Response.OnStarting(() =>
            {
                if (ctx.Response.StatusCode == 200 &&
                    (ctx.Response.ContentType?.Contains("text/html", StringComparison.OrdinalIgnoreCase) ?? false))
                {
                    ctx.Response.Cookies.Append("efaktur_last", full,
                        new CookieOptions
                        {
                            HttpOnly = true,
                            Secure = ctx.Request.IsHttps,
                            SameSite = SameSiteMode.Strict,
                            IsEssential = true,
                            MaxAge = TimeSpan.FromHours(8),
                        });
                }
                return Task.CompletedTask;
            });
        }
    }

    await next();
});

app.UseAuthorization();

app.UseAntiforgery();

// Logout: sign-out cookie lalu kembali ke login. Hanya POST + wajib
// menyertakan token antiforgery yang valid (anti-CSRF).
app.MapPost("/Account/Logout",
    async (HttpContext http, Microsoft.AspNetCore.Antiforgery.IAntiforgery antiforgery) =>
{
    try
    {
        await antiforgery.ValidateRequestAsync(http);
    }
    catch (Microsoft.AspNetCore.Antiforgery.AntiforgeryValidationException)
    {
        return Results.BadRequest("Token antiforgery tidak valid.");
    }

    await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.LocalRedirect("/login");
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

// ── Helper ──────────────────────────────────────────────────────────────────

// Hanya izinkan path lokal yang aman (cegah open-redirect) & bukan /login.
static bool IsSafeLocalPath(string? path)
{
    if (string.IsNullOrWhiteSpace(path)) return false;
    if (!path.StartsWith('/') || path.StartsWith("//") || path.StartsWith("/\\")) return false;
    if (path.StartsWith("/login", StringComparison.OrdinalIgnoreCase)) return false;
    return true;
}

// Halaman yang layak dicatat sebagai "terakhir dibuka".
static bool IsNavigablePage(string path)
{
    if (path.StartsWith("/_")) return false;                                   // _framework, _blazor
    if (path.StartsWith("/Account", StringComparison.OrdinalIgnoreCase)) return false;
    if (path.Equals("/login", StringComparison.OrdinalIgnoreCase)) return false;
    if (path.Equals("/denied", StringComparison.OrdinalIgnoreCase)) return false;
    if (Path.HasExtension(path)) return false;                                 // file statis (.css/.js/.png)
    return true;
}

// Tujuan default bila user login membuka /login: halaman Home (semua role).
static string DefaultFor(System.Security.Claims.ClaimsPrincipal user) => "/";
