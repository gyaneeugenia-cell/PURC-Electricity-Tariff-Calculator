using Microsoft.AspNetCore.Authentication.Cookies;
using PurcHistoricTariffReckoner.CSharp.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

builder.Services.AddControllersWithViews();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/account/login";
        options.AccessDeniedPath = "/account/login";
        options.Cookie.Name = "PurcTariffAuth";
        options.SlidingExpiration = true;
    });
builder.Services.AddAuthorization();

builder.Services.AddScoped<ITariffRepository, PostgresTariffRepository>();
builder.Services.AddScoped<ITariffCalculationService, TariffCalculationService>();
builder.Services.AddScoped<IAuthRepository, PostgresAuthRepository>();
builder.Services.AddSingleton<IPasswordSecurityService, PasswordSecurityService>();
builder.Services.AddHttpClient<IAiAssistantService, GeminiAssistantService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var authRepository = scope.ServiceProvider.GetRequiredService<IAuthRepository>();
    await authRepository.EnsureSchemaAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapControllers();

app.Run();
