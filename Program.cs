using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi.Any;
using MottuVision.Data;
using MottuVision.Models;
using MottuVision.Dtos;
using MottuVision.Api;
using System.Data.Common;
using System.Data;
using System.Text.Json.Serialization;
using DotNetEnv;

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
    
    var connectionString = $"User Id={oracleUser};Password={oraclePassword};Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST={oracleHost})(PORT={oraclePort}))(CONNECT_DATA=(SERVICE_NAME={oracleService})));";
    
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

// ---------- CLASSES DE ENDPOINTS ----------

public static class UsuarioEndpoints
{
    public static void Configure(WebApplication app)
    {
        var usuarios = app.MapGroup("/api/usuarios").WithTags("Usuários");

        usuarios.MapGet("/", GetUsuarios)
            .WithOpenApi(op =>
            {
                op.Summary = "Lista usuários (paginado)";
                op.Responses["200"] = new OpenApiResponse
                {
                    Description = "Lista paginada de usuários",
                    Content = new Dictionary<string, OpenApiMediaType>
                    {
                        ["application/json"] = new OpenApiMediaType
                        {
                            Example = new OpenApiString("""
                            {
                              "items":[{"id":1,"usuario":"admin"}],
                              "page":1,"pageSize":20,"totalCount":1,
                              "links":[{"rel":"self","href":"https://localhost/api/usuarios?page=1&pageSize=20","method":"GET"}]
                            }
                            """)
                        }
                    }
                };
                return op;
            });

        usuarios.MapGet("/{id:decimal}", GetUsuarioById)
            .WithOpenApi(op =>
            {
                op.Summary = "Obtém usuário por ID";
                op.Responses["200"] = new OpenApiResponse
                {
                    Description = "Usuário encontrado",
                    Content = new Dictionary<string, OpenApiMediaType>
                    {
                        ["application/json"] = new OpenApiMediaType
                        {
                            Example = new OpenApiString("""{ "id": 1, "usuario": "admin" }""")
                        }
                    }
                };
                op.Responses["404"] = new OpenApiResponse { Description = "Usuário não encontrado" };
                return op;
            });

        usuarios.MapPost("/", CreateUsuario)
            .WithOpenApi(op =>
            {
                op.Summary = "Cria usuário";
                op.RequestBody = new OpenApiRequestBody
                {
                    Content = new Dictionary<string, OpenApiMediaType>
                    {
                        ["application/json"] = new OpenApiMediaType
                        {
                            Example = new OpenApiString("""{ "usuario":"novo_user", "senha":"Forte@123" }""")
                        }
                    }
                };
                op.Responses["201"] = new OpenApiResponse
                {
                    Description = "Usuário criado",
                    Content = new Dictionary<string, OpenApiMediaType>
                    {
                        ["application/json"] = new OpenApiMediaType
                        {
                            Example = new OpenApiString("""{ "id": 2, "nomeUsuario":"novo_user", "senhaHash":"<hash>" }""")
                        }
                    }
                };
                op.Responses["400"] = new OpenApiResponse { Description = "Erro de validação" };
                return op;
            });

        usuarios.MapPut("/{id:decimal}", UpdateUsuario)
            .WithOpenApi(op =>
            {
                op.Summary = "Atualiza usuário";
                op.RequestBody = new OpenApiRequestBody
                {
                    Content = new Dictionary<string, OpenApiMediaType>
                    {
                        ["application/json"] = new OpenApiMediaType
                        {
                            Example = new OpenApiString("""{ "usuario":"admin2", "senha":"Nova@123" }""")
                        }
                    }
                };
                op.Responses["200"] = new OpenApiResponse
                {
                    Description = "Usuário atualizado",
                    Content = new Dictionary<string, OpenApiMediaType>
                    {
                        ["application/json"] = new OpenApiMediaType
                        {
                            Example = new OpenApiString("""{ "id": 1, "usuario": "admin2" }""")
                        }
                    }
                };
                op.Responses["400"] = new OpenApiResponse { Description = "Erro de validação" };
                op.Responses["404"] = new OpenApiResponse { Description = "Usuário não encontrado" };
                return op;
            });

        usuarios.MapDelete("/{id:decimal}", DeleteUsuario)
            .WithOpenApi(op =>
            {
                op.Summary = "Remove usuário";
                op.Responses["204"] = new OpenApiResponse { Description = "Usuário removido" };
                op.Responses["404"] = new OpenApiResponse { Description = "Usuário não encontrado" };
                return op;
            });
    }

