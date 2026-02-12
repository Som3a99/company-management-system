namespace ERP.BLL.Common
{
    public class TaskOperationResult
    {
        public bool Succeeded { get; init; }
        public string? Error { get; init; }

        public static TaskOperationResult Success() => new() { Succeeded = true };
        public static TaskOperationResult Forbidden(string? message = null) =>
            new() { Succeeded = false, Error = message ?? "Forbidden." };
        public static TaskOperationResult Invalid(string message) =>
            new() { Succeeded = false, Error = message };
        public static TaskOperationResult NotFound(string? message = null) =>
            new() { Succeeded = false, Error = message ?? "Not found." };
    }

    public class TaskOperationResult<T> : TaskOperationResult
    {
        public T? Data { get; init; }

        public static TaskOperationResult<T> Success(T data) =>
            new() { Succeeded = true, Data = data };

        public new static TaskOperationResult<T> Forbidden(string? message = null) =>
            new() { Succeeded = false, Error = message ?? "Forbidden." };

        public new static TaskOperationResult<T> Invalid(string message) =>
            new() { Succeeded = false, Error = message };

        public new static TaskOperationResult<T> NotFound(string? message = null) =>
            new() { Succeeded = false, Error = message ?? "Not found." };
    }
}
