terraform {
  required_version = ">= 1.0"
  
  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 3.0"
    }
  }
}

provider "azurerm" {
  features {
    resource_group {
      prevent_deletion_if_contains_resources = false
    }
  }
}

resource "azurerm_resource_group" "ai_agents" {
  name     = var.resource_group_name
  location = var.location
  
  tags = {
    Environment = var.environment
    Project     = "AI-Development-Agents"
    ManagedBy   = "Terraform"
  }
}
