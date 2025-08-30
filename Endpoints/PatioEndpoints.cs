using Microsoft.AspNetCore.OpenApi;
using Microsoft.EntityFrameworkCore;
using MottuVision.Data;
using MottuVision.Dtos;
using MottuVision.Helpers;
using MottuVision.Models;

namespace MottuVision.Endpoints;

public static class PatioEndpoints
{
    public static void Configure(WebApplication app)
    {
        var patios = app.MapGroup("/api/patios").WithTags("Patios");

        patios.MapGet("/", GetPatios).WithOpenApi(op => { op.Summary = "Lista pátios (paginado)"; return op; });
        patios.MapGet("/{id:decimal}", GetPatioById).WithOpenApi(op => { op.Summary = "Obtém pátio por ID"; return op; });
        patios.MapPost("/", CreatePatio).WithOpenApi(op => { op.Summary = "Cria pátio"; return op; });
        patios.MapPut("/{id:decimal}", UpdatePatio).WithOpenApi(op => { op.Summary = "Atualiza pátio"; return op; });
        patios.MapDelete("/{id:decimal}", DeletePatio).WithOpenApi(op => { op.Summary = "Remove pátio"; return op; });
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