    private static async Task<IResult> GetUsuarios(HttpContext ctx, AppDbContext db, int page = 1, int pageSize = 20)
    {
        var (validPage, validPageSize) = ApiHelper.ValidatePagination(page, pageSize);
        var total = await db.Usuarios.LongCountAsync();
        var items = await db.Usuarios
            .OrderBy(u => u.Id)
            .Skip((validPage - 1) * validPageSize)
            .Take(validPageSize)
            .Select(u => new { u.Id, Usuario = u.NomeUsuario })
            .ToListAsync();

        return TypedResults.Ok(ApiHelper.ToPaged(ctx, items, validPage, validPageSize, total, "/api/usuarios"));
    }

    private static async Task<IResult> GetUsuarioById(AppDbContext db, decimal id)
    {
        var u = await db.Usuarios.FindAsync(id);
        if (u is null) return TypedResults.NotFound();
        return TypedResults.Ok(new { u.Id, Usuario = u.NomeUsuario });
    }

    private static async Task<IResult> CreateUsuario(AppDbContext db, UsuarioCreateDto dto)
    {
        if (await db.Usuarios.AnyAsync(x => x.NomeUsuario == dto.Usuario))
            return TypedResults.BadRequest("Usuário já existe.");

        var entity = new Usuario
        {
            Id = await ApiHelper.NextIdAsync(db, "usuario"),
            NomeUsuario = dto.Usuario,
            SenhaHash = dto.Senha
        };
        
        db.Usuarios.Add(entity);
        await db.SaveChangesAsync();
        return TypedResults.Created($"/api/usuarios/{entity.Id}", entity);
    }

    private static async Task<IResult> UpdateUsuario(AppDbContext db, decimal id, UsuarioUpdateDto dto)
    {
        var u = await db.Usuarios.FindAsync(id);
        if (u is null) return TypedResults.NotFound();

        if (await db.Usuarios.AnyAsync(x => x.NomeUsuario == dto.Usuario && x.Id != id))
            return TypedResults.BadRequest("Já existe outro usuário com esse nome.");

        u.NomeUsuario = dto.Usuario;
        u.SenhaHash = dto.Senha;
        await db.SaveChangesAsync();
        return TypedResults.Ok(new { u.Id, Usuario = u.NomeUsuario });
    }

    private static async Task<IResult> DeleteUsuario(AppDbContext db, decimal id)
    {
        var u = await db.Usuarios.FindAsync(id);
        if (u is null) return TypedResults.NotFound();
        
        db.Usuarios.Remove(u);
        await db.SaveChangesAsync();
        return TypedResults.NoContent();
    }
}

