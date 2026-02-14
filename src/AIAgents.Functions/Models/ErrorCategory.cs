namespace AIAgents.Functions.Models;

/// <summary>
/// Categorizes agent execution errors to drive retry vs. fail-permanently decisions.
/// </summary>
public enum ErrorCategory
{
    /// <summary>API rate limits, network timeouts, temporary service outages. Retry is likely to succeed.</summary>
    Transient,

    /// <summary>Invalid API key, expired PAT, wrong permissions. Will not self-heal — requires operator action.</summary>
    Configuration,

    /// <summary>Invalid work item format, malformed input, missing required fields. Needs content fix.</summary>
    Data,

    /// <summary>Bug in agent logic, unexpected exception. May be transient, worth retrying once.</summary>
    Code
}
