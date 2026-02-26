using ERP.BLL.DTOs;

namespace ERP.BLL.Interfaces
{
    public interface ITaskAssignmentSuggestionService
    {
        Task<List<TaskAssignmentSuggestion>> GetSuggestionsAsync(int projectId, string? taskTitle);
    }
}