public static class ZonaEndpoints
{
    public static void Configure(WebApplication app)
    {
        var zonas = app.MapGroup("/api/zonas").WithTags("Zonas");

        zonas.MapGet("/", GetZonas)
            .WithOpenApi(op =>
            {
                op.Summary = "Lista zonas (paginado)";
                op.Responses["200"] = new OpenApiResponse
                {
                    Description = "Lista paginada de zonas",
                    Content = new Dictionary<string, OpenApiMediaType>
                    {
                        ["application/json"] = new OpenApiMediaType
                        {
                            Example = new OpenApiString("""
                            { "items":[{"id":1,"nome":"Zona Leste","letra":"L"}], "page":1, "pageSize":20, "totalCount":1 }
                            """)
                        }
                    }
                };
                return op;
            });

        zonas.MapGet("/{id:decimal}", GetZonaById)
            .WithOpenApi(op =>
            {
                op.Summary = "Obtém zona por ID";
                op.Responses["200"] = new OpenApiResponse
                {
                    Description = "Zona encontrada",
                    Content = new Dictionary<string, OpenApiMediaType>
                    {
                        ["application/json"] = new OpenApiMediaType
                        {
                            Example = new OpenApiString("""{ "id":1, "nome":"Zona Leste", "letra":"L" }""")
                        }
                    }
                };
                op.Responses["404"] = new OpenApiResponse { Description = "Zona não encontrada" };
                return op;
            });

        zonas.MapPost("/", CreateZona)
            .WithOpenApi(op =>
            {
                op.Summary = "Cria zona";
                op.RequestBody = new OpenApiRequestBody
                {
                    Content = new Dictionary<string, OpenApiMediaType>
                    {
                        ["application/json"] = new OpenApiMediaType
                        {
                            Example = new OpenApiString("""{ "nome":"Zona Norte", "letra":"N" }""")
                        }
                    }
                };
                op.Responses["201"] = new OpenApiResponse
                {
                    Description = "Zona criada",
                    Content = new Dictionary<string, OpenApiMediaType>
                    {
                        ["application/json"] = new OpenApiMediaType
                        {
                            Example = new OpenApiString("""{ "id":2, "nome":"Zona Norte", "letra":"N" }""")
                        }
                    }
                };
                op.Responses["400"] = new OpenApiResponse { Description = "Erro de validação" };
                return op;
            });

        zonas.MapPut("/{id:decimal}", UpdateZona)
            .WithOpenApi(op =>
            {
                op.Summary = "Atualiza zona";
                op.RequestBody = new OpenApiRequestBody
                {
                    Content = new Dictionary<string, OpenApiMediaType>
                    {
                        ["application/json"] = new OpenApiMediaType
                        {
                            Example = new OpenApiString("""{ "nome":"Zona Leste Atualizada", "letra":"L" }""")
                        }
                    }
                };
                op.Responses["200"] = new OpenApiResponse
                {
                    Description = "Zona atualizada",
                    Content = new Dictionary<string, OpenApiMediaType>
                    {
                        ["application/json"] = new OpenApiMediaType
                        {
                            Example = new OpenApiString("""{ "id":1, "nome":"Zona Leste Atualizada", "letra":"L" }""")
                        }
                    }
                };
                op.Responses["400"] = new OpenApiResponse { Description = "Erro de validação" };
                op.Responses["404"] = new OpenApiResponse { Description = "Zona não encontrada" };
                return op;
            });

        zonas.MapDelete("/{id:decimal}", DeleteZona)
            .WithOpenApi(op =>
            {
                op.Summary = "Remove zona";
                op.Responses["204"] = new OpenApiResponse { Description = "Zona removida" };
                op.Responses["404"] = new OpenApiResponse { Description = "Zona não encontrada" };
                return op;
            });
    }

    private static async Task<IResult> GetZonas(HttpContext ctx, AppDbContext db, int page = 1, int pageSize = 20)
    {
        var total = await db.Zonas.LongCountAsync();
        var items = await db.Zonas
            .OrderBy(z => z.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return TypedResults.Ok(ApiHelper.ToPaged(ctx, items, page, pageSize, total, "/api/zonas"));
    }

    private static async Task<IResult> GetZonaById(AppDbContext db, decimal id)
    {
        var z = await db.Zonas.FindAsync(id);
        if (z is null) return TypedResults.NotFound();
        return TypedResults.Ok(z);
    }

    private static async Task<IResult> CreateZona(AppDbContext db, ZonaCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Nome) || dto.Letra?.Length != 1)
            return TypedResults.BadRequest("Nome obrigatório e Letra deve ter 1 caractere.");

        var entity = new Zona 
        { 
            Id = await ApiHelper.NextIdAsync(db, "zona"), 
            Nome = dto.Nome, 
            Letra = dto.Letra! 
        };
        
        db.Zonas.Add(entity);
        await db.SaveChangesAsync();
        return TypedResults.Created($"/api/zonas/{entity.Id}", entity);
    }

    private static async Task<IResult> UpdateZona(AppDbContext db, decimal id, ZonaUpdateDto dto)
    {
        var z = await db.Zonas.FindAsync(id);
        if (z is null) return TypedResults.NotFound();
        
        if (string.IsNullOrWhiteSpace(dto.Nome) || dto.Letra?.Length != 1)
            return TypedResults.BadRequest("Nome obrigatório e Letra deve ter 1 caractere.");

        z.Nome = dto.Nome;
        z.Letra = dto.Letra!;
        await db.SaveChangesAsync();
        return TypedResults.Ok(z);
    }

    private static async Task<IResult> DeleteZona(AppDbContext db, decimal id)
    {
        var z = await db.Zonas.FindAsync(id);
        if (z is null) return TypedResults.NotFound();
        
        db.Zonas.Remove(z);
        await db.SaveChangesAsync();
        return TypedResults.NoContent();
    }
}

