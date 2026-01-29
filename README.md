# ingress-nginx-retirement-auditor

A cross-platform CLI tool to audit Kubernetes clusters and Git repositories for [ingress-nginx](https://kubernetes.github.io/ingress-nginx/) usage, producing actionable migration reports.

[![CI](https://github.com/relusion/ingress-nginx-retirement-auditor/actions/workflows/ci.yml/badge.svg)](https://github.com/relusion/ingress-nginx-retirement-auditor/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

## Overview

With the [retirement of ingress-nginx](https://kubernetes.io/blog/2023/10/25/introducing-ingress2gateway/), organizations need to plan their migration to alternative solutions like Gateway API, Envoy Gateway, Istio, or Traefik. This tool helps you:

- **Discover** all Ingress resources using ingress-nginx in your clusters or repositories
- **Assess risk** by identifying high-complexity configurations (snippets, rewrites, auth)
- **Prioritize** migration efforts with severity-based findings and risk scores
- **Generate reports** in Markdown and JSON formats for documentation and CI/CD integration

## Installation

### As a .NET Global Tool

```bash
dotnet tool install --global ingress-nginx-auditor
```

### From Source

```bash
git clone https://github.com/relusion/ingress-nginx-retirement-auditor.git
cd ingress-nginx-retirement-auditor
dotnet build
```

### Pre-built Binaries

Download from [Releases](https://github.com/relusion/ingress-nginx-retirement-auditor/releases) for:
- Linux x64
- macOS x64 / ARM64
- Windows x64

## Quick Start

### Scan a Kubernetes Cluster

```bash
# Scan all namespaces in current context
ingress-nginx-auditor scan cluster

# Scan specific context and namespaces
ingress-nginx-auditor scan cluster --context prod-cluster -n production,staging

# Fail CI if high-severity issues found
ingress-nginx-auditor scan cluster --fail-on high
```

### Scan a Repository

```bash
# Scan YAML files in a directory
ingress-nginx-auditor scan repo --path ./k8s/manifests

# Scan from stdin (for piped input)
cat my-ingress.yaml | ingress-nginx-auditor scan repo --stdin

# Custom glob patterns
ingress-nginx-auditor scan repo -p ./manifests -i "**/*.yaml" -e "**/test/**"
```

### List Detection Rules

```bash
# Show all rules
ingress-nginx-auditor rules list

# Filter by category
ingress-nginx-auditor rules list --category risk

# Get details for a specific rule
ingress-nginx-auditor rules explain RISK-SNIPPET-001
```

## Detection Rules

| Rule ID | Severity | Description |
|---------|----------|-------------|
| `DET-NGINX-CLASS-001` | Info | Detects `ingressClassName: nginx` |
| `DET-NGINX-ANNOT-PREFIX-001` | Info | Detects `nginx.ingress.kubernetes.io/*` annotations |
| `DET-NGINX-CTRL-001` | Info | Detects ingress-nginx controller deployments |
| `RISK-SNIPPET-001` | High/Critical | Detects snippet annotations (configuration, server, location) |
| `RISK-REGEX-001` | Medium | Detects `use-regex: "true"` annotation |
| `RISK-REWRITE-001` | Medium | Detects `rewrite-target` annotation |
| `RISK-AUTH-001` | Medium | Detects external auth annotations |
| `RISK-TLS-REDIRECT-001` | Low | Detects SSL redirect annotations |

## Exit Codes

| Code | Meaning |
|------|---------|
| 0 | Success - no policy violations |
| 1 | Policy violation - findings exceed threshold |
| 2 | Invalid configuration |
| 3 | Partial failure - some resources couldn't be scanned |
| 4 | Fatal error |

## Configuration

Create `auditor.config.yaml` in your project root:

```yaml
# Custom ingress class names to detect
ingressClassNames:
  - nginx
  - nginx-internal

# Policy settings
policy:
  failOn: high  # info, low, medium, high, critical

# Rule configuration
rules:
  disabled:
    - RISK-TLS-REDIRECT-001  # Ignore TLS redirect findings
  severityOverrides:
    RISK-REGEX-001: low  # Downgrade regex findings

# Output settings
output:
  formats: [md, json]
  showAnnotationValues: false  # Redact sensitive values
```

Use with: `--config ./auditor.config.yaml`

## CI/CD Integration

### GitHub Actions

```yaml
- name: Audit ingress-nginx usage
  run: |
    dotnet tool install --global ingress-nginx-auditor
    ingress-nginx-auditor scan repo --path ./k8s --fail-on high
```

### GitLab CI

```yaml
audit-ingress:
  image: mcr.microsoft.com/dotnet/sdk:8.0
  script:
    - dotnet tool install --global ingress-nginx-auditor
    - export PATH="$PATH:$HOME/.dotnet/tools"
    - ingress-nginx-auditor scan repo --path ./k8s --fail-on high
```

## Sample Output

### Console Summary

```
Policy Status: FAIL

┌───────────────────┬───────┐
│ Metric            │ Value │
├───────────────────┼───────┤
│ Resources Scanned │ 15    │
│ Ingresses Scanned │ 12    │
│ NGINX-Dependent   │ 8     │
│ Total Findings    │ 14    │
└───────────────────┴───────┘

┌──────────┬───────┐
│ Severity │ Count │
├──────────┼───────┤
│ Critical │ 1     │
│ High     │ 2     │
│ Medium   │ 5     │
│ Info     │ 6     │
└──────────┴───────┘
```

### Generated Reports

- `report.md` - Human-readable Markdown report with findings, recommendations, and namespace breakdown
- `report.json` - Machine-readable JSON for programmatic processing

## RBAC Requirements

For cluster scanning, the tool needs read access to Ingress resources:

```yaml
apiVersion: rbac.authorization.k8s.io/v1
kind: ClusterRole
metadata:
  name: ingress-nginx-auditor
rules:
- apiGroups: ["networking.k8s.io"]
  resources: ["ingresses"]
  verbs: ["get", "list"]
- apiGroups: ["apps"]
  resources: ["deployments", "daemonsets"]
  verbs: ["get", "list"]
```

## Development

```bash
# Build
dotnet build

# Run tests
dotnet test

# Run locally
dotnet run --project src/IngressNginxAuditor -- scan repo --path ./examples/manifests
```

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
