CREATE TABLE pot_cycles (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    instance_key TEXT NOT NULL,
    territory_id INTEGER NOT NULL,
    datacenter_id INTEGER NOT NULL,
    pot_fate_id INTEGER NOT NULL,
    spawn_at_unix INTEGER NOT NULL,
    installation_hash TEXT NOT NULL,
    plugin_version TEXT NOT NULL,
    observed_at_utc TEXT NOT NULL,
    created_at_utc TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
);

CREATE INDEX idx_pot_cycles_instance
ON pot_cycles(instance_key, spawn_at_unix DESC);