public static class MotoEndpoints
{
    public static void Configure(WebApplication app)
    {
        var motos = app.MapGroup("/api/motos").WithTags("Motos");

        motos.MapGet("/", GetMotos)
            .WithOpenApi(op =>
            {
                op.Summary = "Lista motos (paginado, filtro por placa)";
                op.Responses["200"] = new OpenApiResponse
                {
                    Description = "Lista paginada de motos",
                    Content = new Dictionary<string, OpenApiMediaType>
                    {
                        ["application/json"] = new OpenApiMediaType
                        {
                            Example = new OpenApiString("""
                            { "items":[{"id":1,"placa":"ABC1D23","chassi":"9BW...","qrCode":"QR001","zonaId":1,"patioId":1,"statusId":1}], "page":1,"pageSize":20,"totalCount":1 }
                            """)
                        }
                    }
                };
                return op;
            });

        motos.MapGet("/{id:decimal}", GetMotoById)
            .WithOpenApi(op =>
            {
                op.Summary = "Obtém moto por ID";
                op.Responses["200"] = new OpenApiResponse
                {
                    Description = "Moto encontrada",
                    Content = new Dictionary<string, OpenApiMediaType>
                    {
                        ["application/json"] = new OpenApiMediaType
                        {
                            Example = new OpenApiString("""{ "id":1, "placa":"ABC1D23", "chassi":"9BW...", "qrCode":"QR001", "zonaId":1, "patioId":1, "statusId":1 }""")
                        }
                    }
                };
                op.Responses["404"] = new OpenApiResponse { Description = "Moto não encontrada" };
                return op;
            });

        motos.MapPost("/", CreateMoto)
            .WithOpenApi(op =>
            {
                op.Summary = "Cria moto";
                op.RequestBody = new OpenApiRequestBody
                {
                    Content = new Dictionary<string, OpenApiMediaType>
                    {
                        ["application/json"] = new OpenApiMediaType
                        {
                            Example = new OpenApiString("""{ "placa":"JKL7M89","chassi":"9BWZZZ377VT004253","qrCode":"QR003","dataEntrada":"2023-01-01T10:00:00","zonaId":1,"patioId":1,"statusId":1 }""")
                        }
                    }
                };
                op.Responses["201"] = new OpenApiResponse
                {
                    Description = "Moto criada",
                    Content = new Dictionary<string, OpenApiMediaType>
                    {
                        ["application/json"] = new OpenApiMediaType
                        {
                            Example = new OpenApiString("""{ "id":3, "placa":"JKL7M89", "chassi":"9BWZZZ377VT004253", "qrCode":"QR003", "zonaId":1, "patioId":1, "statusId":1 }""")
                        }
                    }
                };
                op.Responses["400"] = new OpenApiResponse { Description = "Erro de validação" };
                return op;
            });

        motos.MapPut("/{id:decimal}", UpdateMoto)
            .WithOpenApi(op =>
            {
                op.Summary = "Atualiza moto";
                op.RequestBody = new OpenApiRequestBody
                {
                    Content = new Dictionary<string, OpenApiMediaType>
                    {
                        ["application/json"] = new OpenApiMediaType
                        {
                            Example = new OpenApiString("""{ "placa":"ABC1D23","chassi":"9BW...","qrCode":"QR010","dataEntrada":"2023-01-01T10:00:00","zonaId":2,"patioId":1,"statusId":2 }""")
                        }
                    }
                };
                op.Responses["200"] = new OpenApiResponse
                {
                    Description = "Moto atualizada",
                    Content = new Dictionary<string, OpenApiMediaType>
                    {
                        ["application/json"] = new OpenApiMediaType
                        {
                            Example = new OpenApiString("""{ "id":1, "placa":"ABC1D23", "chassi":"9BW...", "qrCode":"QR010", "zonaId":2, "patioId":1, "statusId":2 }""")
                        }
                    }
                };
                op.Responses["400"] = new OpenApiResponse { Description = "Erro de validação" };
                op.Responses["404"] = new OpenApiResponse { Description = "Moto não encontrada" };
                return op;
            });

        motos.MapDelete("/{id:decimal}", DeleteMoto)
            .WithOpenApi(op =>
            {
                op.Summary = "Remove moto";
                op.Responses["204"] = new OpenApiResponse { Description = "Moto removida" };
                op.Responses["404"] = new OpenApiResponse { Description = "Moto não encontrada" };
                return op;
            });
    }

