using MottuVision.Data;
using Microsoft.EntityFrameworkCore;
using System.Data;
using Microsoft.AspNetCore.Http;
using MottuVision.Dtos;

namespace MottuVision.Helpers;

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