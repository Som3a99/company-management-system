using ERP.BLL.DTOs;

namespace ERP.BLL.Interfaces
{
    public interface IAuditAnomalyService
    {
        Task<List<AuditAnomalyFlag>> DetectAnomaliesAsync(int lookbackHours = 24);
    }
}