    private static async Task<IResult> GetMotos(HttpContext ctx, AppDbContext db, int page = 1, int pageSize = 20, string? placa = null)
    {
        var query = db.Motos.Include(m => m.Zona).Include(m => m.Patio).Include(m => m.Status).AsQueryable();
        
        if (!string.IsNullOrWhiteSpace(placa))
            query = query.Where(m => m.Placa.Contains(placa, StringComparison.OrdinalIgnoreCase));

        var total = await query.LongCountAsync();
        var items = await query
            .OrderBy(m => m.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return TypedResults.Ok(ApiHelper.ToPaged(ctx, items, page, pageSize, total, "/api/motos"));
    }

    private static async Task<IResult> GetMotoById(AppDbContext db, decimal id)
    {
        var m = await db.Motos
            .Include(x => x.Zona)
            .Include(x => x.Patio)
            .Include(x => x.Status)
            .FirstOrDefaultAsync(x => x.Id == id);
            
        if (m is null) return TypedResults.NotFound();
        return TypedResults.Ok(m);
    }

    private static async Task<IResult> CreateMoto(AppDbContext db, MotoCreateDto dto)
    {
        var validationResult = await ValidateMotoCreateDto(db, dto);
        if (validationResult != null) return validationResult;

        var entity = new Moto
        {
            Id = await ApiHelper.NextIdAsync(db, "moto"),
            Placa = dto.Placa,
            Chassi = dto.Chassi,
            QrCode = dto.QrCode,
            DataEntrada = dto.DataEntrada,
            PrevisaoEntrega = dto.PrevisaoEntrega,
            Fotos = dto.Fotos,
            ZonaId = dto.ZonaId,
            PatioId = dto.PatioId,
            StatusId = dto.StatusId,
            Observacoes = dto.Observacoes
        };
        
        db.Motos.Add(entity);
        await db.SaveChangesAsync();
        return TypedResults.Created($"/api/motos/{entity.Id}", entity);
    }

    private static async Task<IResult> UpdateMoto(AppDbContext db, decimal id, MotoUpdateDto dto)
    {
        var m = await db.Motos.FindAsync(id);
        if (m is null) return TypedResults.NotFound();

        var validationResult = await ValidateMotoUpdateDto(db, dto, id);
        if (validationResult != null) return validationResult;

        m.Placa = dto.Placa;
        m.Chassi = dto.Chassi;
        m.QrCode = dto.QrCode;
        m.DataEntrada = dto.DataEntrada;
        m.PrevisaoEntrega = dto.PrevisaoEntrega;
        m.Fotos = dto.Fotos;
        m.ZonaId = dto.ZonaId;
        m.PatioId = dto.PatioId;
        m.StatusId = dto.StatusId;
        m.Observacoes = dto.Observacoes;
        
        await db.SaveChangesAsync();
        return TypedResults.Ok(m);
    }

    private static async Task<IResult> DeleteMoto(AppDbContext db, decimal id)
    {
        var m = await db.Motos.FindAsync(id);
        if (m is null) return TypedResults.NotFound();
        
        db.Motos.Remove(m);
        await db.SaveChangesAsync();
        return TypedResults.NoContent();
    }

    private static async Task<IResult?> ValidateMotoCreateDto(AppDbContext db, MotoCreateDto dto)
    {
        if (!await db.Zonas.AnyAsync(z => z.Id == dto.ZonaId))
            return TypedResults.BadRequest("ZonaId inválido.");
            
        if (!await db.Patios.AnyAsync(p => p.Id == dto.PatioId))
            return TypedResults.BadRequest("PatioId inválido.");
            
        if (!await db.Statuses.AnyAsync(s => s.Id == dto.StatusId))
            return TypedResults.BadRequest("StatusId inválido.");

        if (await db.Motos.AnyAsync(m => m.Placa == dto.Placa))
            return TypedResults.BadRequest("Placa já cadastrada.");

        if (await db.Motos.AnyAsync(m => m.Chassi == dto.Chassi))
            return TypedResults.BadRequest("Chassi já cadastrado.");

        return null;
    }

    private static async Task<IResult?> ValidateMotoUpdateDto(AppDbContext db, MotoUpdateDto dto, decimal excludeId)
    {
        if (!await db.Zonas.AnyAsync(z => z.Id == dto.ZonaId))
            return TypedResults.BadRequest("ZonaId inválido.");
            
        if (!await db.Patios.AnyAsync(p => p.Id == dto.PatioId))
            return TypedResults.BadRequest("PatioId inválido.");
            
        if (!await db.Statuses.AnyAsync(s => s.Id == dto.StatusId))
            return TypedResults.BadRequest("StatusId inválido.");

        if (await db.Motos.AnyAsync(m => m.Placa == dto.Placa && m.Id != excludeId))
            return TypedResults.BadRequest("Placa já cadastrada.");

        if (await db.Motos.AnyAsync(m => m.Chassi == dto.Chassi && m.Id != excludeId))
            return TypedResults.BadRequest("Chassi já cadastrado.");

        return null;
    }
}

public static class PatioEndpoints
{
    public static void Configure(WebApplication app)
    {
        var patios = app.MapGroup("/api/patios").WithTags("Patios");

        patios.MapGet("/", GetPatios)
            .WithOpenApi(op =>
            {
                op.Summary = "Lista pátios (paginado)";
                return op;
            });

        patios.MapGet("/{id:decimal}", GetPatioById)
            .WithOpenApi(op =>
            {
                op.Summary = "Obtém pátio por ID";
                return op;
            });

        patios.MapPost("/", CreatePatio)
            .WithOpenApi(op =>
            {
                op.Summary = "Cria pátio";
                return op;
            });

        patios.MapPut("/{id:decimal}", UpdatePatio)
            .WithOpenApi(op =>
            {
                op.Summary = "Atualiza pátio";
                return op;
            });

        patios.MapDelete("/{id:decimal}", DeletePatio)
            .WithOpenApi(op =>
            {
                op.Summary = "Remove pátio";
                return op;
            });
    }

