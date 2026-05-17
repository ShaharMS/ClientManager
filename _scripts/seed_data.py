"""
Seeds the public ClientManager API with realistic mock data.

Before running this script, start ClientManager.StorageApi first and then ClientManager.Api.

Creates:
    - 20 services
    - 10 resource pools
    - 25 client configurations with varied access patterns
    - Global rate limits for each service and resource pool
    - Historical usage snapshots for dashboards and statistics queries

Usage:
    python seed_data.py [--base-url http://localhost:5062]
"""

import argparse
import math
import json
import os
import random
import sys
from collections import defaultdict
from datetime import datetime, timedelta, timezone
from pathlib import Path
import urllib.request
import urllib.error

BASE_URL = "http://localhost:5062"
API_PREFIX = "/api/v1"
DEFAULT_HISTORY_DAYS = 395
DEFAULT_HISTORY_SEED = 8675309
USAGE_SNAPSHOTS_COLLECTION = "UsageSnapshots"

# ── Services ──────────────────────────────────────────────────────────────

SERVICES = [
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

# ── Resource Pools ────────────────────────────────────────────────────────

RESOURCE_POOLS = [
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

# ── Global Rate Limits ────────────────────────────────────────────────────

GLOBAL_RATE_LIMITS = [
    # Service limits
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
    # Resource pool limits
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

# ── Client Configurations ─────────────────────────────────────────────────

CLIENTS = [
    # ── Original 8 clients (kept for continuity) ──────────────────────────
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
    # ── New clients (9–25) ────────────────────────────────────────────────
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


# ── Historical Usage Snapshots ─────────────────────────────────────────────

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


def default_history_data_dir():
    configured = os.environ.get("CLIENTMANAGER_STORAGE_DATA_DIR")
    if configured:
        return Path(configured)

    repo_root = Path(__file__).resolve().parents[1]
    root_data_dir = repo_root / "data"
    if root_data_dir.exists():
        return root_data_dir

    return repo_root / "ClientManager.StorageApi" / "data"


def load_json_collection(data_dir: Path, collection: str):
    path = data_dir / f"{collection}.json"
    if not path.exists():
        return {}

    with path.open("r", encoding="utf-8-sig") as handle:
        return json.load(handle)


def save_json_collection(data_dir: Path, collection: str, documents):
    data_dir.mkdir(parents=True, exist_ok=True)
    path = data_dir / f"{collection}.json"
    temp_path = path.with_suffix(".json.tmp")

    with temp_path.open("w", encoding="utf-8") as handle:
        json.dump(documents, handle, indent=2)
        handle.write("\n")

    os.replace(temp_path, path)


def utc_now_rounded_to_five_minutes():
    now = datetime.now(timezone.utc).replace(second=0, microsecond=0)
    return now - timedelta(minutes=now.minute % 5)


def iso_z(value: datetime):
    return value.astimezone(timezone.utc).replace(tzinfo=None).isoformat(timespec="seconds") + "Z"


def segment_start(timestamp: datetime, granularity: str):
    timestamp = timestamp.astimezone(timezone.utc)
    if granularity == "Second":
        return timestamp.replace(minute=0, second=0, microsecond=0)
    if granularity == "FiveMinute":
        return timestamp.replace(hour=0, minute=0, second=0, microsecond=0)
    if granularity == "Hour":
        monday = timestamp.date() - timedelta(days=timestamp.weekday())
        return datetime(monday.year, monday.month, monday.day, tzinfo=timezone.utc)
    if granularity == "Day":
        return datetime(timestamp.year, timestamp.month, 1, tzinfo=timezone.utc)
    raise ValueError(f"Unsupported granularity: {granularity}")


def history_windows(history_days: int, end: datetime):
    days = max(1, history_days)
    windows = [
        ("FiveMinute", timedelta(minutes=5), min(days, 7)),
        ("Hour", timedelta(hours=1), min(days, 90)),
        ("Day", timedelta(days=1), days),
    ]

    for granularity, step, window_days in windows:
        start = end - timedelta(days=window_days)
        if granularity == "FiveMinute":
            start = start.replace(minute=start.minute - (start.minute % 5), second=0, microsecond=0)
        elif granularity == "Hour":
            start = start.replace(minute=0, second=0, microsecond=0)
        else:
            start = start.replace(hour=0, minute=0, second=0, microsecond=0)

        current = start
        while current < end:
            yield granularity, step, current
            current += step


def max_requests_per_minute_by_target():
    return {limit["targetId"]: limit["maxRequests"] for limit in GLOBAL_RATE_LIMITS}


def service_cap_per_minute(client, service_id, global_caps):
    caps = []

    if not client.get("exemptFromGlobalLimits"):
        caps.append(global_caps.get(service_id, 120))

    client_global = client.get("globalRateLimit")
    if client_global:
        caps.append(client_global["maxRequests"])

    service_config = client.get("services", {}).get(service_id, {})
    service_limit = service_config.get("rateLimit")
    if service_limit:
        caps.append(service_limit["maxRequests"])

    return max(1, min(caps) if caps else global_caps.get(service_id, 120))


def resource_pool_cap(client, pool_id):
    pool = next((item for item in RESOURCE_POOLS if item["id"] == pool_id), None)
    pool_slots = pool["maxSlots"] if pool else 10
    client_slots = client.get("resourcePools", {}).get(pool_id, {}).get("maxSlots", pool_slots)
    return max(1, min(pool_slots, client_slots))


def client_target_pairs():
    for client in CLIENTS:
        if not client.get("isEnabled", True):
            continue

        for service_id, config in client.get("services", {}).items():
            if config.get("isAllowed", True):
                yield client, "Service", service_id

        for pool_id in client.get("resourcePools", {}):
            yield client, "ResourcePool", pool_id


def business_cycle_factor(timestamp: datetime, client_id: str, target_type: str):
    hour = timestamp.hour + timestamp.minute / 60
    weekday = timestamp.weekday()
    weekend_factor = 0.42 if weekday >= 5 else 1.0

    morning_peak = math.exp(-((hour - 10.5) ** 2) / 18)
    afternoon_peak = 0.72 * math.exp(-((hour - 15.5) ** 2) / 10)
    night_trickle = 0.10 + 0.04 * math.cos((hour / 24) * 2 * math.pi)
    human_factor = night_trickle + morning_peak + afternoon_peak

    if client_id in {"iot-gateway", "content-cdn", "chatbot-ai"}:
        human_factor = 0.62 + 0.16 * math.sin(((hour - 5) / 24) * 2 * math.pi)
        weekend_factor = 0.86
    elif client_id in {"data-warehouse", "cicd-pipeline", "compliance-bot", "reporting-svc"}:
        nightly_batch = 0.58 * math.exp(-((hour - 2.0) ** 2) / 5)
        human_factor = 0.16 + nightly_batch + 0.32 * morning_peak
        weekend_factor = 0.72
    elif client_id.startswith("partner-"):
        human_factor *= 0.78
        weekend_factor *= 0.62

    if target_type == "ResourcePool":
        human_factor *= 0.7

    day_of_year = timestamp.timetuple().tm_yday
    seasonal = 1.0 + 0.12 * math.sin(((day_of_year - 20) / 365) * 2 * math.pi)
    return max(0.02, human_factor * weekend_factor * seasonal)


def random_multiplier(rng: random.Random):
    return max(0.05, min(2.5, rng.lognormvariate(0, 0.28)))


def generated_service_bucket(rng, client, target_id, timestamp, step, global_caps):
    cap_per_minute = service_cap_per_minute(client, target_id, global_caps)
    minutes = max(1, int(step.total_seconds() // 60))
    bucket_cap = cap_per_minute * minutes
    base_load = CLIENT_BASE_LOAD.get(client["id"], 0.2)
    target_load = TARGET_LOAD_MULTIPLIER.get(target_id, 0.35)
    cycle = business_cycle_factor(timestamp, client["id"], "Service")
    utilization = min(1.25, base_load * target_load * cycle * random_multiplier(rng))
    demand = int(bucket_cap * utilization)

    if rng.random() < 0.018:
        demand = int(bucket_cap * rng.uniform(0.85, 1.35))

    granted = min(bucket_cap, max(0, demand))
    denied = max(0, demand - bucket_cap)

    if granted > 0 and rng.random() < 0.012:
        denied += rng.randint(1, max(1, int(granted * 0.04)))

    return {
        "Timestamp": iso_z(timestamp),
        "GrantedCount": granted,
        "DeniedCount": denied,
        "ReleasedCount": 0,
        "ActiveCount": 0,
    }


def generated_pool_bucket(rng, client, target_id, timestamp, step, global_caps):
    slots = resource_pool_cap(client, target_id)
    minute_cap = min(global_caps.get(target_id, slots * 4), slots * 6)
    minutes = max(1, int(step.total_seconds() // 60))
    bucket_cap = max(slots, minute_cap * minutes)
    base_load = CLIENT_BASE_LOAD.get(client["id"], 0.2)
    cycle = business_cycle_factor(timestamp, client["id"], "ResourcePool")
    utilization = min(1.15, base_load * cycle * random_multiplier(rng))

    active = min(slots, max(0, int(round(slots * utilization * rng.uniform(0.65, 1.15)))))
    demand = int(bucket_cap * utilization * rng.uniform(0.08, 0.22))
    granted = min(bucket_cap, max(0, demand))
    denied = max(0, demand - bucket_cap)

    if active >= slots and rng.random() < 0.22:
        denied += rng.randint(1, max(1, slots // 2))

    released = min(granted + active, max(0, int(granted * rng.uniform(0.65, 1.05))))

    return {
        "Timestamp": iso_z(timestamp),
        "GrantedCount": granted,
        "DeniedCount": denied,
        "ReleasedCount": released,
        "ActiveCount": active,
    }


def build_usage_snapshots(history_days: int, seed: int):
    rng = random.Random(seed)
    end = utc_now_rounded_to_five_minutes() - timedelta(minutes=5)
    global_caps = max_requests_per_minute_by_target()
    snapshots = defaultdict(list)

    pairs = list(client_target_pairs())
    for granularity, step, timestamp in history_windows(history_days, end):
        for client, target_type, target_id in pairs:
            if target_type == "Service":
                bucket = generated_service_bucket(rng, client, target_id, timestamp, step, global_caps)
            else:
                bucket = generated_pool_bucket(rng, client, target_id, timestamp, step, global_caps)

            if bucket["GrantedCount"] == 0 and bucket["DeniedCount"] == 0 and bucket["ActiveCount"] == 0:
                continue

            segment = segment_start(timestamp, granularity)
            snapshot_id = f"{client['id']}:{target_type}:{target_id}:{granularity}:{segment:%Y%m%d%H}"
            snapshots[(snapshot_id, client["id"], target_type, target_id, granularity, segment)].append(bucket)

    return {
        snapshot_id: {
            "Id": snapshot_id,
            "ClientId": client_id,
            "TargetId": target_id,
            "TargetType": target_type,
            "Granularity": granularity,
            "SegmentStart": iso_z(segment),
            "Buckets": sorted(buckets, key=lambda bucket: bucket["Timestamp"]),
        }
        for (snapshot_id, client_id, target_type, target_id, granularity, segment), buckets in snapshots.items()
    }


def merge_usage_snapshots(existing, generated, replace_history: bool):
    if replace_history:
        existing = {
            key: value
            for key, value in existing.items()
            if value.get("Granularity") not in {"FiveMinute", "Hour", "Day"}
        }

    for snapshot_id, snapshot in generated.items():
        if snapshot_id not in existing:
            existing[snapshot_id] = snapshot
            continue

        current = existing[snapshot_id]
        buckets = {bucket["Timestamp"]: bucket for bucket in current.get("Buckets", [])}
        buckets.update({bucket["Timestamp"]: bucket for bucket in snapshot["Buckets"]})
        current.update(snapshot)
        current["Buckets"] = [buckets[key] for key in sorted(buckets)]

    return existing


def seed_historical_usage(data_dir: Path, history_days: int, seed: int, replace_history: bool):
    print(f"\nHistorical Usage ({history_days} days):")
    existing = load_json_collection(data_dir, USAGE_SNAPSHOTS_COLLECTION)
    generated = build_usage_snapshots(history_days, seed)
    merged = merge_usage_snapshots(existing, generated, replace_history)
    save_json_collection(data_dir, USAGE_SNAPSHOTS_COLLECTION, merged)

    bucket_count = sum(len(snapshot.get("Buckets", [])) for snapshot in generated.values())
    action = "replaced" if replace_history else "merged"
    print(f"  [{action}] {len(generated):,} usage snapshot documents")
    print(f"  [wrote] {bucket_count:,} historical buckets to {data_dir / (USAGE_SNAPSHOTS_COLLECTION + '.json')}")
    print("  [note] Restart StorageApi after file-backed historical seeding so in-memory caches/providers reload it.")


def api(method: str, path: str, body=None):
    """Make an API call. Returns (status_code, response_body_or_None)."""
    url = f"{BASE_URL.rstrip('/')}/{path.lstrip('/')}"
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(url, data=data, method=method)
    req.add_header("Content-Type", "application/json")
    try:
        with urllib.request.urlopen(req) as resp:
            payload = resp.read()
            if not payload:
                return resp.status, None

            try:
                return resp.status, json.loads(payload)
            except json.JSONDecodeError:
                return resp.status, payload.decode(errors="ignore")
    except urllib.error.HTTPError as e:
        payload = e.read()
        if not payload:
            return e.code, None

        try:
            return e.code, json.loads(payload)
        except json.JSONDecodeError:
            return e.code, payload.decode(errors="ignore")


def verify_public_api():
    status, response = api("GET", f"{API_PREFIX}/statistics/overview")
    if status != 200:
        raise RuntimeError(
            f"Expected the public ClientManager API at {BASE_URL}, but "
            f"GET {API_PREFIX}/statistics/overview returned HTTP {status}: {response}. "
            "Start ClientManager.StorageApi first, then ClientManager.Api."
        )


def seed_list(label: str, endpoint: str, items: list):
    for item in items:
        existing_status, existing_response = api("GET", f"{endpoint}/{item['id']}")
        if existing_status == 200:
            print(f"  [exists] {label} {item['id']}")
            continue

        if existing_status != 404:
            raise RuntimeError(
                f"Failed to check {label} {item['id']} via GET {endpoint}/{item['id']}: "
                f"HTTP {existing_status} {existing_response}"
            )

        status, response = api("POST", endpoint, item)
        if status == 201:
            tag = "created"
        elif status == 409:
            tag = "exists"
        else:
            raise RuntimeError(
                f"Failed to seed {label} {item['id']} via POST {endpoint}: "
                f"HTTP {status} {response}"
            )

        print(f"  [{tag}] {label} {item['id']}")


def main():
    global BASE_URL

    parser = argparse.ArgumentParser(description="Seed the public ClientManager API with mock data")
    parser.add_argument("--base-url", default=BASE_URL, help="Public API base URL (start StorageApi first)")
    parser.add_argument(
        "--skip-history",
        action="store_true",
        help="Only seed catalog data through the API; do not write historical usage snapshots",
    )
    parser.add_argument(
        "--history-days",
        type=int,
        default=DEFAULT_HISTORY_DAYS,
        help=f"Number of days of historical daily usage to generate (default: {DEFAULT_HISTORY_DAYS})",
    )
    parser.add_argument(
        "--history-seed",
        type=int,
        default=DEFAULT_HISTORY_SEED,
        help="Random seed for deterministic historical usage generation",
    )
    parser.add_argument(
        "--history-data-dir",
        type=Path,
        default=default_history_data_dir(),
        help="Directory containing UsageSnapshots.json for the JSON file storage provider",
    )
    parser.add_argument(
        "--replace-history",
        action="store_true",
        help="Replace existing FiveMinute/Hour/Day usage snapshots instead of merging generated buckets",
    )
    args = parser.parse_args()

    if args.history_days < 1:
        parser.error("--history-days must be at least 1")

    BASE_URL = args.base_url.rstrip("/")

    print(f"Seeding API at {BASE_URL}\n")
    verify_public_api()

    # Order matters: services & pools first, then clients reference them
    print("Services:")
    seed_list("Service", f"{API_PREFIX}/services", SERVICES)

    print("\nResource Pools:")
    seed_list("Pool", f"{API_PREFIX}/resource-pools", RESOURCE_POOLS)

    print("\nGlobal Rate Limits:")
    seed_list("GRL", f"{API_PREFIX}/global-rate-limits", GLOBAL_RATE_LIMITS)

    print("\nClients:")
    seed_list("Client", f"{API_PREFIX}/clients", CLIENTS)

    if not args.skip_history:
        seed_historical_usage(
            args.history_data_dir,
            args.history_days,
            args.history_seed,
            args.replace_history)

    print("\n✓ Seeding complete.")


if __name__ == "__main__":
    main()
