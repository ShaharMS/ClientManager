from __future__ import annotations

from pathlib import Path

SCRIPT_DIR = Path(__file__).resolve().parent
REPO_ROOT = SCRIPT_DIR.parent

SERVICES = [
    {"id": "auth-service", "name": "Authentication Service", "isEnabled": True},
    {"id": "billing-service", "name": "Billing & Payments", "isEnabled": True},
    {"id": "analytics-service", "name": "Analytics Pipeline", "isEnabled": True},
    {"id": "cache-service", "name": "Distributed Cache", "isEnabled": True},
    {"id": "audit-service", "name": "Audit Trail", "isEnabled": True},
    {"id": "search-service", "name": "Search & Indexing", "isEnabled": False},
]


def policy(strategy: str, max_requests: int, tokens_per_refill: int | None = None) -> dict:
    value = {
        "strategy": strategy,
        "maxRequests": max_requests,
        "window": "00:01:00",
    }
    if tokens_per_refill is not None:
        value["tokensPerRefill"] = tokens_per_refill
    return value


GLOBAL_RATE_LIMITS = [
    {"id": "auth-service", "policy": policy("FixedWindow", 100_000)},
    {"id": "billing-service", "policy": policy("ApproximateSlidingWindow", 100_000)},
    {"id": "analytics-service", "policy": policy("TokenBucket", 100_000, 100_000)},
    {"id": "cache-service", "policy": policy("FixedWindow", 1_000_000)},
    {"id": "audit-service", "policy": policy("TokenBucket", 100_000, 100_000)},
]

CLIENTS = [
    {
        "id": "platform-core",
        "name": "Platform Core",
        "isEnabled": True,
        "contributesToGlobalLimits": True,
        "exemptFromGlobalLimits": False,
        "globalRateLimit": policy("FixedWindow", 1_000_000),
        "services": {
            "auth-service": {"isAllowed": True},
            "billing-service": {"isAllowed": True},
            "analytics-service": {"isAllowed": True},
            "cache-service": {
                "isAllowed": True,
                "rateLimit": policy("FixedWindow", 1_000_000),
            },
            "audit-service": {"isAllowed": True},
        },
    },
    {
        "id": "mobile-app",
        "name": "Mobile App",
        "isEnabled": True,
        "services": {
            "auth-service": {
                "isAllowed": True,
                "rateLimit": policy("FixedWindow", 1_000),
            },
            "billing-service": {"isAllowed": True},
            "analytics-service": {"isAllowed": True},
        },
    },
    {
        "id": "web-dashboard",
        "name": "Web Dashboard",
        "isEnabled": True,
        "services": {
            "auth-service": {"isAllowed": True},
            "analytics-service": {"isAllowed": True},
            "cache-service": {"isAllowed": True},
        },
    },
    {
        "id": "compliance-bot",
        "name": "Compliance Bot",
        "isEnabled": True,
        "services": {
            "auth-service": {"isAllowed": True},
            "audit-service": {
                "isAllowed": True,
                "rateLimit": policy("TokenBucket", 500, 100),
            },
        },
    },
    {
        "id": "disabled-client",
        "name": "Disabled Client",
        "isEnabled": False,
        "services": {"auth-service": {"isAllowed": True}},
    },
]

ENABLED_CLIENTS = [client for client in CLIENTS if client["isEnabled"]]
ENABLED_CLIENT_IDS = [client["id"] for client in ENABLED_CLIENTS]
DISABLED_CLIENT_IDS = [client["id"] for client in CLIENTS if not client["isEnabled"]]
CLIENT_SERVICE_MAP = {
    client["id"]: [
        service_id
        for service_id, settings in client["services"].items()
        if settings.get("isAllowed", True)
    ]
    for client in ENABLED_CLIENTS
}

GLOBAL_SETTINGS = {
    "api": {
        "base_url": "http://localhost:5062",
        "prefix": "api/v1",
        "prefix_with_leading_slash": "/api/v1",
    },
    "local_runtime": {
        "docker_host": "host.docker.internal",
        "api_port": 5062,
    },
    "queries": {
        "search_body": {"take": 100},
        "client_summaries_page_size": 100,
    },
    "catalogs": {
        "services": SERVICES,
        "service_ids": [service["id"] for service in SERVICES],
        "global_rate_limits": GLOBAL_RATE_LIMITS,
        "clients": CLIENTS,
        "enabled_client_ids": ENABLED_CLIENT_IDS,
        "disabled_client_ids": DISABLED_CLIENT_IDS,
        "all_client_ids": ENABLED_CLIENT_IDS + DISABLED_CLIENT_IDS,
        "client_services": CLIENT_SERVICE_MAP,
    },
}

SCRIPT_SETTINGS = {
    "traffic_generator": {
        "defaults": {"interval_seconds": 2.0},
        "probabilities": {
            "valid_access_combination": 0.85,
            "detailed_read": 0.4,
        },
        "burst": {
            "sizes": [1, 2, 3, 4, 5],
            "weights": [20, 35, 25, 15, 5],
        },
        "actions": {
            "types": ["access_check", "read"],
            "weights": [70, 30],
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
            "compliance-bot": 1,
        },
    },
    "performance_baseline": {
        "defaults": {
            "requests_per_day": 1_000_000,
            "duration_seconds": 60,
            "virtual_clients": 100,
            "seed": 42,
        },
        "virtual_clients": {
            "heavy_weight_probability": 0.35,
            "heavy_weight_value": 2,
            "default_weight_value": 1,
        },
        "timing": {
            "seconds_per_day": 86_400,
            "minimum_elapsed_seconds": 0.001,
        },
        "metrics": {"latency_percentile": 0.95},
        "action_weights": {"access": 85, "overview": 15},
    },
    "access_load": {
        "defaults": {
            "target_rpm": 18_000,
            "duration_seconds": 60,
            "concurrency": 64,
            "client_id": "platform-core",
            "service_id": "cache-service",
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
