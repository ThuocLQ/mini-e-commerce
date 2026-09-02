# Alert Routing And Retention Policy

## Scope

This policy applies to the local production-like Compose profile. It defines an executable first-line route, ownership labels, and the retention period of the monitoring data. It does not claim an enterprise paging service is configured.

## Routing

- Prometheus evaluates `docker/observability/prometheus-rules.yml` every 15 seconds.
- Alertmanager groups by `alertname`, `service`, and `severity` and sends both firing and resolved notifications to `operations@microshop.test` through Mailpit.
- `owner=platform` is responsible for collector, Kafka/projection, and cross-service runtime alerts.
- `owner=procurement` is responsible for SupplierService and the supplier-to-inventory receipt path.
- `critical` alerts require investigation immediately; `warning` alerts are triaged in the current operations window.

For a real deployment, replace the Mailpit receiver through an environment-specific Alertmanager configuration or secret-backed notification integration. The routed receiver must identify an owned mailbox, on-call system, or incident channel. Do not expose Alertmanager publicly.

## Retention

- Prometheus retains local metrics for 15 days (`--storage.tsdb.retention.time=15d`).
- Alertmanager retains notification state for 120 hours (`--data.retention=120h`).
- Grafana and Prometheus data are stored in named volumes; they survive normal container recreation but are not a substitute for off-host backups.
- Application logs remain container-runtime logs in this profile. Long-term searchable log retention requires the later centralized logging deployment and an explicit data-retention approval.

## Verification

1. Start observability with `scripts/local-prod-observability-up.ps1`.
2. Open Alertmanager only on its loopback address, `http://127.0.0.1:9093`.
3. Stop a scrape target such as SupplierService and confirm the alert is visible after its `for` duration and that Mailpit receives the notification.
4. Restore the target and confirm a resolved notification arrives.