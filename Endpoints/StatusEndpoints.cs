using Microsoft.AspNetCore.OpenApi;
using Microsoft.EntityFrameworkCore;
using MottuVision.Data;
using MottuVision.Dtos;
using MottuVision.Helpers;
using MottuVision.Models;

namespace MottuVision.Endpoints;

public static class StatusEndpoints
{
    public static void Configure(WebApplication app)
    {
        var statuses = app.MapGroup("/api/statuses").WithTags("Status");

        statuses.MapGet("/", GetStatuses).WithOpenApi(op => { op.Summary = "Lista status (paginado)"; return op; });
        statuses.MapGet("/{id:decimal}", GetStatusById).WithOpenApi(op => { op.Summary = "Obtém status por ID"; return op; });
        statuses.MapPost("/", CreateStatus).WithOpenApi(op => { op.Summary = "Cria status"; return op; });
        statuses.MapPut("/{id:decimal}", UpdateStatus).WithOpenApi(op => { op.Summary = "Atualiza status"; return op; });
        statuses.MapDelete("/{id:decimal}", DeleteStatus).WithOpenApi(op => { op.Summary = "Remove status"; return op; });
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
