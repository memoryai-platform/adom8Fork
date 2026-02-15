# =============================================================================
# Monitoring & Alerting
# Azure Monitor alert rules for proactive issue detection.
# Requires alert_email variable for notification delivery.
# =============================================================================

# Action Group — delivers alert notifications
resource "azurerm_monitor_action_group" "alerts" {
  name                = "${var.function_app_name}-alerts"
  resource_group_name = azurerm_resource_group.ai_agents.name
  short_name          = "aiagents"

  email_receiver {
    name          = "admin"
    email_address = var.alert_email
  }

  tags = {
    Environment = var.environment
  }
}

# Alert: Function HTTP 5xx errors > 10 in 5 minutes
resource "azurerm_monitor_metric_alert" "function_errors" {
  name                = "${var.function_app_name}-function-errors"
  resource_group_name = azurerm_resource_group.ai_agents.name
  scopes              = [azurerm_windows_function_app.agents.id]
  description         = "Alert when function error rate exceeds 10 errors in 5 minutes"
  severity            = 2
  frequency           = "PT1M"
  window_size         = "PT5M"

  criteria {
    metric_namespace = "Microsoft.Web/sites"
    metric_name      = "Http5xx"
    aggregation      = "Total"
    operator         = "GreaterThan"
    threshold        = 10
  }

  action {
    action_group_id = azurerm_monitor_action_group.alerts.id
  }

  tags = {
    Environment = var.environment
  }
}

# Alert: Queue depth > 100 messages (stuck processing)
# Uses a log-based query since queue metrics require sub-resource scoping
resource "azurerm_monitor_scheduled_query_rules_alert_v2" "queue_depth" {
  name                = "${var.function_app_name}-queue-depth"
  resource_group_name = azurerm_resource_group.ai_agents.name
  location            = azurerm_resource_group.ai_agents.location
  description         = "Alert when agent-tasks queue has too many pending messages"
  severity            = 3
  enabled             = true

  evaluation_frequency = "PT5M"
  window_duration      = "PT5M"

  scopes = [azurerm_application_insights.functions.id]

  criteria {
    query = <<-QUERY
      customMetrics
      | where name == "QueueDepth"
      | summarize avg(value) by bin(timestamp, 5m)
      | where avg_value > 100
    QUERY

    time_aggregation_method = "Count"
    operator                = "GreaterThan"
    threshold               = 0

    failing_periods {
      minimum_failing_periods_to_trigger_alert = 1
      number_of_evaluation_periods             = 1
    }
  }

  action {
    action_groups = [azurerm_monitor_action_group.alerts.id]
  }

  tags = {
    Environment = var.environment
  }
}

# Alert: Function execution duration > 10 minutes average
resource "azurerm_monitor_metric_alert" "function_duration" {
  name                = "${var.function_app_name}-function-duration"
  resource_group_name = azurerm_resource_group.ai_agents.name
  scopes              = [azurerm_windows_function_app.agents.id]
  description         = "Alert when average function execution exceeds 10 minutes"
  severity            = 3
  frequency           = "PT5M"
  window_size         = "PT15M"

  criteria {
    metric_namespace = "Microsoft.Web/sites"
    metric_name      = "FunctionExecutionUnits"
    aggregation      = "Average"
    operator         = "GreaterThan"
    threshold        = 600000000 # 10 minutes in MB-milliseconds (consumption plan units)
  }

  action {
    action_group_id = azurerm_monitor_action_group.alerts.id
  }

  tags = {
    Environment = var.environment
  }
}

# Alert: Dead letter queue has any messages (immediate notification)
resource "azurerm_monitor_scheduled_query_rules_alert_v2" "dead_letter_alert" {
  name                = "${var.function_app_name}-dead-letter"
  resource_group_name = azurerm_resource_group.ai_agents.name
  location            = azurerm_resource_group.ai_agents.location
  description         = "Alert when messages appear in the dead letter (poison) queue"
  severity            = 2
  enabled             = true

  evaluation_frequency = "PT5M"
  window_duration      = "PT5M"

  scopes = [azurerm_application_insights.functions.id]

  criteria {
    query = <<-QUERY
      customEvents
      | where name == "DeadLetterProcessed"
      | summarize count() by bin(timestamp, 5m)
    QUERY

    time_aggregation_method = "Count"
    operator                = "GreaterThan"
    threshold               = 0

    failing_periods {
      minimum_failing_periods_to_trigger_alert = 1
      number_of_evaluation_periods             = 1
    }
  }

  action {
    action_groups = [azurerm_monitor_action_group.alerts.id]
  }

  tags = {
    Environment = var.environment
  }
}

# Alert: AI API errors (429/401/500) — log-based query alert
resource "azurerm_monitor_scheduled_query_rules_alert_v2" "ai_api_errors" {
  name                = "${var.function_app_name}-ai-api-errors"
  resource_group_name = azurerm_resource_group.ai_agents.name
  location            = azurerm_resource_group.ai_agents.location
  description         = "Alert when AI API errors exceed 5 in 5 minutes"
  severity            = 2
  enabled             = true

  evaluation_frequency = "PT5M"
  window_duration      = "PT5M"

  scopes = [azurerm_application_insights.functions.id]

  criteria {
    query = <<-QUERY
      customEvents
      | where name == "AgentFailed"
      | where customDimensions.errorCategory in ("Transient", "Configuration")
      | summarize count() by bin(timestamp, 5m)
    QUERY

    time_aggregation_method = "Count"
    operator                = "GreaterThan"
    threshold               = 5

    failing_periods {
      minimum_failing_periods_to_trigger_alert = 1
      number_of_evaluation_periods             = 1
    }
  }

  action {
    action_groups = [azurerm_monitor_action_group.alerts.id]
  }

  tags = {
    Environment = var.environment
  }
}
