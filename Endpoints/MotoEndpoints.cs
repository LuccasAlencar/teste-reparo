using Microsoft.AspNetCore.OpenApi;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using MottuVision.Data;
using MottuVision.Dtos;
using MottuVision.Helpers;
using MottuVision.Models;

namespace MottuVision.Endpoints;

public static class MotoEndpoints
{
    public static void Configure(WebApplication app)
    {
        var motos = app.MapGroup("/api/motos").WithTags("Motos");

        motos.MapGet("/", GetMotos).WithOpenApi(op =>
        {
            op.Summary = "Lista motos (paginado, filtro por placa)";
            return op;
        });

        motos.MapGet("/{id:decimal}", GetMotoById).WithOpenApi(op =>
        {
            op.Summary = "Obtém moto por ID";
            return op;
        });

        motos.MapPost("/", CreateMoto).WithOpenApi(op =>
        {
            op.Summary = "Cria moto";
            return op;
        });

        motos.MapPut("/{id:decimal}", UpdateMoto).WithOpenApi(op =>
        {
            op.Summary = "Atualiza moto";
            return op;
        });

        motos.MapDelete("/{id:decimal}", DeleteMoto).WithOpenApi(op =>
        {
            op.Summary = "Remove moto";
            return op;
        });
    }

    private static async Task<IResult> GetMotos(HttpContext ctx, AppDbContext db, int page = 1, int pageSize = 20, string? placa = null)
    {
        var query = db.Motos.Include(m => m.Zona).Include(m => m.Patio).Include(m => m.Status).AsQueryable();

        if (!string.IsNullOrWhiteSpace(placa))
            query = query.Where(m => EF.Functions.Like(m.Placa, $"%{placa}%"));


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
