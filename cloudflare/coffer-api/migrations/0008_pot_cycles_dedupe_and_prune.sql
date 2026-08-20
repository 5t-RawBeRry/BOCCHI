-- Speeds up “already have this instance spawn?” checks (many clients report the same anchor).
CREATE INDEX IF NOT EXISTS idx_pot_cycles_instance_spawn
ON pot_cycles(instance_key, pot_fate_id, spawn_at_unix);

-- Speeds up age-based pruning.
CREATE INDEX IF NOT EXISTS idx_pot_cycles_spawn_at
ON pot_cycles(spawn_at_unix);
