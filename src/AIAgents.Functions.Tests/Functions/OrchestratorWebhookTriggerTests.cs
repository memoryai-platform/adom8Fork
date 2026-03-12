using System.Text.Json;
using AIAgents.Functions.Functions;
using AIAgents.Functions.Models;

namespace AIAgents.Functions.Tests.Functions;

public sealed class OrchestratorWebhookTriggerTests
{
    [Fact]
    public void EvaluateTrigger_FreshTransitionIntoAiAgent_IsFreshTrigger()
    {
        var payload = JsonSerializer.Deserialize<ServiceHookPayload>("""
        {
          "eventType": "workitem.updated",
          "resource": {
            "fields": {
              "System.State": {
                "oldValue": "New",
                "newValue": "AI Agent"
              }
            }
          }
        }
        """)!;

        var result = OrchestratorWebhook.EvaluateTrigger(
            payload,
            payload.Resource!.Fields!.State!.OldValue,
            payload.Resource.Fields.State.NewValue,
            payload.GetCurrentState());

        Assert.True(result.IsFreshTrigger);
        Assert.False(result.IsPlanningReplyTrigger);
        Assert.Equal("AI Agent", result.MappedState);
    }

    [Fact]
    public void EvaluateTrigger_CommentAddedWhilePlanning_IsPlanningReplyTrigger()
    {
        var payload = JsonSerializer.Deserialize<ServiceHookPayload>("""
        {
          "eventType": "workitem.commented",
          "resource": {
            "workItemId": 12345,
            "commentVersionRef": {
              "commentId": 777,
              "url": "https://dev.azure.com/org/project/_apis/wit/workItems/12345/comments/777"
            },
            "revision": {
              "id": 12345,
              "fields": {
                "System.State": "Planning"
              }
            }
          }
        }
        """)!;

        var result = OrchestratorWebhook.EvaluateTrigger(
            payload,
            payload.Resource!.Fields?.State?.OldValue,
            payload.Resource.Fields?.State?.NewValue,
            payload.GetCurrentState());

        Assert.False(result.IsFreshTrigger);
        Assert.True(result.IsPlanningReplyTrigger);
        Assert.Equal("Planning", result.MappedState);
    }

    [Fact]
    public void EvaluateTrigger_WorkItemUpdatedWithoutTransition_IsNotTrigger()
    {
        var payload = JsonSerializer.Deserialize<ServiceHookPayload>("""
        {
          "eventType": "workitem.updated",
          "resource": {
            "fields": {
              "System.State": {
                "oldValue": "Active",
                "newValue": "Active"
              }
            }
          }
        }
        """)!;

        var result = OrchestratorWebhook.EvaluateTrigger(
            payload,
            payload.Resource!.Fields!.State!.OldValue,
            payload.Resource.Fields.State.NewValue,
            payload.GetCurrentState());

        Assert.False(result.IsFreshTrigger);
        Assert.False(result.IsPlanningReplyTrigger);
    }
}
