using KargoApi.Data;
using Kargo.Domain;
using Microsoft.EntityFrameworkCore;

namespace KargoApi.Features.Branches
{
    public static class BranchEndpoints
    {
        public static IEndpointRouteBuilder MapBranchEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/branches").WithTags("Branches");

            group.MapGet("/", GetAll);
            group.MapPost("/", Create);

            return app;
        }

        private static async Task<IResult> GetAll(AppDbContext db)
        {
            var branches = await db.Branches.AsNoTracking().ToListAsync();
            return Results.Ok(branches);
        }

        private static async Task<IResult> Create(AppDbContext db, Branch branch)
        {
            db.Branches.Add(branch);
            await db.SaveChangesAsync();
            return Results.Created($"/branches/{branch.Id}", branch);
        }
    }
}
