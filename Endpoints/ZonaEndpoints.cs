using Microsoft.AspNetCore.OpenApi;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using MottuVision.Data;
using MottuVision.Dtos;
using MottuVision.Helpers;
using MottuVision.Models;

namespace MottuVision.Endpoints;

public static class ZonaEndpoints
{
    public static void Configure(WebApplication app)
    {
        var zonas = app.MapGroup("/api/zonas").WithTags("Zonas");

        zonas.MapGet("/", GetZonas).WithOpenApi(op =>
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

        zonas.MapGet("/{id:decimal}", GetZonaById).WithOpenApi(op =>
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

        zonas.MapPost("/", CreateZona).WithOpenApi(op =>
        {
            op.Summary = "Cria zona";
            return op;
        });

        zonas.MapPut("/{id:decimal}", UpdateZona).WithOpenApi(op =>
        {
            op.Summary = "Atualiza zona";
            return op;
        });

        zonas.MapDelete("/{id:decimal}", DeleteZona).WithOpenApi(op =>
        {
            op.Summary = "Remove zona";
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
