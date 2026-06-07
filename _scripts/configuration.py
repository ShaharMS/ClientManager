from __future__ import annotations

import os
from pathlib import Path

SCRIPT_DIR = Path(__file__).resolve().parent
REPO_ROOT = SCRIPT_DIR.parent

SHARED_API_SETTINGS = {
    "base_url": "http://localhost:5062",
    "prefix": "api/v1",
    "prefix_with_leading_slash": "/api/v1",
}

SHARED_LOCAL_RUNTIME_SETTINGS = {
    "docker_host": "host.docker.internal",
    "api_port": 5062,
}

SHARED_QUERY_SETTINGS = {
    "search_body": {"take": 100},
    "client_summaries_page_size": 100,
}

SHARED_DATA_SETTINGS = {
    "storage_data_dir_env_var": "CLIENTMANAGER_STORAGE_DATA_DIR",
    "repo_data_directory": REPO_ROOT / "data",
    "api_data_directory": REPO_ROOT / "ClientManager.Api" / "data",
    "usage_snapshots_collection": "UsageSnapshots",
    "usage_snapshots_file": "UsageSnapshots.json",
    "counters_file": "_counters.json",
}

SERVICE_CATALOG = [
    {"id": "auth-service", "name": "Authentication Service", "isEnabled": True},
    {"id": "billing-service", "name": "Billing & Payments", "isEnabled": True},
    {"id": "notification-service", "name": "Notification Dispatcher", "isEnabled": True},
    {"id": "analytics-service", "name": "Analytics Pipeline", "isEnabled": True},
    {"id": "storage-service", "name": "Object Storage Gateway", "isEnabled": True},
    {"id": "search-service", "name": "Search & Indexing", "isEnabled": False},
    {"id": "email-service", "name": "Email Delivery", "isEnabled": True},
    {"id": "sms-service", "name": "SMS Gateway", "isEnabled": True},
    {"id": "cache-service", "name": "Distributed Cache", "isEnabled": True},
    {"id": "logging-service", "name": "Centralized Logging", "isEnabled": True},
    {"id": "config-service", "name": "Configuration Manager", "isEnabled": True},
    {"id": "scheduler-service", "name": "Task Scheduler", "isEnabled": True},
    {"id": "geo-service", "name": "Geolocation API", "isEnabled": True},
    {"id": "media-service", "name": "Media Processing", "isEnabled": True},
    {"id": "pdf-service", "name": "PDF Generator", "isEnabled": True},
    {"id": "audit-service", "name": "Audit Trail", "isEnabled": True},
    {"id": "webhook-service", "name": "Webhook Relay", "isEnabled": True},
    {"id": "translate-service", "name": "Translation API", "isEnabled": True},
    {"id": "ml-service", "name": "ML Inference Engine", "isEnabled": True},
    {"id": "queue-service", "name": "Message Queue", "isEnabled": True},
]

RESOURCE_POOL_CATALOG = [
    {"id": "db-connections", "name": "Database Connection Pool", "maxSlots": 50, "allocationTtl": "00:05:00"},
    {"id": "worker-threads", "name": "Background Worker Threads", "maxSlots": 20, "allocationTtl": "00:10:00"},
    {"id": "file-upload-slots", "name": "File Upload Slots", "maxSlots": 10, "allocationTtl": "00:02:00"},
    {"id": "gpu-compute", "name": "GPU Compute Instances", "maxSlots": 8, "allocationTtl": "00:15:00"},
    {"id": "report-workers", "name": "Report Generation Workers", "maxSlots": 12, "allocationTtl": "00:05:00"},
    {"id": "video-transcode", "name": "Video Transcoding Slots", "maxSlots": 6, "allocationTtl": "00:20:00"},
    {"id": "api-gateway-slots", "name": "API Gateway Connections", "maxSlots": 100, "allocationTtl": "00:03:00"},
    {"id": "sandbox-envs", "name": "Sandbox Environments", "maxSlots": 5, "allocationTtl": "00:30:00"},
    {"id": "pdf-render-slots", "name": "PDF Render Workers", "maxSlots": 15, "allocationTtl": "00:02:00"},
    {"id": "ml-inference-slots", "name": "ML Inference Slots", "maxSlots": 10, "allocationTtl": "00:05:00"},
]

