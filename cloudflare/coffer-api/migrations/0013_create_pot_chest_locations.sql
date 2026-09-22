CREATE TABLE pot_chest_observations (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    territory_id INTEGER NOT NULL,
    pot_fate_id INTEGER NOT NULL,
    is_reroll INTEGER NOT NULL DEFAULT 0,
    world_x REAL NOT NULL,
    world_y REAL NOT NULL,
    world_z REAL NOT NULL,
    installation_hash TEXT NOT NULL,
    plugin_version TEXT NOT NULL,
    observed_at_utc TEXT NOT NULL,
    received_at_utc TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    processed INTEGER NOT NULL DEFAULT 0
);

CREATE INDEX idx_pot_chest_observations_pending
ON pot_chest_observations(processed, id);

CREATE INDEX idx_pot_chest_observations_near_dupe
ON pot_chest_observations(installation_hash, territory_id, pot_fate_id, is_reroll, received_at_utc);

CREATE INDEX idx_pot_chest_observations_processed_received
ON pot_chest_observations(processed, received_at_utc);

CREATE TABLE pot_chest_candidates (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    territory_id INTEGER NOT NULL,
    pot_fate_id INTEGER NOT NULL,
    is_reroll INTEGER NOT NULL DEFAULT 0,
    centroid_x REAL NOT NULL,
    centroid_y REAL NOT NULL,
    centroid_z REAL NOT NULL,
    observation_count INTEGER NOT NULL DEFAULT 0,
    distinct_installation_count INTEGER NOT NULL DEFAULT 0,
    first_observed_at_utc TEXT NOT NULL,
    last_observed_at_utc TEXT NOT NULL,
    status TEXT NOT NULL DEFAULT 'pending'
        CHECK (status IN ('pending', 'review', 'accepted', 'rejected')),
    acceptance_method TEXT CHECK (acceptance_method IN ('automatic', 'manual')),
    created_at_utc TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at_utc TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    reviewed_at_utc TEXT,
    review_note TEXT
);

CREATE INDEX idx_pot_chest_candidates_lookup
ON pot_chest_candidates(territory_id, pot_fate_id, is_reroll, status);

CREATE TABLE pot_chest_candidate_members (
    candidate_id INTEGER NOT NULL REFERENCES pot_chest_candidates(id),
    observation_id INTEGER NOT NULL REFERENCES pot_chest_observations(id),
    installation_hash TEXT NOT NULL,
    assigned_at_utc TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (candidate_id, observation_id),
    UNIQUE (observation_id)
);

CREATE INDEX idx_pot_chest_candidate_members_candidate
ON pot_chest_candidate_members(candidate_id);
