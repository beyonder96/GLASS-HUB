using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using GlassHub;
using GlassHub.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// Configuração dos Componentes Raiz
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Configuração de Serviços (Injeção de Dependência)
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Serviço de Estado de Despesas (Singleton Pattern na prática para WASM)
builder.Services.AddScoped<IExpenseService, ExpenseService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<SettingsService>();

// Configuração do Supabase (Banco de Dados e Auth)
// Nota: Substitua pelas chaves reais em produção
builder.Services.AddScoped<Supabase.Client>(provider => 
    new Supabase.Client(
        builder.Configuration["Supabase:Url"] ?? "https://your-supabase-url.supabase.co", 
        builder.Configuration["Supabase:Key"] ?? "your-anon-key",
        new Supabase.SupabaseOptions
        {
            AutoRefreshToken = true,
            AutoConnectRealtime = true,
        }));

// Inicialização da Aplicação
await builder.Build().RunAsync();