GLOBAL_RATE_LIMIT_CATALOG = [
    {"id": "grl-auth", "targetId": "auth-service", "targetType": "Service", "strategy": "FixedWindow", "maxRequests": 500, "window": "00:01:00"},
    {"id": "grl-billing", "targetId": "billing-service", "targetType": "Service", "strategy": "ApproximateSlidingWindow", "maxRequests": 200, "window": "00:01:00"},
    {"id": "grl-notifications", "targetId": "notification-service", "targetType": "Service", "strategy": "TokenBucket", "maxRequests": 300, "window": "00:01:00"},
    {"id": "grl-analytics", "targetId": "analytics-service", "targetType": "Service", "strategy": "FixedWindow", "maxRequests": 1000, "window": "00:01:00"},
    {"id": "grl-storage", "targetId": "storage-service", "targetType": "Service", "strategy": "ApproximateSlidingWindow", "maxRequests": 150, "window": "00:01:00"},
    {"id": "grl-email", "targetId": "email-service", "targetType": "Service", "strategy": "FixedWindow", "maxRequests": 200, "window": "00:01:00"},
    {"id": "grl-sms", "targetId": "sms-service", "targetType": "Service", "strategy": "FixedWindow", "maxRequests": 100, "window": "00:01:00"},
    {"id": "grl-cache", "targetId": "cache-service", "targetType": "Service", "strategy": "FixedWindow", "maxRequests": 2000, "window": "00:01:00"},
    {"id": "grl-logging", "targetId": "logging-service", "targetType": "Service", "strategy": "TokenBucket", "maxRequests": 5000, "window": "00:01:00"},
    {"id": "grl-config", "targetId": "config-service", "targetType": "Service", "strategy": "FixedWindow", "maxRequests": 300, "window": "00:01:00"},
    {"id": "grl-scheduler", "targetId": "scheduler-service", "targetType": "Service", "strategy": "FixedWindow", "maxRequests": 100, "window": "00:01:00"},
    {"id": "grl-geo", "targetId": "geo-service", "targetType": "Service", "strategy": "ApproximateSlidingWindow", "maxRequests": 400, "window": "00:01:00"},
    {"id": "grl-media", "targetId": "media-service", "targetType": "Service", "strategy": "FixedWindow", "maxRequests": 80, "window": "00:01:00"},
    {"id": "grl-pdf", "targetId": "pdf-service", "targetType": "Service", "strategy": "FixedWindow", "maxRequests": 60, "window": "00:01:00"},
    {"id": "grl-audit", "targetId": "audit-service", "targetType": "Service", "strategy": "TokenBucket", "maxRequests": 1000, "window": "00:01:00"},
    {"id": "grl-webhook", "targetId": "webhook-service", "targetType": "Service", "strategy": "FixedWindow", "maxRequests": 200, "window": "00:01:00"},
    {"id": "grl-translate", "targetId": "translate-service", "targetType": "Service", "strategy": "ApproximateSlidingWindow", "maxRequests": 150, "window": "00:01:00"},
    {"id": "grl-ml", "targetId": "ml-service", "targetType": "Service", "strategy": "FixedWindow", "maxRequests": 50, "window": "00:01:00"},
    {"id": "grl-queue", "targetId": "queue-service", "targetType": "Service", "strategy": "TokenBucket", "maxRequests": 500, "window": "00:01:00"},
    {"id": "grl-db", "targetId": "db-connections", "targetType": "ResourcePool", "strategy": "FixedWindow", "maxRequests": 100, "window": "00:01:00"},
    {"id": "grl-workers", "targetId": "worker-threads", "targetType": "ResourcePool", "strategy": "TokenBucket", "maxRequests": 40, "window": "00:01:00"},
    {"id": "grl-gpu", "targetId": "gpu-compute", "targetType": "ResourcePool", "strategy": "FixedWindow", "maxRequests": 20, "window": "00:01:00"},
    {"id": "grl-report", "targetId": "report-workers", "targetType": "ResourcePool", "strategy": "FixedWindow", "maxRequests": 30, "window": "00:01:00"},
    {"id": "grl-video", "targetId": "video-transcode", "targetType": "ResourcePool", "strategy": "FixedWindow", "maxRequests": 15, "window": "00:01:00"},
    {"id": "grl-apigw", "targetId": "api-gateway-slots", "targetType": "ResourcePool", "strategy": "FixedWindow", "maxRequests": 200, "window": "00:01:00"},
    {"id": "grl-sandbox", "targetId": "sandbox-envs", "targetType": "ResourcePool", "strategy": "FixedWindow", "maxRequests": 10, "window": "00:01:00"},
    {"id": "grl-pdfrender", "targetId": "pdf-render-slots", "targetType": "ResourcePool", "strategy": "FixedWindow", "maxRequests": 40, "window": "00:01:00"},
    {"id": "grl-mlinfer", "targetId": "ml-inference-slots", "targetType": "ResourcePool", "strategy": "FixedWindow", "maxRequests": 25, "window": "00:01:00"},
]

