using System.Net;
using System.Text.Json;
using AIAgents.Core.Configuration;
using AIAgents.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;

namespace AIAgents.Core.Tests.Services;

public sealed class GitHubApiContextServiceTests
{
    private static GitHubOptions CreateOptions() => new()
    {
        Owner = "toddpick",
        Repo = "adom8",
        Token = "ghp_test"
    };

    [Fact]
    public async Task GetFileTreeAsync_BranchVisibleAfterInitial404_RetriesAndSucceeds()
    {
        var branchResponseCount = 0;

        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage request, CancellationToken _) =>
            {
                var path = request.RequestUri!.PathAndQuery;

                if (path.Contains("/branches/feature%2FUS-149", StringComparison.OrdinalIgnoreCase))
                {
                    branchResponseCount++;
                    if (branchResponseCount == 1)
                    {
                        return new HttpResponseMessage(HttpStatusCode.NotFound)
                        {
                            Content = new StringContent("{\"message\":\"Branch not found\"}")
                        };
                    }

                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("""
                        {
                          "commit": {
                            "sha": "abc123def456"
                          }
                        }
                        """)
                    };
                }

                if (path.Contains("/git/trees/abc123def456?recursive=1", StringComparison.OrdinalIgnoreCase))
                {
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("""
                        {
                          "tree": [
                            { "path": "src/App.jsx", "type": "blob" },
                            { "path": "src", "type": "tree" }
                          ]
                        }
                        """)
                    };
                }

                throw new InvalidOperationException($"Unexpected GitHub API request: {path}");
            });

        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("https://api.github.com/")
        };

        var service = new GitHubApiContextService(
            Options.Create(CreateOptions()),
            NullLogger<GitHubApiContextService>.Instance,
            httpClient);

        var files = await service.GetFileTreeAsync("feature/US-149");

        Assert.Equal(2, branchResponseCount);
        Assert.Single(files);
        Assert.Equal("src/App.jsx", files[0]);
    }
}
