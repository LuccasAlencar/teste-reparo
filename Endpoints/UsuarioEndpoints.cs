using Microsoft.AspNetCore.OpenApi;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using MottuVision.Data;
using MottuVision.Dtos;
using MottuVision.Helpers;
using MottuVision.Models;

namespace MottuVision.Endpoints;

public static class UsuarioEndpoints
{
    public static void Configure(WebApplication app)
    {
        var usuarios = app.MapGroup("/api/usuarios").WithTags("Usuários");

        usuarios.MapGet("/", GetUsuarios).WithOpenApi(op =>
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

        usuarios.MapGet("/{id:decimal}", GetUsuarioById).WithOpenApi(op =>
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

        usuarios.MapPost("/", CreateUsuario).WithOpenApi(op =>
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

        usuarios.MapPut("/{id:decimal}", UpdateUsuario).WithOpenApi(op =>
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

        usuarios.MapDelete("/{id:decimal}", DeleteUsuario).WithOpenApi(op =>
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