CLIENT_CATALOG = [
    {
        "id": "platform-core",
        "name": "Platform Core (Internal)",
        "isEnabled": True,
        "contributesToGlobalLimits": True,
        "exemptFromGlobalLimits": True,
        "globalRateLimit": {"strategy": "TokenBucket", "maxRequests": 1000, "window": "00:01:00", "tokensPerRefill": 100},
        "services": {
            "auth-service": {"isAllowed": True, "rateLimit": {"strategy": "FixedWindow", "maxRequests": 200, "window": "00:01:00"}},
            "billing-service": {"isAllowed": True},
            "notification-service": {"isAllowed": True},
            "analytics-service": {"isAllowed": True},
            "storage-service": {"isAllowed": True},
            "email-service": {"isAllowed": True},
            "cache-service": {"isAllowed": True},
            "logging-service": {"isAllowed": True},
            "config-service": {"isAllowed": True},
            "audit-service": {"isAllowed": True},
            "queue-service": {"isAllowed": True},
        },
        "resourcePools": {
            "db-connections": {"maxSlots": 20},
            "worker-threads": {"maxSlots": 10},
            "file-upload-slots": {"maxSlots": 5},
            "api-gateway-slots": {"maxSlots": 30},
        },
    },
    {
        "id": "mobile-app",
        "name": "Mobile App Backend",
        "isEnabled": True,
        "contributesToGlobalLimits": True,
        "exemptFromGlobalLimits": False,
        "globalRateLimit": {"strategy": "ApproximateSlidingWindow", "maxRequests": 300, "window": "00:01:00"},
        "services": {
            "auth-service": {"isAllowed": True, "rateLimit": {"strategy": "FixedWindow", "maxRequests": 100, "window": "00:01:00"}},
            "billing-service": {"isAllowed": True, "rateLimit": {"strategy": "FixedWindow", "maxRequests": 50, "window": "00:01:00"}},
            "notification-service": {"isAllowed": True},
            "analytics-service": {"isAllowed": True},
            "storage-service": {"isAllowed": True, "rateLimit": {"strategy": "FixedWindow", "maxRequests": 30, "window": "00:01:00"}},
            "geo-service": {"isAllowed": True},
            "media-service": {"isAllowed": True},
        },
        "resourcePools": {
            "db-connections": {"maxSlots": 8},
            "file-upload-slots": {"maxSlots": 3},
            "api-gateway-slots": {"maxSlots": 15},
        },
    },
    {
        "id": "web-dashboard",
        "name": "Web Dashboard",
        "isEnabled": True,
        "contributesToGlobalLimits": True,
        "exemptFromGlobalLimits": False,
        "globalRateLimit": {"strategy": "FixedWindow", "maxRequests": 500, "window": "00:01:00"},
        "services": {
            "auth-service": {"isAllowed": True},
            "analytics-service": {"isAllowed": True},
            "storage-service": {"isAllowed": True},
            "cache-service": {"isAllowed": True},
            "config-service": {"isAllowed": True},
        },
        "resourcePools": {"db-connections": {"maxSlots": 5}},
    },
    {
        "id": "partner-acme",
        "name": "ACME Corp Integration",
        "isEnabled": True,
        "contributesToGlobalLimits": True,
        "exemptFromGlobalLimits": False,
        "globalRateLimit": {"strategy": "FixedWindow", "maxRequests": 60, "window": "00:01:00"},
        "services": {
            "auth-service": {"isAllowed": True, "rateLimit": {"strategy": "FixedWindow", "maxRequests": 20, "window": "00:01:00"}},
            "billing-service": {"isAllowed": True, "rateLimit": {"strategy": "FixedWindow", "maxRequests": 10, "window": "00:01:00"}},
        },
        "resourcePools": {"db-connections": {"maxSlots": 3}},
    },
    {
        "id": "partner-globex",
        "name": "Globex Corporation",
        "isEnabled": False,
        "contributesToGlobalLimits": False,
        "exemptFromGlobalLimits": False,
        "services": {"auth-service": {"isAllowed": True}, "billing-service": {"isAllowed": True}},
        "resourcePools": {},
    },
    {
        "id": "iot-gateway",
        "name": "IoT Device Gateway",
        "isEnabled": True,
        "contributesToGlobalLimits": True,
        "exemptFromGlobalLimits": False,
        "globalRateLimit": {"strategy": "TokenBucket", "maxRequests": 100, "window": "00:01:00", "tokensPerRefill": 10},
        "services": {
            "auth-service": {"isAllowed": True, "rateLimit": {"strategy": "TokenBucket", "maxRequests": 50, "window": "00:01:00", "tokensPerRefill": 5}},
            "analytics-service": {"isAllowed": True},
            "logging-service": {"isAllowed": True},
        },
        "resourcePools": {"worker-threads": {"maxSlots": 4}},
    },
    {
        "id": "cicd-pipeline",
        "name": "CI/CD Pipeline Runner",
        "isEnabled": True,
        "contributesToGlobalLimits": True,
        "exemptFromGlobalLimits": False,
        "globalRateLimit": {"strategy": "TokenBucket", "maxRequests": 200, "window": "00:01:00", "tokensPerRefill": 50},
        "services": {
            "auth-service": {"isAllowed": True},
            "storage-service": {"isAllowed": True},
            "analytics-service": {"isAllowed": True},
            "scheduler-service": {"isAllowed": True},
        },
        "resourcePools": {
            "db-connections": {"maxSlots": 5},
            "worker-threads": {"maxSlots": 6},
            "file-upload-slots": {"maxSlots": 2},
            "sandbox-envs": {"maxSlots": 2},
        },
    },
    {
        "id": "admin-tool",
        "name": "Admin & Monitoring Console",
        "isEnabled": True,
        "contributesToGlobalLimits": False,
        "exemptFromGlobalLimits": True,
        "services": {
            "auth-service": {"isAllowed": True},
            "billing-service": {"isAllowed": True},
            "notification-service": {"isAllowed": True},
            "analytics-service": {"isAllowed": True},
            "storage-service": {"isAllowed": True},
            "audit-service": {"isAllowed": True},
            "config-service": {"isAllowed": True},
        },
        "resourcePools": {"db-connections": {"maxSlots": 2}},
    },
    {
        "id": "data-warehouse",
        "name": "Data Warehouse ETL",
        "isEnabled": True,
        "contributesToGlobalLimits": True,
        "exemptFromGlobalLimits": False,
        "globalRateLimit": {"strategy": "FixedWindow", "maxRequests": 400, "window": "00:01:00"},
        "services": {
            "auth-service": {"isAllowed": True},
            "analytics-service": {"isAllowed": True},
            "storage-service": {"isAllowed": True},
            "queue-service": {"isAllowed": True},
            "logging-service": {"isAllowed": True},
        },
        "resourcePools": {
            "db-connections": {"maxSlots": 10},
            "worker-threads": {"maxSlots": 8},
            "gpu-compute": {"maxSlots": 2},
        },
    },
    {
        "id": "crm-integration",
        "name": "CRM Integration Hub",
        "isEnabled": True,
        "contributesToGlobalLimits": True,
        "exemptFromGlobalLimits": False,
        "globalRateLimit": {"strategy": "FixedWindow", "maxRequests": 150, "window": "00:01:00"},
        "services": {
            "auth-service": {"isAllowed": True},
            "billing-service": {"isAllowed": True},
            "notification-service": {"isAllowed": True},
            "email-service": {"isAllowed": True},
            "webhook-service": {"isAllowed": True},
        },
        "resourcePools": {"db-connections": {"maxSlots": 4}, "api-gateway-slots": {"maxSlots": 10}},
    },
    {
        "id": "marketing-platform",
        "name": "Marketing Automation",
        "isEnabled": True,
        "contributesToGlobalLimits": True,
        "exemptFromGlobalLimits": False,
        "globalRateLimit": {"strategy": "FixedWindow", "maxRequests": 200, "window": "00:01:00"},
        "services": {
            "auth-service": {"isAllowed": True},
            "email-service": {"isAllowed": True},
            "sms-service": {"isAllowed": True},
            "analytics-service": {"isAllowed": True},
            "translate-service": {"isAllowed": True},
        },
        "resourcePools": {"report-workers": {"maxSlots": 3}, "pdf-render-slots": {"maxSlots": 4}},
    },
    {
        "id": "payment-gateway",
        "name": "Payment Processing Gateway",
        "isEnabled": True,
        "contributesToGlobalLimits": True,
        "exemptFromGlobalLimits": False,
        "globalRateLimit": {"strategy": "FixedWindow", "maxRequests": 500, "window": "00:01:00"},
        "services": {
            "auth-service": {"isAllowed": True},
            "billing-service": {"isAllowed": True},
            "notification-service": {"isAllowed": True},
            "audit-service": {"isAllowed": True},
            "webhook-service": {"isAllowed": True},
        },
        "resourcePools": {"db-connections": {"maxSlots": 6}, "api-gateway-slots": {"maxSlots": 20}},
    },
    {
        "id": "support-portal",
        "name": "Customer Support Portal",
        "isEnabled": True,
        "contributesToGlobalLimits": True,
        "exemptFromGlobalLimits": False,
        "globalRateLimit": {"strategy": "FixedWindow", "maxRequests": 120, "window": "00:01:00"},
        "services": {
            "auth-service": {"isAllowed": True},
            "notification-service": {"isAllowed": True},
            "storage-service": {"isAllowed": True},
            "translate-service": {"isAllowed": True},
        },
        "resourcePools": {"db-connections": {"maxSlots": 3}, "file-upload-slots": {"maxSlots": 2}},
    },
    {
        "id": "inventory-mgr",
        "name": "Inventory Manager",
        "isEnabled": True,
        "contributesToGlobalLimits": True,
        "exemptFromGlobalLimits": False,
        "globalRateLimit": {"strategy": "FixedWindow", "maxRequests": 250, "window": "00:01:00"},
        "services": {
            "auth-service": {"isAllowed": True},
            "cache-service": {"isAllowed": True},
            "analytics-service": {"isAllowed": True},
            "queue-service": {"isAllowed": True},
        },
        "resourcePools": {"db-connections": {"maxSlots": 7}, "worker-threads": {"maxSlots": 3}},
    },
    {
        "id": "logistics-api",
        "name": "Logistics & Shipping API",
        "isEnabled": True,
        "contributesToGlobalLimits": True,
        "exemptFromGlobalLimits": False,
        "globalRateLimit": {"strategy": "FixedWindow", "maxRequests": 180, "window": "00:01:00"},
        "services": {
            "auth-service": {"isAllowed": True},
            "geo-service": {"isAllowed": True},
            "notification-service": {"isAllowed": True},
            "webhook-service": {"isAllowed": True},
        },
        "resourcePools": {"api-gateway-slots": {"maxSlots": 8}},
    },
    {
        "id": "ml-training",
        "name": "ML Model Training",
        "isEnabled": True,
        "contributesToGlobalLimits": True,
        "exemptFromGlobalLimits": False,
        "globalRateLimit": {"strategy": "TokenBucket", "maxRequests": 100, "window": "00:01:00", "tokensPerRefill": 20},
        "services": {
            "auth-service": {"isAllowed": True},
            "storage-service": {"isAllowed": True},
            "ml-service": {"isAllowed": True},
            "analytics-service": {"isAllowed": True},
            "logging-service": {"isAllowed": True},
        },
        "resourcePools": {"gpu-compute": {"maxSlots": 4}, "worker-threads": {"maxSlots": 5}, "ml-inference-slots": {"maxSlots": 3}},
    },
    {
        "id": "content-cdn",
        "name": "Content Delivery Network",
        "isEnabled": True,
        "contributesToGlobalLimits": True,
        "exemptFromGlobalLimits": False,
        "globalRateLimit": {"strategy": "FixedWindow", "maxRequests": 800, "window": "00:01:00"},
        "services": {
            "auth-service": {"isAllowed": True},
            "storage-service": {"isAllowed": True},
            "media-service": {"isAllowed": True},
            "cache-service": {"isAllowed": True},
        },
        "resourcePools": {"api-gateway-slots": {"maxSlots": 25}, "video-transcode": {"maxSlots": 3}},
    },
    {
        "id": "partner-wayne",
        "name": "Wayne Enterprises",
        "isEnabled": True,
        "contributesToGlobalLimits": True,
        "exemptFromGlobalLimits": False,
        "globalRateLimit": {"strategy": "FixedWindow", "maxRequests": 80, "window": "00:01:00"},
        "services": {
            "auth-service": {"isAllowed": True},
            "billing-service": {"isAllowed": True},
            "analytics-service": {"isAllowed": True},
        },
        "resourcePools": {"db-connections": {"maxSlots": 2}},
    },
    {
        "id": "partner-stark",
        "name": "Stark Industries",
        "isEnabled": True,
        "contributesToGlobalLimits": True,
        "exemptFromGlobalLimits": False,
        "globalRateLimit": {"strategy": "FixedWindow", "maxRequests": 120, "window": "00:01:00"},
        "services": {
            "auth-service": {"isAllowed": True},
            "billing-service": {"isAllowed": True},
            "storage-service": {"isAllowed": True},
            "ml-service": {"isAllowed": True},
        },
        "resourcePools": {"db-connections": {"maxSlots": 3}, "gpu-compute": {"maxSlots": 1}},
    },
    {
        "id": "partner-umbrella",
        "name": "Umbrella Corp",
        "isEnabled": True,
        "contributesToGlobalLimits": True,
        "exemptFromGlobalLimits": False,
        "globalRateLimit": {"strategy": "FixedWindow", "maxRequests": 90, "window": "00:01:00"},
        "services": {
            "auth-service": {"isAllowed": True},
            "billing-service": {"isAllowed": True},
            "analytics-service": {"isAllowed": True},
            "audit-service": {"isAllowed": True},
        },
        "resourcePools": {"db-connections": {"maxSlots": 2}},
    },
    {
        "id": "hr-system",
        "name": "HR Management System",
        "isEnabled": True,
        "contributesToGlobalLimits": True,
        "exemptFromGlobalLimits": False,
        "globalRateLimit": {"strategy": "FixedWindow", "maxRequests": 100, "window": "00:01:00"},
        "services": {
            "auth-service": {"isAllowed": True},
            "notification-service": {"isAllowed": True},
            "email-service": {"isAllowed": True},
            "pdf-service": {"isAllowed": True},
        },
        "resourcePools": {"db-connections": {"maxSlots": 3}, "pdf-render-slots": {"maxSlots": 5}},
    },
    {
        "id": "compliance-bot",
        "name": "Compliance Checker Bot",
        "isEnabled": True,
        "contributesToGlobalLimits": True,
        "exemptFromGlobalLimits": False,
        "globalRateLimit": {"strategy": "TokenBucket", "maxRequests": 60, "window": "00:01:00", "tokensPerRefill": 10},
        "services": {
            "auth-service": {"isAllowed": True},
            "audit-service": {"isAllowed": True},
            "logging-service": {"isAllowed": True},
            "config-service": {"isAllowed": True},
        },
        "resourcePools": {"db-connections": {"maxSlots": 2}},
    },
    {
        "id": "chatbot-ai",
        "name": "AI Customer Chatbot",
        "isEnabled": True,
        "contributesToGlobalLimits": True,
        "exemptFromGlobalLimits": False,
        "globalRateLimit": {"strategy": "FixedWindow", "maxRequests": 300, "window": "00:01:00"},
        "services": {
            "auth-service": {"isAllowed": True},
            "ml-service": {"isAllowed": True},
            "translate-service": {"isAllowed": True},
            "cache-service": {"isAllowed": True},
            "logging-service": {"isAllowed": True},
        },
        "resourcePools": {"ml-inference-slots": {"maxSlots": 4}, "api-gateway-slots": {"maxSlots": 10}},
    },
    {
        "id": "reporting-svc",
        "name": "Scheduled Report Generator",
        "isEnabled": True,
        "contributesToGlobalLimits": True,
        "exemptFromGlobalLimits": False,
        "globalRateLimit": {"strategy": "FixedWindow", "maxRequests": 80, "window": "00:01:00"},
        "services": {
            "auth-service": {"isAllowed": True},
            "analytics-service": {"isAllowed": True},
            "pdf-service": {"isAllowed": True},
            "email-service": {"isAllowed": True},
            "storage-service": {"isAllowed": True},
        },
        "resourcePools": {"report-workers": {"maxSlots": 6}, "pdf-render-slots": {"maxSlots": 4}, "db-connections": {"maxSlots": 3}},
    },
    {
        "id": "developer-sandbox",
        "name": "Developer Sandbox",
        "isEnabled": True,
        "contributesToGlobalLimits": True,
        "exemptFromGlobalLimits": False,
        "globalRateLimit": {"strategy": "FixedWindow", "maxRequests": 50, "window": "00:01:00"},
        "services": {
            "auth-service": {"isAllowed": True},
            "storage-service": {"isAllowed": True},
            "cache-service": {"isAllowed": True},
        },
        "resourcePools": {"sandbox-envs": {"maxSlots": 3}, "db-connections": {"maxSlots": 2}},
    },
]

