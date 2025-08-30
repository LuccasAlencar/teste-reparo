using Microsoft.AspNetCore.OpenApi;
using Microsoft.EntityFrameworkCore;
using MottuVision.Data;
using MottuVision.Dtos;
using MottuVision.Helpers;
using MottuVision.Models;

namespace MottuVision.Endpoints;

public static class StatusGrupoEndpoints
{
    public static void Configure(WebApplication app)
    {
        var statusGrupos = app.MapGroup("/api/statusgrupos").WithTags("StatusGrupo");

        statusGrupos.MapGet("/", GetStatusGrupos).WithOpenApi(op => { op.Summary = "Lista status grupos (paginado)"; return op; });
        statusGrupos.MapGet("/{id:decimal}", GetStatusGrupoById).WithOpenApi(op => { op.Summary = "Obtém status grupo por ID"; return op; });
        statusGrupos.MapPost("/", CreateStatusGrupo).WithOpenApi(op => { op.Summary = "Cria status grupo"; return op; });
        statusGrupos.MapPut("/{id:decimal}", UpdateStatusGrupo).WithOpenApi(op => { op.Summary = "Atualiza status grupo"; return op; });
        statusGrupos.MapDelete("/{id:decimal}", DeleteStatusGrupo).WithOpenApi(op => { op.Summary = "Remove status grupo"; return op; });
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
