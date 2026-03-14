using AIAgents.Core.Configuration;
using AIAgents.Functions.Functions;
using AIAgents.Functions.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace AIAgents.Functions.Tests.Functions;

public sealed class CopilotTimeoutCheckerTests
{
    [Fact]
    public async Task RunAsync_RecoversCompletedDelegation_DoesNotMarkTimedOut()
    {
        var delegation = new CopilotDelegation
        {
            WorkItemId = 145,
            IssueNumber = 27,
            CorrelationId = "corr-1",
            BranchName = "feature/US-145",
            DelegatedAt = DateTime.UtcNow.AddMinutes(-45),
            Status = "Pending"
        };

        var delegationService = new Mock<ICopilotDelegationService>();
        delegationService.Setup(x => x.GetPendingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { delegation });

        var activityLogger = new Mock<IActivityLogger>();
        var completionService = new Mock<ICopilotCompletionService>();
        completionService.Setup(x => x.ProbeAndCompletePendingDelegationAsync(
                delegation,
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CopilotCompletionProbeResult.Completed);

        var checker = new CopilotTimeoutChecker(
            Options.Create(new CopilotOptions { Enabled = true, TimeoutMinutes = 30 }),
            delegationService.Object,
            activityLogger.Object,
            completionService.Object,
            Mock.Of<ILogger<CopilotTimeoutChecker>>());

        await checker.RunAsync(null!, CancellationToken.None);

        delegationService.Verify(
            x => x.UpdateAsync(It.IsAny<CopilotDelegation>(), It.IsAny<CancellationToken>()),
            Times.Never);
        activityLogger.Verify(
            x => x.LogAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RunAsync_StillPendingAfterFallback_MarksTimedOut()
    {
        var delegation = new CopilotDelegation
        {
            WorkItemId = 146,
            IssueNumber = 28,
            CorrelationId = "corr-2",
            BranchName = "feature/US-146",
            DelegatedAt = DateTime.UtcNow.AddMinutes(-45),
            Status = "Pending"
        };

        var delegationService = new Mock<ICopilotDelegationService>();
        delegationService.Setup(x => x.GetPendingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { delegation });

        var activityLogger = new Mock<IActivityLogger>();
        var completionService = new Mock<ICopilotCompletionService>();
        completionService.Setup(x => x.ProbeAndCompletePendingDelegationAsync(
                delegation,
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CopilotCompletionProbeResult.NoSignal);

        var checker = new CopilotTimeoutChecker(
            Options.Create(new CopilotOptions { Enabled = true, TimeoutMinutes = 30 }),
            delegationService.Object,
            activityLogger.Object,
            completionService.Object,
            Mock.Of<ILogger<CopilotTimeoutChecker>>());

        await checker.RunAsync(null!, CancellationToken.None);

        delegationService.Verify(
            x => x.UpdateAsync(
                It.Is<CopilotDelegation>(d =>
                    d.WorkItemId == 146 &&
                    d.Status == "TimedOut" &&
                    d.CompletedAt.HasValue),
                It.IsAny<CancellationToken>()),
            Times.Once);

        activityLogger.Verify(
            x => x.LogAsync(
                "Coding",
                146,
                It.Is<string>(message => message.Contains("timed out", StringComparison.OrdinalIgnoreCase)),
                "warning",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RunAsync_IssueReadyButWaitingForPr_KeepsDelegationPending()
    {
        var delegation = new CopilotDelegation
        {
            WorkItemId = 147,
            IssueNumber = 29,
            CorrelationId = "corr-3",
            BranchName = "feature/US-147",
            DelegatedAt = DateTime.UtcNow.AddMinutes(-45),
            Status = "Pending"
        };

        var delegationService = new Mock<ICopilotDelegationService>();
        delegationService.Setup(x => x.GetPendingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { delegation });

        var activityLogger = new Mock<IActivityLogger>();
        var completionService = new Mock<ICopilotCompletionService>();
        completionService.Setup(x => x.ProbeAndCompletePendingDelegationAsync(
                delegation,
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CopilotCompletionProbeResult.WaitingForPullRequest);

        var checker = new CopilotTimeoutChecker(
            Options.Create(new CopilotOptions { Enabled = true, TimeoutMinutes = 30 }),
            delegationService.Object,
            activityLogger.Object,
            completionService.Object,
            Mock.Of<ILogger<CopilotTimeoutChecker>>());

        await checker.RunAsync(null!, CancellationToken.None);

        delegationService.Verify(
            x => x.UpdateAsync(It.IsAny<CopilotDelegation>(), It.IsAny<CancellationToken>()),
            Times.Never);
        activityLogger.Verify(
            x => x.LogAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RunAsync_BeforeTimeout_StillPollsForCompletion()
    {
        var delegation = new CopilotDelegation
        {
            WorkItemId = 148,
            IssueNumber = 30,
            CorrelationId = "corr-4",
            BranchName = "feature/US-148",
            DelegatedAt = DateTime.UtcNow.AddMinutes(-5),
            Status = "Pending"
        };

        var delegationService = new Mock<ICopilotDelegationService>();
        delegationService.Setup(x => x.GetPendingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { delegation });

        var activityLogger = new Mock<IActivityLogger>();
        var completionService = new Mock<ICopilotCompletionService>();
        completionService.Setup(x => x.ProbeAndCompletePendingDelegationAsync(
                delegation,
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CopilotCompletionProbeResult.NoSignal);

        var checker = new CopilotTimeoutChecker(
            Options.Create(new CopilotOptions { Enabled = true, TimeoutMinutes = 30 }),
            delegationService.Object,
            activityLogger.Object,
            completionService.Object,
            Mock.Of<ILogger<CopilotTimeoutChecker>>());

        await checker.RunAsync(null!, CancellationToken.None);

        completionService.Verify(
            x => x.ProbeAndCompletePendingDelegationAsync(
                delegation,
                It.Is<string>(source => source.Contains("timer poll", StringComparison.OrdinalIgnoreCase)),
                It.IsAny<CancellationToken>()),
            Times.Once);
        delegationService.Verify(
            x => x.UpdateAsync(It.IsAny<CopilotDelegation>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
