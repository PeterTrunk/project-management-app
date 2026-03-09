using Microsoft.EntityFrameworkCore;
using ProjectManager.API.Data;
using ProjectManager.API.DTOs.Labels;
using ProjectManager.API.Model;

namespace ProjectManager.API.Services.LabelService
{
    public class LabelService : ILabelService
    {
        private readonly AppDbContext _context;

        public LabelService(AppDbContext context)
        {
            _context = context;
        }

        public async Task AddLabelToTaskAsync(Guid projectId, Guid taskId, Guid labelId)
        {
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null)
                throw new Exception("Projekt nem található!");

            var task = await _context.ProjectTasks.FirstOrDefaultAsync(t => t.Id == taskId);
            if (task == null)
                throw new Exception("Feladat nem található");

            var existing = await _context.LabelTasks.FirstOrDefaultAsync(lt => lt.TaskId == taskId && lt.LabelId == labelId);
            if (existing != null)
                throw new Exception("Ez a cimke már hozzá van rendelve ehhez a feladathoz!");

            var labelTask = new LabelTask
            {
                TaskId = taskId,
                LabelId = labelId
            };
            _context.LabelTasks.Add(labelTask);
            await _context.SaveChangesAsync();
        }

        public async Task<LabelResponseDto> CreateLabelAsync(Guid projectId, CreateLabelDto dto)
        {
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null)
                throw new Exception("Projekt nem található!");

            var label = new Label
            {
                ProjectId = projectId,
                Name = dto.Name,
                Color = dto.Color,
            };
            _context.Labels.Add(label);
            await _context.SaveChangesAsync();

            var response = new LabelResponseDto
            {
                Id = label.Id,
                ProjectId = label.ProjectId,
                Name = label.Name,
                Color = label.Color
            };
            return response;
        }

        public async Task DeleteLabelAsync(Guid projectId, Guid labelId)
        {
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null)
                throw new Exception("Projekt nem található!");

            var label = await _context.Labels.FirstOrDefaultAsync(l => l.Id == labelId);
            if (label == null)
                throw new Exception("Cimke nem található");

            _context.Labels.Remove(label);
            await _context.SaveChangesAsync();
        }

        public async Task<List<LabelResponseDto>> GetLabelsAsync(Guid projectId)
        {
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null)
                throw new Exception("Projekt nem található!");

            var labels = await _context.Labels
                .Where(l => l.ProjectId == projectId)
                .ToListAsync();

            return labels.Select(l => new LabelResponseDto
            {
                Id = l.Id,
                ProjectId = projectId,
                Name = l.Name,
                Color = l.Color
            }).ToList();
        }

        public async Task RemoveLabelFromTaskAsync(Guid projectId, Guid taskId, Guid labelId)
        {
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null)
                throw new Exception("Projekt nem található!");

            var task = await _context.ProjectTasks.FirstOrDefaultAsync(t => t.Id == taskId);
            if (task == null)
                throw new Exception("Feladat nem található");

            var labelTask = await _context.LabelTasks.FirstOrDefaultAsync(lt => lt.TaskId == taskId && lt.LabelId == labelId);
            if (labelTask == null)
                throw new Exception("Ez a cimke nincs ehhez a feladathoz rendelve!");

            _context.LabelTasks.Remove(labelTask);
            await _context.SaveChangesAsync();
        }
    }
}
