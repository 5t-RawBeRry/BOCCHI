CREATE TABLE carrot_observations (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    territory_id INTEGER NOT NULL,
    world_x REAL NOT NULL,
    world_y REAL NOT NULL,
    world_z REAL NOT NULL,
    object_base_id INTEGER NOT NULL DEFAULT 2010139,
    installation_hash TEXT NOT NULL,
    plugin_version TEXT NOT NULL,
    observed_at_utc TEXT NOT NULL,
    received_at_utc TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    processed INTEGER NOT NULL DEFAULT 0
);

CREATE INDEX idx_carrot_observations_pending
ON carrot_observations(processed, id);

CREATE INDEX idx_carrot_observations_near_dupe
ON carrot_observations(installation_hash, territory_id, received_at_utc);

CREATE TABLE carrot_candidates (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    territory_id INTEGER NOT NULL,
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

CREATE INDEX idx_carrot_candidates_lookup
ON carrot_candidates(territory_id, status);

CREATE TABLE carrot_candidate_members (
    candidate_id INTEGER NOT NULL REFERENCES carrot_candidates(id),
    observation_id INTEGER NOT NULL REFERENCES carrot_observations(id),
    installation_hash TEXT NOT NULL,
    assigned_at_utc TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (candidate_id, observation_id),
    UNIQUE (observation_id)
);

CREATE INDEX idx_carrot_candidate_members_candidate
ON carrot_candidate_members(candidate_id);
