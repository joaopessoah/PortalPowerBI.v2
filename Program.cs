using PortalPowerBI.Services;

var builder = WebApplication.CreateBuilder(args);

// --- 1. CONFIGURAÇÃO DOS SERVIÇOS ---
builder.Services.AddControllersWithViews();
builder.Services.AddScoped<PbiService>(); 

// Configuração da Sessão (Login)
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(60);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddHttpContextAccessor();

var app = builder.Build();

// --- 2. CONFIGURAÇÃO DO PIPELINE ---

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // app.UseHsts(); // Comentado para evitar erros de certificado local
}

// app.UseHttpsRedirection(); // <--- COMENTEI ESTA LINHA (É ELA QUE DÁ O AVISO DE PORTA)
app.UseStaticFiles();

app.UseRouting();

app.UseSession(); // Sessão deve vir antes da Autorização
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Login}/{id?}");

app.Run();