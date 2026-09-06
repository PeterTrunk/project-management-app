using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using ProjectManager.API.Common.Exceptions;
using ProjectManager.API.Data;
using ProjectManager.API.DTOs.Labels;
using ProjectManager.API.Hubs;
using ProjectManager.API.Model;

namespace ProjectManager.API.Services.LabelService
{
    public class LabelService : ILabelService
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<ProjectHub> _hubContext;
        private readonly ILogger<LabelService> _logger;

        public LabelService(
            AppDbContext context, 
            IHubContext<ProjectHub> hubContext,
            ILogger<LabelService> logger)
        {
            _context = context;
            _hubContext = hubContext;
            _logger = logger;
        }

        public async Task AddLabelToTaskAsync(Guid projectId, Guid taskId, Guid labelId)
        {
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null)
                throw new NotFoundException("Projekt nem található!");

            var task = await _context.ProjectTasks
                .FirstOrDefaultAsync(t => t.Id == taskId && t.ProjectId == projectId);
            if (task == null)
                throw new NotFoundException("Feladat nem található");

            //A címke azonosítója a kérésből jön: enélkül idegen projekt címkéje is
            //rákerülhet a taskra, és a neve/színe megjelenik a felületen
            var labelExists = await _context.Labels
                .AnyAsync(l => l.Id == labelId && l.ProjectId == projectId);
            if (!labelExists)
                throw new NotFoundException("Cimke nem található!");

            var existing = await _context.LabelTasks.FirstOrDefaultAsync(lt => lt.TaskId == taskId && lt.LabelId == labelId);
            if (existing != null)
                throw new ConflictException("Ez a cimke már hozzá van rendelve ehhez a feladathoz!");

            var labelTask = new LabelTask
            {
                TaskId = taskId,
                LabelId = labelId
            };
            _context.LabelTasks.Add(labelTask);
            await _context.SaveChangesAsync();

            try
            {
                await _hubContext.Clients
                    .Group($"project-{projectId}")
                    .SendAsync("TaskLabelAdded", new { taskId, labelId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SignalR broadcast hiba | Event: {Event} | ProjectId: {ProjectId}",
                    "TaskLabelAdded", projectId);
            }
        }

        public async Task<LabelResponseDto> CreateLabelAsync(Guid projectId, CreateLabelDto dto)
        {
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null)
                throw new NotFoundException("Projekt nem található!");

            var label = new Label
            {
                ProjectId = projectId,
                Name = dto.Name,
                Color = dto.Color,
            };
            _context.Labels.Add(label);
            await _context.SaveChangesAsync();

            try
            {
                await _hubContext.Clients
                    .Group($"project-{projectId}")
                    .SendAsync("LabelCreated", new { label.Id, label.Name, label.Color });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SignalR broadcast hiba | Event: {Event} | ProjectId: {ProjectId}",
                    "LabelCreated", projectId);
            }

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
                throw new NotFoundException("Projekt nem található!");

            var label = await _context.Labels
                .FirstOrDefaultAsync(l => l.Id == labelId && l.ProjectId == projectId);
            if (label == null)
                throw new NotFoundException("Cimke nem található");

            _context.Labels.Remove(label);
            await _context.SaveChangesAsync();

            try
            {
                await _hubContext.Clients
                    .Group($"project-{projectId}")
                    .SendAsync("LabelDeleted", new { labelId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SignalR broadcast hiba | Event: {Event} | ProjectId: {ProjectId}",
                    "LabelDeleted", projectId);
            }
        }

        public async Task<List<LabelResponseDto>> GetLabelsAsync(Guid projectId)
        {
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null)
                throw new NotFoundException("Projekt nem található!");

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
                throw new NotFoundException("Projekt nem található!");

            var task = await _context.ProjectTasks
                .FirstOrDefaultAsync(t => t.Id == taskId && t.ProjectId == projectId);
            if (task == null)
                throw new NotFoundException("Feladat nem található");

            var labelTask = await _context.LabelTasks.FirstOrDefaultAsync(lt => lt.TaskId == taskId && lt.LabelId == labelId);
            if (labelTask == null)
                throw new ValidationException("Ez a cimke nincs ehhez a feladathoz rendelve!");

            _context.LabelTasks.Remove(labelTask);
            await _context.SaveChangesAsync();

            try
            {
                await _hubContext.Clients
                    .Group($"project-{projectId}")
                    .SendAsync("TaskLabelRemoved", new { taskId, labelId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SignalR broadcast hiba | Event: {Event} | ProjectId: {ProjectId}",
                    "TaskLabelRemoved", projectId);
            }
        }
    }
}
