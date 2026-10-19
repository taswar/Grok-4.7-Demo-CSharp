## Assumptions
- ~10K concurrent users are mostly read-heavy (availability checks); stock writes are a smaller fraction and must be strongly consistent per SKU+location.
- SQL Server remains the system of record; the team will not adopt a new primary datastore.
- “Visible within two seconds” means committed stock changes appear on other reads within 2s, not a hard real-time guarantee under partition.
- SAP is an external system of record for master data and selected business documents, reached only over its REST APIs.
- Six months covers a production-capable core (stock, audit, SAP sync), not every legacy report.

## Architecture trade-offs
- Stay on SQL Server (row versioning, transactional stock updates) instead of NoSQL: lower delivery risk given limited NoSQL experience; scale reads with caching and read replicas rather than a new write model.
- Synchronous update in SQL Server, then async publish (outbox + queue) for caches, search, and SAP notifications. Avoid dual-write to SAP in the same transaction.
- Per-location inventory partitions (or filtered indexes) keep hot SKUs from locking the whole catalog; do not introduce geo-distributed multi-master in v1.
- Short-TTL cache or replica reads for availability; reservations and adjustments always hit primary so oversell is prevented.

## Data model
- Core: `Location`, `Sku`, `InventoryBalance` (location_id, sku_id, on_hand, reserved, version), `InventoryMovement` (immutable ledger: type, qty, ref, actor, timestamp).
- Balance is derived-enforced: movements are the audit source; balance is the current projection updated in the same transaction.
- `Outbox` for integration events; `AuditEvent` (who, what, before/after, correlation id) append-only, retained per policy.
- Idempotency key on movements and SAP callbacks. Optimistic concurrency via row version on balance.

## APIs
- Internal REST/JSON: get availability, reserve, release, adjust, transfer between locations, movement history. All writes idempotent.
- Read APIs may use replica/cache; write APIs return the new balance and version.
- SAP adapter: inbound item/location master and outbound movement or reservation callbacks, with retry and dead-letter. Map SAP ids explicitly; do not leak SAP payloads into core tables.
- Authn on every call; location-scoped authz. Correlation id required.

## Reliability
- Single-region primary plus readable secondary; RPO/RTO set by existing SQL ops (not re-specified here).
- Outbox poller with at-least-once delivery; consumers dedupe on event id.
- Reservation TTL and a reconciler to release abandoned holds. SAP failures queue and retry; they do not roll back a committed local movement.
- SLOs: write commit p99 within normal OLTP bounds; cross-node visibility target ≤2s via replica lag alerts and cache invalidation. No unverified throughput claims.

## Security
- TLS, secrets in a vault, least-privilege DB and SAP credentials. Audit is immutable to app roles.
- PII minimized (actor id, not full profile). SAP tokens rotated; inbound webhooks signed or mutually authenticated.
- Backups encrypted; audit access logged. Pen test before broad rollout.

## Rollout phases
1. Months 1–2: schema, movement ledger, balance updates, audit, internal APIs, SQL migration of locations/SKUs/balances.
2. Months 3–4: SAP master sync, outbound events, idempotent retries, cache/replica read path, 2s visibility checks.
3. Months 5–6: reservation/transfer, reconciler, pilot locations, load test against the 10K-concurrency target, then phased cutover. Legacy write path frozen after pilot sign-off.

## Open questions
- Peak write rate and reservation conflict rate per hot SKU (unknown; drives locking and partition design).
- Is SAP bidirectional for stock, or master-data only? What is SAP’s rate limit and idempotency contract?
- Required audit retention, and whether auditors need legal hold/export.
- Multi-region read or active-active later, or single region for the six-month scope?
- Acceptable oversell policy if SAP confirm lags the local commit.