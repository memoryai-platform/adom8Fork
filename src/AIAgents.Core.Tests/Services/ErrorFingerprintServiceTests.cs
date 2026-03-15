using AIAgents.Core.Services;

namespace AIAgents.Core.Tests.Services;

public sealed class ErrorFingerprintServiceTests
{
    private readonly ErrorFingerprintService _service = new();

    [Fact]
    public void Normalize_ReplacesVariableValuesWithStablePlaceholders()
    {
        var normalized = _service.Normalize(
            " Contoso.Plugins.AccountSync ",
            " Create ",
            " Account ",
            "Failure for 6f9619ff-8b86-d011-b42d-00cf4fc964ff at 2026-03-14T11:22:33.123Z on record 123456789 from 0x00ABCDEF.");

        Assert.Equal(
            "contoso.plugins.accountsync|create|account|failure for [guid] at [timestamp] on record [id] from [addr].",
            normalized);
    }

    [Fact]
    public void ComputeFingerprint_EquivalentMessagesProduceSameHash()
    {
        var first = _service.ComputeFingerprint(
            "Contoso.Plugins.AccountSync",
            "Create",
            "account",
            "Failure for 6f9619ff-8b86-d011-b42d-00cf4fc964ff at 2026-03-14T11:22:33.123Z on record 123456789 from 0x00ABCDEF.");
        var second = _service.ComputeFingerprint(
            " contoso.plugins.accountsync ",
            " create ",
            "Account",
            "Failure for 11111111-2222-3333-4444-555555555555 at 2026-03-15T01:02:03.456Z on record 987654321 from 0x00123456.");

        Assert.Equal(first, second);
    }

    [Fact]
    public void ComputeFingerprint_DifferentPluginContextProducesDifferentHash()
    {
        var first = _service.ComputeFingerprint(
            "Contoso.Plugins.AccountSync",
            "Create",
            "account",
            "Failure for 6f9619ff-8b86-d011-b42d-00cf4fc964ff.");
        var second = _service.ComputeFingerprint(
            "Contoso.Plugins.ContactSync",
            "Create",
            "account",
            "Failure for 11111111-2222-3333-4444-555555555555.");

        Assert.NotEqual(first, second);
    }
}
