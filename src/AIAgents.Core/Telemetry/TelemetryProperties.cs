namespace AIAgents.Core.Telemetry;

/// <summary>
/// Standardized property keys for Application Insights custom events.
/// Use as dictionary keys with <see cref="Microsoft.ApplicationInsights.TelemetryClient.TrackEvent"/>
/// to ensure consistent property naming across all agents.
/// </summary>
public static class TelemetryProperties
{
    public const string WorkItemId = "workItemId";
    public const string AgentType = "agentType";
    public const string CorrelationId = "correlationId";
    public const string AutonomyLevel = "autonomyLevel";
    public const string TokensInput = "tokensInput";
    public const string TokensOutput = "tokensOutput";
    public const string Cost = "cost";
    public const string Model = "model";
    public const string ReviewScore = "reviewScore";
    public const string FromState = "fromState";
    public const string ToState = "toState";
    public const string ErrorCategory = "errorCategory";
    public const string ErrorMessage = "errorMessage";
    public const string Duration = "duration";
}
