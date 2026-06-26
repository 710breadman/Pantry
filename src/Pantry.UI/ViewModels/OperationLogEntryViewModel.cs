using Pantry.Infrastructure;

namespace Pantry.UI.ViewModels;

public sealed class OperationLogEntryViewModel
{
    public OperationLogEntryViewModel(OperationLogEntry entry)
    {
        Time = entry.TimestampUtc.ToLocalTime().ToString("g");
        Category = entry.Category;
        Message = entry.Message;
        Details = entry.DetailsJson ?? string.Empty;
    }

    public string Time { get; }

    public string Category { get; }

    public string Message { get; }

    public string Details { get; }
}