CLIENT_BASE_LOAD = {
    "platform-core": 0.62,
    "mobile-app": 0.46,
    "web-dashboard": 0.38,
    "partner-acme": 0.16,
    "partner-globex": 0.04,
    "iot-gateway": 0.34,
    "cicd-pipeline": 0.24,
    "admin-tool": 0.06,
    "data-warehouse": 0.42,
    "crm-integration": 0.25,
    "marketing-platform": 0.31,
    "payment-gateway": 0.43,
    "support-portal": 0.29,
    "inventory-mgr": 0.33,
    "logistics-api": 0.28,
    "ml-training": 0.36,
    "content-cdn": 0.49,
    "partner-wayne": 0.13,
    "partner-stark": 0.19,
    "partner-umbrella": 0.14,
    "hr-system": 0.18,
    "compliance-bot": 0.12,
    "chatbot-ai": 0.32,
    "reporting-svc": 0.26,
    "developer-sandbox": 0.11,
}

TARGET_LOAD_MULTIPLIER = {
    "auth-service": 1.0,
    "billing-service": 0.42,
    "notification-service": 0.58,
    "analytics-service": 0.86,
    "storage-service": 0.64,
    "email-service": 0.38,
    "sms-service": 0.22,
    "cache-service": 0.72,
    "logging-service": 0.92,
    "config-service": 0.21,
    "scheduler-service": 0.24,
    "geo-service": 0.36,
    "media-service": 0.28,
    "pdf-service": 0.18,
    "audit-service": 0.34,
    "webhook-service": 0.41,
    "translate-service": 0.23,
    "ml-service": 0.26,
    "queue-service": 0.55,
}