    private static async Task<IResult> GetPatios(HttpContext ctx, AppDbContext db, int page = 1, int pageSize = 20)
    {
        var (validPage, validPageSize) = ApiHelper.ValidatePagination(page, pageSize);
        var total = await db.Patios.LongCountAsync();
        var items = await db.Patios
            .OrderBy(p => p.Id)
            .Skip((validPage - 1) * validPageSize)
            .Take(validPageSize)
            .ToListAsync();

        return TypedResults.Ok(ApiHelper.ToPaged(ctx, items, validPage, validPageSize, total, "/api/patios"));
    }

    private static async Task<IResult> GetPatioById(AppDbContext db, decimal id)
    {
        var p = await db.Patios.FindAsync(id);
        if (p is null) return TypedResults.NotFound();
        return TypedResults.Ok(p);
    }

    private static async Task<IResult> CreatePatio(AppDbContext db, PatioCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Nome))
            return TypedResults.BadRequest("Nome obrigatório.");

        var entity = new Patio 
        { 
            Id = await ApiHelper.NextIdAsync(db, "patio"), 
            Nome = dto.Nome 
        };
        
        db.Patios.Add(entity);
        await db.SaveChangesAsync();
        return TypedResults.Created($"/api/patios/{entity.Id}", entity);
    }

    private static async Task<IResult> UpdatePatio(AppDbContext db, decimal id, PatioUpdateDto dto)
    {
        var p = await db.Patios.FindAsync(id);
        if (p is null) return TypedResults.NotFound();
        
        if (string.IsNullOrWhiteSpace(dto.Nome))
            return TypedResults.BadRequest("Nome obrigatório.");

        p.Nome = dto.Nome;
        await db.SaveChangesAsync();
        return TypedResults.Ok(p);
    }

    private static async Task<IResult> DeletePatio(AppDbContext db, decimal id)
    {
        var p = await db.Patios.Include(x => x.Motos).FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return TypedResults.NotFound();
        
        if (p.Motos.Count > 0)
            return TypedResults.BadRequest("Não é possível remover pátio com motos associadas.");

        db.Patios.Remove(p);
        await db.SaveChangesAsync();
        return TypedResults.NoContent();
    }
}

