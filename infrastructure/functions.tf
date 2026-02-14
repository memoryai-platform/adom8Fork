# App Service Plan (Consumption Y1 - serverless)
resource "azurerm_service_plan" "functions" {
  name                = "${var.function_app_name}-plan"
  resource_group_name = azurerm_resource_group.ai_agents.name
  location            = azurerm_resource_group.ai_agents.location
  os_type             = "Linux"
  sku_name            = "Y1"
  
  tags = {
    Environment = var.environment
  }
}

# Application Insights for monitoring
resource "azurerm_application_insights" "functions" {
  name                = "${var.function_app_name}-insights"
  resource_group_name = azurerm_resource_group.ai_agents.name
  location            = azurerm_resource_group.ai_agents.location
  application_type    = "web"
  retention_in_days   = 30
  
  tags = {
    Environment = var.environment
  }
}

# Linux Function App (.NET 8 Isolated)
resource "azurerm_linux_function_app" "agents" {
  name                       = var.function_app_name
  resource_group_name        = azurerm_resource_group.ai_agents.name
  location                   = azurerm_resource_group.ai_agents.location
  service_plan_id            = azurerm_service_plan.functions.id
  storage_account_name       = azurerm_storage_account.functions.name
  storage_account_access_key = azurerm_storage_account.functions.primary_access_key
  
  site_config {
    application_stack {
      dotnet_version              = "8.0"
      use_dotnet_isolated_runtime = true
    }
    
    cors {
      allowed_origins     = ["*"]
      support_credentials = false
    }
    
    application_insights_key               = azurerm_application_insights.functions.instrumentation_key
    application_insights_connection_string = azurerm_application_insights.functions.connection_string
  }
  
  app_settings = {
    "FUNCTIONS_WORKER_RUNTIME"                 = "dotnet-isolated"
    "WEBSITE_RUN_FROM_PACKAGE"                 = "1"
    "APPINSIGHTS_INSTRUMENTATIONKEY"           = azurerm_application_insights.functions.instrumentation_key
    "APPLICATIONINSIGHTS_CONNECTION_STRING"    = azurerm_application_insights.functions.connection_string
    "AzureWebJobsStorage"                      = azurerm_storage_account.functions.primary_connection_string
    "WEBSITE_CONTENTAZUREFILECONNECTIONSTRING" = azurerm_storage_account.functions.primary_connection_string
    "WEBSITE_CONTENTSHARE"                     = "${var.function_app_name}-content"
    
    # AI Configuration (SET MANUALLY AFTER DEPLOYMENT)
    "AI__Provider"  = "Claude"
    "AI__Model"     = "claude-sonnet-4-20250514"
    "AI__ApiKey"    = ""
    "AI__Endpoint"  = ""
    "AI__MaxTokens" = "4096"
    
    # Azure DevOps Configuration (SET MANUALLY)
    "AzureDevOps__OrganizationUrl" = ""
    "AzureDevOps__Pat"             = ""
    "AzureDevOps__Project"         = ""
    
    # Git Configuration (SET MANUALLY)
    "Git__RepositoryUrl" = ""
    "Git__Username"      = "ai-agent-bot"
    "Git__Token"         = ""
    "Git__Email"         = "ai-agents@example.com"
    "Git__Name"          = "AI Agent Bot"
  }
  
  tags = {
    Environment = var.environment
  }
  
  lifecycle {
    ignore_changes = [
      app_settings["AI__ApiKey"],
      app_settings["AzureDevOps__Pat"],
      app_settings["Git__Token"],
    ]
  }
}
