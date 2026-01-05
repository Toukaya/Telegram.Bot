namespace TelegramBotService.TaskAI;

public class TaskAiResult
{
    public bool Success { get; set; }
    public string Error { get; set; } = "";

    public static TaskAiResult Ok() => new() { Success = true };
    public static TaskAiResult Fail(string error) => new() { Success = false, Error = error };
}

public class TaskAiResult<T> : TaskAiResult
{
    public T Data { get; set; }

    public static TaskAiResult<T> Ok(T data) => new() { Success = true, Data = data };
    public new static TaskAiResult<T> Fail(string error) => new() { Success = false, Error = error };
}