public static class StatusGrupoEndpoints
{
    public static void Configure(WebApplication app)
    {
        var statusGrupos = app.MapGroup("/api/statusgrupos").WithTags("StatusGrupo");

        statusGrupos.MapGet("/", GetStatusGrupos)
            .WithOpenApi(op =>
            {
                op.Summary = "Lista status grupos (paginado)";
                return op;
            });

        statusGrupos.MapGet("/{id:decimal}", GetStatusGrupoById)
            .WithOpenApi(op =>
            {
                op.Summary = "Obtém status grupo por ID";
                return op;
            });

        statusGrupos.MapPost("/", CreateStatusGrupo)
            .WithOpenApi(op =>
            {
                op.Summary = "Cria status grupo";
                return op;
            });

        statusGrupos.MapPut("/{id:decimal}", UpdateStatusGrupo)
            .WithOpenApi(op =>
            {
                op.Summary = "Atualiza status grupo";
                return op;
            });

        statusGrupos.MapDelete("/{id:decimal}", DeleteStatusGrupo)
            .WithOpenApi(op =>
            {
                op.Summary = "Remove status grupo";
                return op;
            });
    }

    private static async Task<IResult> GetStatusGrupos(HttpContext ctx, AppDbContext db, int page = 1, int pageSize = 20)
    {
        var (validPage, validPageSize) = ApiHelper.ValidatePagination(page, pageSize);
        var total = await db.StatusGrupos.LongCountAsync();
        var items = await db.StatusGrupos
            .OrderBy(sg => sg.Id)
            .Skip((validPage - 1) * validPageSize)
            .Take(validPageSize)
            .ToListAsync();

        return TypedResults.Ok(ApiHelper.ToPaged(ctx, items, validPage, validPageSize, total, "/api/statusgrupos"));
    }

    private static async Task<IResult> GetStatusGrupoById(AppDbContext db, decimal id)
    {
        var sg = await db.StatusGrupos.Include(x => x.Statuses).FirstOrDefaultAsync(x => x.Id == id);
        if (sg is null) return TypedResults.NotFound();
        return TypedResults.Ok(sg);
    }

    private static async Task<IResult> CreateStatusGrupo(AppDbContext db, StatusGrupoCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Nome))
            return TypedResults.BadRequest("Nome obrigatório.");

        var entity = new StatusGrupo 
        { 
            Id = await ApiHelper.NextIdAsync(db, "status_grupo"), 
            Nome = dto.Nome 
        };
        
        db.StatusGrupos.Add(entity);
        await db.SaveChangesAsync();
        return TypedResults.Created($"/api/statusgrupos/{entity.Id}", entity);
    }

    private static async Task<IResult> UpdateStatusGrupo(AppDbContext db, decimal id, StatusGrupoUpdateDto dto)
    {
        var sg = await db.StatusGrupos.FindAsync(id);
        if (sg is null) return TypedResults.NotFound();
        
        if (string.IsNullOrWhiteSpace(dto.Nome))
            return TypedResults.BadRequest("Nome obrigatório.");

        sg.Nome = dto.Nome;
        await db.SaveChangesAsync();
        return TypedResults.Ok(sg);
    }

    private static async Task<IResult> DeleteStatusGrupo(AppDbContext db, decimal id)
    {
        var sg = await db.StatusGrupos.Include(x => x.Statuses).FirstOrDefaultAsync(x => x.Id == id);
        if (sg is null) return TypedResults.NotFound();
        
        if (sg.Statuses.Count > 0)
            return TypedResults.BadRequest("Não é possível remover StatusGrupo que contém Statuses.");

        db.StatusGrupos.Remove(sg);
        await db.SaveChangesAsync();
        return TypedResults.NoContent();
    }
}

public static class StatusEndpoints
{
    public static void Configure(WebApplication app)
    {
        var statuses = app.MapGroup("/api/statuses").WithTags("Status");

        statuses.MapGet("/", GetStatuses)
            .WithOpenApi(op =>
            {
                op.Summary = "Lista status (paginado)";
                return op;
            });

        statuses.MapGet("/{id:decimal}", GetStatusById)
            .WithOpenApi(op =>
            {
                op.Summary = "Obtém status por ID";
                return op;
            });

        statuses.MapPost("/", CreateStatus)
            .WithOpenApi(op =>
            {
                op.Summary = "Cria status";
                return op;
            });

        statuses.MapPut("/{id:decimal}", UpdateStatus)
            .WithOpenApi(op =>
            {
                op.Summary = "Atualiza status";
                return op;
            });

        statuses.MapDelete("/{id:decimal}", DeleteStatus)
            .WithOpenApi(op =>
            {
                op.Summary = "Remove status";
                return op;
            });
    }