def _enabled_client_ids(clients: list[dict]) -> list[str]:
    return [client["id"] for client in clients if client.get("isEnabled", True)]


def _disabled_client_ids(clients: list[dict]) -> list[str]:
    return [client["id"] for client in clients if not client.get("isEnabled", True)]


def _client_services(clients: list[dict]) -> dict[str, list[str]]:
    return {
        client["id"]: [
            service_id
            for service_id, service_config in client.get("services", {}).items()
            if service_config.get("isAllowed", True)
        ]
        for client in clients
        if client.get("isEnabled", True)
    }


def _client_pools(clients: list[dict]) -> dict[str, list[str]]:
    return {
        client["id"]: list(client.get("resourcePools", {}).keys())
        for client in clients
        if client.get("isEnabled", True)
    }


def default_history_data_dir() -> Path:
    configured = os.environ.get(SHARED_DATA_SETTINGS["storage_data_dir_env_var"])
    if configured:
        return Path(configured)

    repo_data_directory = SHARED_DATA_SETTINGS["repo_data_directory"]
    if repo_data_directory.exists():
        return repo_data_directory

    return SHARED_DATA_SETTINGS["api_data_directory"]


ENABLED_CLIENT_IDS = _enabled_client_ids(CLIENT_CATALOG)
DISABLED_CLIENT_IDS = _disabled_client_ids(CLIENT_CATALOG)
CLIENT_SERVICE_MAP = _client_services(CLIENT_CATALOG)
CLIENT_POOL_MAP = _client_pools(CLIENT_CATALOG)

