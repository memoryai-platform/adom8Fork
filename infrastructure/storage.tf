# Storage Account for Azure Functions
resource "azurerm_storage_account" "functions" {
  name                     = var.storage_account_name
  resource_group_name      = azurerm_resource_group.ai_agents.name
  location                 = azurerm_resource_group.ai_agents.location
  account_tier             = "Standard"
  account_replication_type = "LRS"
  
  tags = {
    Environment = var.environment
    Purpose     = "Functions and Queues"
  }
}

# Queue for Agent Tasks
resource "azurerm_storage_queue" "agent_tasks" {
  name                 = "agent-tasks"
  storage_account_name = azurerm_storage_account.functions.name
}

# Queue for Dead Letter (failed tasks)
resource "azurerm_storage_queue" "agent_tasks_poison" {
  name                 = "agent-tasks-poison"
  storage_account_name = azurerm_storage_account.functions.name
}

# Container for temporary Git repositories
resource "azurerm_storage_container" "temp_repos" {
  name                  = "temp-repos"
  storage_account_name  = azurerm_storage_account.functions.name
  container_access_type = "private"
}

# Table for Agent Activity Log (for dashboard)
resource "azurerm_storage_table" "activity_log" {
  name                 = "activitylog"
  storage_account_name = azurerm_storage_account.functions.name
}
