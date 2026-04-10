using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using ProjectManager.API.Data;

namespace ProjectManager.API.Filters
{
    public class ProjectNotArchivedFilter : IAsyncActionFilter
    {
        private readonly AppDbContext _context;

        public ProjectNotArchivedFilter(AppDbContext context)
        {
            _context = context;
        }
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            // GET kérések átengedése
            if (context.HttpContext.Request.Method == HttpMethods.Get)
            {
                await next();
                return;
            }

            // Archivált projekt ellenőrzés
            if (context.ActionArguments.TryGetValue("projectId", out var projectIdObj) && projectIdObj is Guid projectId)
            {
                var project = await _context.Projects
                    .FirstOrDefaultAsync(p => p.Id == projectId);

                if (project?.IsArchived == true)
                {
                    context.Result = new ObjectResult("A projekt archiválva van, nem végezhető rajta írás művelet!")
                    {
                        StatusCode = StatusCodes.Status403Forbidden
                    };
                    return;
                }
            }

            await next();
        }
    }
}
