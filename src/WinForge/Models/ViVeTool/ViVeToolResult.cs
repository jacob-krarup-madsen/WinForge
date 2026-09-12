namespace ViVeToolApp.Models;

/// <summary>
/// Status classification for ViVeTool execution.
/// </summary>
public enum ViVeToolStatus
{
    Success,
    UnsupportedOrNotFound,
    Skip = UnsupportedOrNotFound,
    Warning,
    Error
}

/// <summary>
/// Alias enum for ViVeResultStatus.
/// </summary>
public enum ViVeResultStatus
{
    Success,
    Skip,
    Warning,
    Error
}

/// <summary>
/// Represents the outcome of executing a ViVeTool command on a single feature ID.
/// </summary>
public class ViVeToolResult
{
    public long FeatureId { get; set; }
    public ViVeExecutionMode Mode { get; set; } = ViVeExecutionMode.Enable;
    public ViVeToolStatus Status { get; set; } = ViVeToolStatus.Success;
    public int ExitCode { get; set; }
    public string Output { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;

    public string Message
    {
        get => !string.IsNullOrEmpty(ErrorMessage) ? ErrorMessage : Output;
        set
        {
            if (Status == ViVeToolStatus.Error || Status == ViVeToolStatus.Warning)
            {
                ErrorMessage = value;
            }
            else
            {
                Output = value;
            }
        }
    }

    public string RawOutput
    {
        get => !string.IsNullOrEmpty(Output) ? Output : ErrorMessage;
        set => Output = value;
    }

    public bool IsSuccess => Status == ViVeToolStatus.Success;
    public bool IsSkipped => Status == ViVeToolStatus.UnsupportedOrNotFound || Status == ViVeToolStatus.Skip;
    public bool IsError => Status == ViVeToolStatus.Error;

    public ViVeToolResult()
    {
    }

    public ViVeToolResult(
        long featureId,
        ViVeExecutionMode mode,
        ViVeToolStatus status,
        int exitCode,
        string output,
        string errorMessage = "")
    {
        FeatureId = featureId;
        Mode = mode;
        Status = status;
        ExitCode = exitCode;
        Output = output;
        ErrorMessage = errorMessage;
    }
}