GLOBAL_SETTINGS = {
    "api": SHARED_API_SETTINGS,
    "local_runtime": SHARED_LOCAL_RUNTIME_SETTINGS,
    "queries": SHARED_QUERY_SETTINGS,
    "data": SHARED_DATA_SETTINGS,
    "catalogs": {
        "services": SERVICE_CATALOG,
        "service_ids": [service["id"] for service in SERVICE_CATALOG],
        "resource_pools": RESOURCE_POOL_CATALOG,
        "resource_pool_ids": [pool["id"] for pool in RESOURCE_POOL_CATALOG],
        "global_rate_limits": GLOBAL_RATE_LIMIT_CATALOG,
        "clients": CLIENT_CATALOG,
        "enabled_client_ids": ENABLED_CLIENT_IDS,
        "disabled_client_ids": DISABLED_CLIENT_IDS,
        "all_client_ids": ENABLED_CLIENT_IDS + DISABLED_CLIENT_IDS,
        "client_services": CLIENT_SERVICE_MAP,
        "client_pools": CLIENT_POOL_MAP,
        "client_base_load": CLIENT_BASE_LOAD,
        "target_load_multiplier": TARGET_LOAD_MULTIPLIER,
    },
}

SCRIPT_SETTINGS = {
    "traffic_generator": {
        "defaults": {
            "interval_seconds": 2.0,
        },
        "probabilities": {
            "valid_access_combination": 0.85,
            "detailed_read": 0.4,
        },
        "burst": {
            "sizes": [1, 2, 3, 4, 5],
            "weights": [20, 35, 25, 15, 5],
        },
        "actions": {
            "types": ["access_check", "acquire", "release", "read"],
            "weights": [50, 15, 10, 25],
        },
        "timing": {
            "stats_every_iterations": 15,
            "minimum_sleep_seconds": 0.2,
            "sleep_jitter_multiplier": 0.4,
        },
        "client_weights": {
            "platform-core": 5,
            "mobile-app": 4,
            "web-dashboard": 3,
            "content-cdn": 4,
            "payment-gateway": 3,
            "data-warehouse": 3,
            "chatbot-ai": 3,
            "marketing-platform": 2,
            "crm-integration": 2,
            "inventory-mgr": 2,
            "ml-training": 2,
            "logistics-api": 2,
            "support-portal": 2,
            "cicd-pipeline": 2,
            "iot-gateway": 2,
            "admin-tool": 1,
            "reporting-svc": 1,
            "hr-system": 1,
            "partner-acme": 1,
            "partner-wayne": 1,
            "partner-stark": 1,
            "partner-umbrella": 1,
            "compliance-bot": 1,
            "developer-sandbox": 1,
        },
    },
    "performance_baseline": {
        "defaults": {
            "requests_per_day": 1_000_000,
            "duration_seconds": 60,
            "virtual_clients": 100,
            "seed": 42,
            "graph_ranges": "7d,30d,90d",
        },
        "virtual_clients": {
            "heavy_weight_probability": 0.35,
            "heavy_weight_value": 2,
            "default_weight_value": 1,
        },
        "graph": {
            "range_presets": {
                "7d": {"days": 7, "granularity": "Hour"},
                "30d": {"days": 30, "granularity": "Day"},
                "90d": {"days": 90, "granularity": "Day"},
            },
            "monitor_window_seconds": 1800,
        },
        "routing": {
            "prefer_primary_service_every": 3,
            "prefer_primary_pool_every": 2,
        },
        "timing": {
            "seconds_per_day": 86400,
            "minimum_elapsed_seconds": 0.001,
        },
        "metrics": {
            "latency_percentile": 0.95,
        },
        "action_weights": {
            "with_graph": {
                "access": 58,
                "acquire": 15,
                "release": 10,
                "idle_release": 1,
                "dashboard": 9,
                "monitor": 8,
                "graph": 6,
            },
            "without_graph": {
                "access": 58,
                "acquire": 15,
                "release": 10,
                "idle_release": 1,
                "dashboard": 9,
                "monitor": 8,
            },
        },
    },
    "seed_data": {
        "defaults": {
            "history_days": 395,
            "history_seed": 8675309,
        },
        "history": {
            "bucket_rounding_minutes": 5,
            "generation_end_offset_minutes": 5,
            "windows": [
                {"granularity": "FiveMinute", "step": {"minutes": 5}, "max_days": 7},
                {"granularity": "Hour", "step": {"hours": 1}, "max_days": 90},
                {"granularity": "Day", "step": {"days": 1}, "max_days": None},
            ],
            "defaults": {
                "service_cap_per_minute": 120,
                "pool_slots": 10,
                "client_base_load": 0.2,
                "target_load_multiplier": 0.35,
                "pool_global_cap_multiplier": 4,
                "pool_minute_cap_multiplier": 6,
            },
            "business_cycle": {
                "weekend_factor": 0.42,
                "morning_peak_hour": 10.5,
                "morning_peak_divisor": 18,
                "afternoon_peak_multiplier": 0.72,
                "afternoon_peak_hour": 15.5,
                "afternoon_peak_divisor": 10,
                "night_trickle_base": 0.10,
                "night_trickle_amplitude": 0.04,
                "always_on_clients": {
                    "ids": ["iot-gateway", "content-cdn", "chatbot-ai"],
                    "base": 0.62,
                    "amplitude": 0.16,
                    "hour_offset": 5,
                    "weekend_factor": 0.86,
                },
                "batch_clients": {
                    "ids": ["data-warehouse", "cicd-pipeline", "compliance-bot", "reporting-svc"],
                    "base": 0.16,
                    "nightly_batch_multiplier": 0.58,
                    "nightly_batch_hour": 2.0,
                    "nightly_batch_divisor": 5,
                    "morning_peak_multiplier": 0.32,
                    "weekend_factor": 0.72,
                },
                "partner_multiplier": 0.78,
                "partner_weekend_multiplier": 0.62,
                "resource_pool_multiplier": 0.7,
                "seasonal_amplitude": 0.12,
                "seasonal_day_offset": 20,
                "seasonal_period_days": 365,
                "minimum_factor": 0.02,
            },
            "random_multiplier": {
                "minimum": 0.05,
                "maximum": 2.5,
                "lognormal_mean": 0,
                "lognormal_sigma": 0.28,
            },
            "service_bucket": {
                "maximum_utilization": 1.25,
                "spike_probability": 0.018,
                "spike_multiplier_minimum": 0.85,
                "spike_multiplier_maximum": 1.35,
                "denial_probability": 0.012,
                "denial_ratio": 0.04,
            },
            "pool_bucket": {
                "maximum_utilization": 1.15,
                "active_multiplier_minimum": 0.65,
                "active_multiplier_maximum": 1.15,
                "demand_ratio_minimum": 0.08,
                "demand_ratio_maximum": 0.22,
                "full_pool_denial_probability": 0.22,
                "full_pool_denial_divisor": 2,
                "release_multiplier_minimum": 0.65,
                "release_multiplier_maximum": 1.05,
            },
        },
    },
    "download_images": {
        "paths": {
            "download_directory": ".downloaded_images",
            "manifest_file": "manifest.json",
        },
        "defaults": {
            "build_version": "local",
            "package_sources": [],
        },
        "docker": {
            "dependency_images": {
                "jaeger": None,
                "prometheus": None,
                "grafana": None,
                "sdk": "mcr.microsoft.com/dotnet/sdk:10.0",
                "aspnet": "mcr.microsoft.com/dotnet/aspnet:10.0",
                "runtime": "mcr.microsoft.com/dotnet/runtime:10.0",
            },
        },
    },
    "launch_observability_ui": {
        "paths": {
            "generated_directory": ".observability-stack",
            "compose_file": "docker-compose.generated.yml",
            "prometheus_directory": "prometheus",
            "prometheus_file": "prometheus.yml",
            "grafana_directory": "grafana",
            "provisioning_directory": "provisioning",
            "datasources_directory": "datasources",
            "datasources_file": "datasources.yml",
            "dashboards_directory": "dashboards",
            "dashboard_provider_file": "dashboard-provider.yml",
            "dashboard_folder": "clientmanager",
            "dashboard_file": "clientmanager-observability.json",
        },
        "containers": {
            "network": "clientmanager-observability",
            "grafana_container": "clientmanager-grafana",
            "prometheus_container": "clientmanager-prometheus",
            "jaeger_container": "clientmanager-jaeger",
        },
        "defaults": {
            "launcher": "auto",
            "grafana_port": 3000,
            "prometheus_port": 9090,
            "jaeger_port": 16686,
            "otlp_grpc_port": 4317,
            "otlp_http_port": 4318,
            "otlp_endpoint": "http://localhost:4317",
            "scrape_interval": "5s",
            "open_browser": True,
        },
        "docker": {
            "api_fallbacks": ["1.50", "1.49", "1.48", "1.47", "1.46"],
            "collector_otlp_enabled": "true",
            "registry_prefix": None,
            "images": {
                "jaeger": "jaegertracing/all-in-one:latest",
                "prometheus": "prom/prometheus:latest",
                "grafana": "grafana/grafana-oss:latest",
            },
            "image_overrides": {
                "jaeger": None,
                "prometheus": None,
                "grafana": None,
            },
        },
        "grafana": {
            "environment": {
                "GF_AUTH_ANONYMOUS_ENABLED": "true",
                "GF_AUTH_ANONYMOUS_ORG_ROLE": "Admin",
                "GF_AUTH_DISABLE_LOGIN_FORM": "true",
                "GF_USERS_DEFAULT_THEME": "light",
            },
            "dashboard": {
                "refresh": "10s",
                "schema_version": 39,
                "style": "dark",
                "title": "ClientManager Observability",
                "uid": "clientmanager-observability",
                "tags": ["clientmanager", "observability"],
                "time_range": {"from": "now-15m", "to": "now"},
                "trace_search_time_range": {"from": "now-1h", "to": "now"},
            },
        },
        "timeouts": {
            "command_available_seconds": 20,
            "wait_for_http_seconds": 90,
            "container_stop_seconds": 30,
            "network_remove_seconds": 15,
            "http_request_seconds": 5,
            "poll_interval_seconds": 1,
        },
    },
}

CONFIGURATION = {
    "global": GLOBAL_SETTINGS,
    "scripts": SCRIPT_SETTINGS,
}
