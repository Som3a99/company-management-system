using ERP.BLL.DTOs;
using ERP.DAL.Models;

namespace ERP.BLL.Interfaces
{
    public interface ITaskRiskService
    {
        TaskRiskResult CalculateRisk(TaskItem task);
    }
}