    private static async Task<IResult> GetStatuses(HttpContext ctx, AppDbContext db, int page = 1, int pageSize = 20)
    {
        var (validPage, validPageSize) = ApiHelper.ValidatePagination(page, pageSize);
        var query = db.Statuses.Include(s => s.StatusGrupo).AsQueryable();
        var total = await query.LongCountAsync();
        var items = await query
            .OrderBy(s => s.Id)
            .Skip((validPage - 1) * validPageSize)
            .Take(validPageSize)
            .ToListAsync();

        return TypedResults.Ok(ApiHelper.ToPaged(ctx, items, validPage, validPageSize, total, "/api/statuses"));
    }

    private static async Task<IResult> GetStatusById(AppDbContext db, decimal id)
    {
        var s = await db.Statuses.Include(x => x.StatusGrupo).FirstOrDefaultAsync(x => x.Id == id);
        if (s is null) return TypedResults.NotFound();
        return TypedResults.Ok(s);
    }

    private static async Task<IResult> CreateStatus(AppDbContext db, StatusCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Nome))
            return TypedResults.BadRequest("Nome obrigatório.");
            
        if (!await db.StatusGrupos.AnyAsync(x => x.Id == dto.StatusGrupoId))
            return TypedResults.BadRequest("StatusGrupoId inválido.");

        var entity = new Status
        {
            Id = await ApiHelper.NextIdAsync(db, "status"),
            Nome = dto.Nome,
            StatusGrupoId = dto.StatusGrupoId
        };
        
        db.Statuses.Add(entity);
        await db.SaveChangesAsync();
        return TypedResults.Created($"/api/statuses/{entity.Id}", entity);
    }

    private static async Task<IResult> UpdateStatus(AppDbContext db, decimal id, StatusUpdateDto dto)
    {
        var s = await db.Statuses.FindAsync(id);
        if (s is null) return TypedResults.NotFound();
        
        if (string.IsNullOrWhiteSpace(dto.Nome))
            return TypedResults.BadRequest("Nome obrigatório.");
            
        if (!await db.StatusGrupos.AnyAsync(x => x.Id == dto.StatusGrupoId))
            return TypedResults.BadRequest("StatusGrupoId inválido.");

        s.Nome = dto.Nome;
        s.StatusGrupoId = dto.StatusGrupoId;
        await db.SaveChangesAsync();
        return TypedResults.Ok(s);
    }

    private static async Task<IResult> DeleteStatus(AppDbContext db, decimal id)
    {
        var s = await db.Statuses.Include(x => x.Motos).FirstOrDefaultAsync(x => x.Id == id);
        if (s is null) return TypedResults.NotFound();
        
        if (s.Motos.Count > 0)
            return TypedResults.BadRequest("Não é possível remover Status com motos associadas.");

        db.Statuses.Remove(s);
        await db.SaveChangesAsync();
        return TypedResults.NoContent();
    }
}

// ---------- HELPERS ----------

public static class ApiHelper
{
    public static Link MakeLink(HttpContext ctx, string rel, string path, string method) =>
        new(rel, $"{ctx.Request.Scheme}://{ctx.Request.Host}{path}", method);

    public static PagedResult<T> ToPaged<T>(
        HttpContext ctx, IEnumerable<T> items, int page, int pageSize, long total, string basePath)
    {
        var links = new List<Link>
        {
            MakeLink(ctx, "self", $"{basePath}?page={page}&pageSize={pageSize}", "GET")
        };

        var totalPages = (int)Math.Ceiling(total / (double)pageSize);

        if (page > 1)
            links.Add(MakeLink(ctx, "prev", $"{basePath}?page={page - 1}&pageSize={pageSize}", "GET"));

        if (page < totalPages)
            links.Add(MakeLink(ctx, "next", $"{basePath}?page={page + 1}&pageSize={pageSize}", "GET"));

        return new PagedResult<T>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = total,
            Links = links
        };
    }

    public static async Task<decimal> NextIdAsync(AppDbContext db, string table)
    {
        var connection = db.Database.GetDbConnection();

        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"SELECT NVL(MAX(\"id\"), 0) + 1 FROM \"{table}\"";

        var result = await cmd.ExecuteScalarAsync();
        return result == DBNull.Value ? 1 : Convert.ToDecimal(result);
    }

    public static (int page, int pageSize) ValidatePagination(int page, int pageSize)
    {
        var validPage = page < 1 ? 1 : page;
        var validPageSize = pageSize is < 1 or > 100 ? 20 : pageSize;
        return (validPage, validPageSize);
    }
}