using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.OpenApi;
using MottuVision.Data;
using MottuVision.Endpoints; 
using System.Text.Json.Serialization;
using DotNetEnv;
using MottuVision.Api;

// Carrega as variáveis de ambiente do arquivo .env
Env.Load();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    });

// DB Oracle - Usando variáveis de ambiente
builder.Services.AddDbContext<AppDbContext>(opt =>
{
    var oracleUser = Environment.GetEnvironmentVariable("ORACLE_USER") ?? "SeuUsuario";
    var oraclePassword = Environment.GetEnvironmentVariable("ORACLE_PASSWORD") ?? "SuaSenha";
    var oracleHost = Environment.GetEnvironmentVariable("ORACLE_HOST") ?? "oracle.fiap.com.br";
    var oraclePort = Environment.GetEnvironmentVariable("ORACLE_PORT") ?? "1521";
    var oracleService = Environment.GetEnvironmentVariable("ORACLE_SERVICE") ?? "orcl";

    var connectionString = 
        $"User Id={oracleUser};Password={oraclePassword};Data Source=" +
        $"(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST={oracleHost})(PORT={oraclePort}))" +
        $"(CONNECT_DATA=(SERVICE_NAME={oracleService})));";

    opt.UseOracle(connectionString);
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(opt =>
{
    opt.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Mottu Vision API",
        Version = "v1",
        Description = "API RESTful (.NET 8 Minimal API) com boas práticas, paginação, HATEOAS e exemplos."
    });
});

var app = builder.Build();

// Inicialização do banco de dados
using (var scope = app.Services.CreateScope())
{
    await DatabaseInitializer.EnsureCreatedAndSeedAsync(scope.ServiceProvider);
}

app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/", () => Results.Redirect("/swagger"));

// Configuração dos endpoints
UsuarioEndpoints.Configure(app);
ZonaEndpoints.Configure(app);
MotoEndpoints.Configure(app);
PatioEndpoints.Configure(app);
StatusGrupoEndpoints.Configure(app);
StatusEndpoints.Configure(app);

await app.RunAsync();
