# Static Web App for Dashboard
resource "azurerm_static_web_app" "dashboard" {
  name                = var.static_web_app_name
  resource_group_name = azurerm_resource_group.ai_agents.name
  location            = "eastus2"
  sku_tier            = "Free"
  sku_size            = "Free"
  
  tags = {
    Environment = var.environment
    Purpose     = "AI Agent Monitoring Dashboard"
  }
}
