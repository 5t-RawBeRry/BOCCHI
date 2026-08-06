var __defProp = Object.defineProperty;
var __name = (target, value) => __defProp(target, "name", { value, configurable: true });

// src/processor.ts
var MAX_OBSERVATIONS_PER_RUN = 100;
var CLUSTER_RADIUS = 1.5;
var REVIEW_REPORTER_THRESHOLD = 3;
async function processPendingObservations(env) {
  const pending = await env.DB.prepare(`
    SELECT
      o.id,
      o.territory_id,
      o.data_id,
      o.world_x,
      o.world_y,
      o.world_z,
      o.installation_hash,
      o.observed_at_utc
    FROM observations o
    WHERE o.processed = 0
      AND o.data_id IS NOT NULL
      AND NOT EXISTS (
        SELECT 1
        FROM observation_candidate_members m
        WHERE m.observation_id = o.id
      )
    ORDER BY o.id
    LIMIT ?
  `).bind(MAX_OBSERVATIONS_PER_RUN).all();
  let assigned = 0;
  let failed = 0;
  for (const observation of pending.results) {
    try {
      let candidate = await findCandidate(env.DB, observation);
      if (candidate === null) {
        candidate = await createCandidate(env.DB, observation);
      }
      await assignObservation(env.DB, candidate.id, observation);
      assigned++;
    } catch (error) {
      failed++;
      console.error(`Failed to process observation ${observation.id}`, error);
    }
  }
  return {
    scanned: pending.results.length,
    assigned,
    failed
  };
}
__name(processPendingObservations, "processPendingObservations");
async function findCandidate(db, observation) {
  const candidates = await db.prepare(`
    SELECT id, centroid_x, centroid_y, centroid_z
    FROM observation_candidates
    WHERE territory_id = ?
      AND data_id = ?
      AND status != 'rejected'
      AND ABS(centroid_x - ?) <= ?
      AND ABS(centroid_y - ?) <= ?
      AND ABS(centroid_z - ?) <= ?
  `).bind(
    observation.territory_id,
    observation.data_id,
    observation.world_x,
    CLUSTER_RADIUS,
    observation.world_y,
    CLUSTER_RADIUS,
    observation.world_z,
    CLUSTER_RADIUS
  ).all();
  const radiusSquared = CLUSTER_RADIUS * CLUSTER_RADIUS;
  return candidates.results.map((candidate) => ({
    candidate,
    distanceSquared: distanceSquared(candidate, observation)
  })).filter((entry) => entry.distanceSquared <= radiusSquared).sort((left, right) => left.distanceSquared - right.distanceSquared)[0]?.candidate ?? null;
}
__name(findCandidate, "findCandidate");
async function createCandidate(db, observation) {
  const result = await db.prepare(`
    INSERT INTO observation_candidates (
      territory_id,
      data_id,
      centroid_x,
      centroid_y,
      centroid_z,
      first_observed_at_utc,
      last_observed_at_utc
    ) VALUES (?, ?, ?, ?, ?, ?, ?)
  `).bind(
    observation.territory_id,
    observation.data_id,
    observation.world_x,
    observation.world_y,
    observation.world_z,
    observation.observed_at_utc,
    observation.observed_at_utc
  ).run();
  if (!result.success || result.meta.last_row_id === void 0) {
    throw new Error("Candidate insert failed.");
  }
  return {
    id: result.meta.last_row_id,
    centroid_x: observation.world_x,
    centroid_y: observation.world_y,
    centroid_z: observation.world_z
  };
}
__name(createCandidate, "createCandidate");
async function assignObservation(db, candidateId, observation) {
  await db.prepare(`
    INSERT OR IGNORE INTO observation_candidate_members (
      candidate_id,
      observation_id,
      installation_hash
    ) VALUES (?, ?, ?)
  `).bind(candidateId, observation.id, observation.installation_hash).run();
  await db.prepare(`
    UPDATE observation_candidates
    SET observation_count = (
          SELECT COUNT(*)
          FROM observation_candidate_members
          WHERE candidate_id = ?
        ),
        distinct_installation_count = (
          SELECT COUNT(DISTINCT installation_hash)
          FROM observation_candidate_members
          WHERE candidate_id = ?
        ),
        centroid_x = (
          SELECT AVG(o.world_x)
          FROM observation_candidate_members m
          JOIN observations o ON o.id = m.observation_id
          WHERE m.candidate_id = ?
        ),
        centroid_y = (
          SELECT AVG(o.world_y)
          FROM observation_candidate_members m
          JOIN observations o ON o.id = m.observation_id
          WHERE m.candidate_id = ?
        ),
        centroid_z = (
          SELECT AVG(o.world_z)
          FROM observation_candidate_members m
          JOIN observations o ON o.id = m.observation_id
          WHERE m.candidate_id = ?
        ),
        first_observed_at_utc = (
          SELECT MIN(o.observed_at_utc)
          FROM observation_candidate_members m
          JOIN observations o ON o.id = m.observation_id
          WHERE m.candidate_id = ?
        ),
        last_observed_at_utc = (
          SELECT MAX(o.observed_at_utc)
          FROM observation_candidate_members m
          JOIN observations o ON o.id = m.observation_id
          WHERE m.candidate_id = ?
        ),
        status = CASE
          WHEN status = 'pending'
            AND (
              SELECT COUNT(DISTINCT installation_hash)
              FROM observation_candidate_members
              WHERE candidate_id = ?
            ) >= ?
            THEN 'accepted'
          ELSE status
        END,
        acceptance_method = CASE
          WHEN status = 'pending'
            AND (
              SELECT COUNT(DISTINCT installation_hash)
              FROM observation_candidate_members
              WHERE candidate_id = ?
            ) >= ?
            THEN 'automatic'
          ELSE acceptance_method
        END,
        reviewed_at_utc = CASE
          WHEN status = 'pending'
            AND (
              SELECT COUNT(DISTINCT installation_hash)
              FROM observation_candidate_members
              WHERE candidate_id = ?
            ) >= ?
            THEN COALESCE(reviewed_at_utc, CURRENT_TIMESTAMP)
          ELSE reviewed_at_utc
        END,
        review_note = CASE
          WHEN status = 'pending'
            AND (
              SELECT COUNT(DISTINCT installation_hash)
              FROM observation_candidate_members
              WHERE candidate_id = ?
            ) >= ?
            THEN 'Automatically accepted after three distinct installation reports.'
          ELSE review_note
        END,
        updated_at_utc = CURRENT_TIMESTAMP
    WHERE id = ?
  `).bind(
    candidateId,
    candidateId,
    candidateId,
    candidateId,
    candidateId,
    candidateId,
    candidateId,
    candidateId,
    REVIEW_REPORTER_THRESHOLD,
    candidateId,
    REVIEW_REPORTER_THRESHOLD,
    candidateId,
    REVIEW_REPORTER_THRESHOLD,
    candidateId,
    REVIEW_REPORTER_THRESHOLD,
    candidateId
  ).run();
  await db.prepare(`
    UPDATE observations
    SET processed = 1
    WHERE id = ?
  `).bind(observation.id).run();
}
__name(assignObservation, "assignObservation");
function distanceSquared(candidate, observation) {
  const deltaX = candidate.centroid_x - observation.world_x;
  const deltaY = candidate.centroid_y - observation.world_y;
  const deltaZ = candidate.centroid_z - observation.world_z;
  return deltaX * deltaX + deltaY * deltaY + deltaZ * deltaZ;
}
__name(distanceSquared, "distanceSquared");

// src/index.ts
var MAX_REQUEST_BYTES = 8 * 1024;
var MAX_STRING_LENGTH = 128;
var UTC_TIMESTAMP_PATTERN = /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,7})?(?:Z|\+00:00)$/;
var INSTANCE_KEY_PATTERN = /^[0-9A-Fa-f]{64}$/;
var CANDIDATE_STATUSES = /* @__PURE__ */ new Set(["pending", "review", "accepted", "rejected"]);
var OCCULT_CRESCENT_TERRITORY_IDS = /* @__PURE__ */ new Set([1252, 1346]);
var OCCULT_POT_FATE_IDS = /* @__PURE__ */ new Set([1976, 1977, 2072, 2073]);
var POT_CYCLE_MAX_AGE_SECONDS = 45 * 60;
function jsonResponse(body, status = 200, extraHeaders = {}) {
  return Response.json(body, {
    status,
    headers: {
      "Cache-Control": "no-store",
      ...extraHeaders
    }
  });
}
__name(jsonResponse, "jsonResponse");
async function enforceObservationRateLimit(request, env) {
  const key = request.headers.get("CF-Connecting-IP") ?? "local-development";
  const result = await env.OBSERVATION_IP_LIMITER.limit({ key });
  if (result.success) {
    return null;
  }
  return jsonResponse(
    { accepted: false, error: "Rate limit exceeded." },
    429,
    { "Retry-After": "60" }
  );
}
__name(enforceObservationRateLimit, "enforceObservationRateLimit");
function authorizeAdmin(request, env) {
  const configuredToken = env.ADMIN_TOKEN?.trim();
  if (!configuredToken) {
    return jsonResponse({ error: "Not found." }, 404);
  }
  if (request.headers.get("Authorization") !== `Bearer ${configuredToken}`) {
    return jsonResponse(
      { error: "Unauthorized." },
      401,
      { "WWW-Authenticate": "Bearer" }
    );
  }
  return null;
}
__name(authorizeAdmin, "authorizeAdmin");
function parsePositiveInteger(value) {
  if (value === null || !/^\d+$/.test(value)) {
    return null;
  }
  const parsed = Number(value);
  return Number.isSafeInteger(parsed) && parsed > 0 ? parsed : null;
}
__name(parsePositiveInteger, "parsePositiveInteger");
function parseNonNegativeInteger(value) {
  if (value === null || !/^\d+$/.test(value)) {
    return null;
  }
  const parsed = Number(value);
  return Number.isSafeInteger(parsed) && parsed >= 0 ? parsed : null;
}
__name(parseNonNegativeInteger, "parseNonNegativeInteger");
async function listCandidates(request, env) {
  const url = new URL(request.url);
  const status = url.searchParams.get("status");
  if (status !== null && !CANDIDATE_STATUSES.has(status)) {
    return jsonResponse({ error: "Invalid candidate status." }, 400);
  }
  const territoryId = url.searchParams.get("territoryId");
  const parsedTerritoryId = territoryId === null ? null : parsePositiveInteger(territoryId);
  if (territoryId !== null && parsedTerritoryId === null) {
    return jsonResponse({ error: "Invalid territoryId." }, 400);
  }
  const dataId = url.searchParams.get("dataId");
  const parsedDataId = dataId === null ? null : parsePositiveInteger(dataId);
  if (dataId !== null && parsedDataId === null) {
    return jsonResponse({ error: "Invalid dataId." }, 400);
  }
  const requestedLimit = url.searchParams.get("limit");
  const parsedLimit = requestedLimit === null ? 50 : parsePositiveInteger(requestedLimit);
  if (parsedLimit === null) {
    return jsonResponse({ error: "Invalid limit." }, 400);
  }
  const requestedOffset = url.searchParams.get("offset");
  const parsedOffset = requestedOffset === null ? 0 : parseNonNegativeInteger(requestedOffset);
  if (parsedOffset === null) {
    return jsonResponse({ error: "Invalid offset." }, 400);
  }
  const clauses = ["1 = 1"];
  const values = [];
  if (status !== null) {
    clauses.push("status = ?");
    values.push(status);
  }
  if (parsedTerritoryId !== null) {
    clauses.push("territory_id = ?");
    values.push(parsedTerritoryId);
  }
  if (parsedDataId !== null) {
    clauses.push("data_id = ?");
    values.push(parsedDataId);
  }
  const candidates = await env.DB.prepare(`
    SELECT id, territory_id, data_id,
      centroid_x, centroid_y, centroid_z,
      observation_count, distinct_installation_count,
      first_observed_at_utc, last_observed_at_utc,
      status, created_at_utc, updated_at_utc,
      reviewed_at_utc, review_note, acceptance_method
    FROM observation_candidates
    WHERE ${clauses.join(" AND ")}
    ORDER BY updated_at_utc DESC, id DESC
    LIMIT ? OFFSET ?
  `).bind(...values, Math.min(parsedLimit, 100) + 1, parsedOffset).all();
  const pageSize = Math.min(parsedLimit, 100);
  return jsonResponse({
    candidates: candidates.results.slice(0, pageSize),
    hasMore: candidates.results.length > pageSize
  });
}
__name(listCandidates, "listCandidates");
async function getCandidateDetail(candidateId, env) {
  const candidate = await env.DB.prepare(`
    SELECT id, territory_id, data_id,
      centroid_x, centroid_y, centroid_z,
      observation_count, distinct_installation_count,
      first_observed_at_utc, last_observed_at_utc,
      status, created_at_utc, updated_at_utc,
      reviewed_at_utc, review_note, acceptance_method
    FROM observation_candidates
    WHERE id = ?
  `).bind(candidateId).first();
  if (candidate === null) {
    return jsonResponse({ error: "Candidate not found." }, 404);
  }
  const members = await env.DB.prepare(`
    SELECT o.id AS observation_id,
      o.territory_id, o.data_id,
      o.world_x, o.world_y, o.world_z,
      o.coffer_type, o.plugin_version,
      o.game_version, o.observed_at_utc,
      o.received_at_utc
    FROM observation_candidate_members m
    JOIN observations o ON o.id = m.observation_id
    WHERE m.candidate_id = ?
    ORDER BY o.observed_at_utc, o.id
  `).bind(candidateId).all();
  return jsonResponse({ candidate, members: members.results });
}
__name(getCandidateDetail, "getCandidateDetail");
async function buildAcceptedCandidatesPayload(request, env) {
  const url = new URL(request.url);
  const territoryId = url.searchParams.get("territoryId");
  const parsedTerritoryId = territoryId === null ? null : parsePositiveInteger(territoryId);
  if (territoryId !== null && parsedTerritoryId === null) {
    throw jsonResponse({ error: "Invalid territoryId." }, 400);
  }
  if (parsedTerritoryId !== null && !OCCULT_CRESCENT_TERRITORY_IDS.has(parsedTerritoryId)) {
    throw jsonResponse({ error: "territoryId must be an Occult Crescent zone." }, 400);
  }
  const dataId = url.searchParams.get("dataId");
  const parsedDataId = dataId === null ? null : parsePositiveInteger(dataId);
  if (dataId !== null && parsedDataId === null) {
    throw jsonResponse({ error: "Invalid dataId." }, 400);
  }
  const clauses = ["status = 'accepted'"];
  const values = [];
  if (parsedTerritoryId !== null) {
    clauses.push("territory_id = ?");
    values.push(parsedTerritoryId);
  }
  if (parsedDataId !== null) {
    clauses.push("data_id = ?");
    values.push(parsedDataId);
  }
  const candidates = await env.DB.prepare(`
    SELECT id, territory_id, data_id,
      centroid_x, centroid_y, centroid_z,
      observation_count, distinct_installation_count,
      first_observed_at_utc, last_observed_at_utc,
      acceptance_method
    FROM observation_candidates
    WHERE ${clauses.join(" AND ")}
    ORDER BY territory_id, data_id,
      centroid_x, centroid_y, centroid_z, id
  `).bind(...values).all();
  return {
    schemaVersion: 1,
    generatedAtUtc: (/* @__PURE__ */ new Date()).toISOString(),
    candidates: candidates.results.map((candidate) => ({
      candidateId: candidate.id,
      territoryId: candidate.territory_id,
      dataId: candidate.data_id,
      position: {
        x: candidate.centroid_x,
        y: candidate.centroid_y,
        z: candidate.centroid_z
      },
      observationCount: candidate.observation_count,
      distinctInstallationCount: candidate.distinct_installation_count,
      firstObservedAtUtc: candidate.first_observed_at_utc,
      lastObservedAtUtc: candidate.last_observed_at_utc,
      acceptanceMethod: candidate.acceptance_method
    }))
  };
}
__name(buildAcceptedCandidatesPayload, "buildAcceptedCandidatesPayload");
async function exportAcceptedCandidates(request, env) {
  try {
    const payload = await buildAcceptedCandidatesPayload(request, env);
    return jsonResponse(
      payload,
      200,
      { "Content-Disposition": 'attachment; filename="accepted-candidates.json"' }
    );
  } catch (error) {
    if (error instanceof Response) {
      return error;
    }
    throw error;
  }
}
__name(exportAcceptedCandidates, "exportAcceptedCandidates");
async function listAcceptedCandidatesPublic(request, env) {
  try {
    const payload = await buildAcceptedCandidatesPayload(request, env);
    return jsonResponse(payload, 200, { "Cache-Control": "public, max-age=60" });
  } catch (error) {
    if (error instanceof Response) {
      return error;
    }
    throw error;
  }
}
__name(listAcceptedCandidatesPublic, "listAcceptedCandidatesPublic");
async function reviewCandidate(request, candidateId, env) {
  const body = await parseJsonBody(request);
  if (body === null || typeof body !== "object" || Array.isArray(body)) {
    return jsonResponse({ error: "Body must be a JSON object." }, 400);
  }
  const input = body;
  if (typeof input.status !== "string" || !CANDIDATE_STATUSES.has(input.status)) {
    return jsonResponse({ error: "Invalid candidate status." }, 400);
  }
  if (input.note !== void 0 && input.note !== null && (typeof input.note !== "string" || input.note.length > 512)) {
    return jsonResponse({ error: "Invalid review note." }, 400);
  }
  const existing = await env.DB.prepare(
    "SELECT id FROM observation_candidates WHERE id = ?"
  ).bind(candidateId).first();
  if (existing === null) {
    return jsonResponse({ error: "Candidate not found." }, 404);
  }
  const reviewTimestamp = input.status === "accepted" || input.status === "rejected" ? (/* @__PURE__ */ new Date()).toISOString() : null;
  await env.DB.prepare(`
    UPDATE observation_candidates
    SET status = ?,
      review_note = ?,
      reviewed_at_utc = ?,
      acceptance_method = ?,
      updated_at_utc = CURRENT_TIMESTAMP
    WHERE id = ?
  `).bind(
    input.status,
    typeof input.note === "string" && input.note.trim().length > 0 ? input.note.trim() : null,
    reviewTimestamp,
    input.status === "accepted" || input.status === "rejected" ? "manual" : null,
    candidateId
  ).run();
  return getCandidateDetail(candidateId, env);
}
__name(reviewCandidate, "reviewCandidate");
function isFiniteNumber(value) {
  return typeof value === "number" && Number.isFinite(value);
}
__name(isFiniteNumber, "isFiniteNumber");
function isIntegerInRange(value, minimum, maximum) {
  return typeof value === "number" && Number.isInteger(value) && value >= minimum && value <= maximum;
}
__name(isIntegerInRange, "isIntegerInRange");
function isAcceptableString(value, required, maxLength = MAX_STRING_LENGTH) {
  if (value === null || value === void 0) {
    return !required;
  }
  return typeof value === "string" && value.trim().length > 0 && value.length <= maxLength;
}
__name(isAcceptableString, "isAcceptableString");
function validateObservation(value) {
  if (value === null || typeof value !== "object" || Array.isArray(value)) {
    return "Body must be a JSON object.";
  }
  const observation = value;
  if (!isIntegerInRange(observation.territoryId, 1, 1e5)) {
    return "Invalid territoryId.";
  }
  if (!OCCULT_CRESCENT_TERRITORY_IDS.has(observation.territoryId)) {
    return "territoryId must be an Occult Crescent zone.";
  }
  if (!isIntegerInRange(observation.dataId, 1, 4294967295)) {
    return "Invalid dataId.";
  }
  if (observation.mapId !== null && observation.mapId !== void 0 && !isIntegerInRange(observation.mapId, 1, 1e5)) {
    return "Invalid mapId.";
  }
  if (!isFiniteNumber(observation.worldX) || !isFiniteNumber(observation.worldY) || !isFiniteNumber(observation.worldZ)) {
    return "Coordinates must be finite numbers.";
  }
  const coordinateLimit = 1e6;
  if (Math.abs(observation.worldX) > coordinateLimit || Math.abs(observation.worldY) > coordinateLimit || Math.abs(observation.worldZ) > coordinateLimit) {
    return "Coordinates are outside the accepted range.";
  }
  if (!isAcceptableString(observation.installationHash, true, 128)) {
    return "installationHash is required.";
  }
  if (!isAcceptableString(observation.pluginVersion, true, 64)) {
    return "pluginVersion is required.";
  }
  if (!isAcceptableString(observation.gameVersion, false, 64) || !isAcceptableString(observation.cofferType, false, 64) || !isAcceptableString(observation.observedAtUtc, true, 64)) {
    return "One or more string fields are invalid.";
  }
  if (!UTC_TIMESTAMP_PATTERN.test(observation.observedAtUtc)) {
    return "observedAtUtc must be an ISO-8601 UTC timestamp.";
  }
  const observedAt = Date.parse(observation.observedAtUtc);
  const now = Date.now();
  if (!Number.isFinite(observedAt)) {
    return "observedAtUtc is invalid.";
  }
  if (observedAt > now + 10 * 60 * 1e3) {
    return "Observation is too far in the future.";
  }
  if (observedAt < now - 7 * 24 * 60 * 60 * 1e3) {
    return "Observation is too old.";
  }
  return null;
}
__name(validateObservation, "validateObservation");
async function readBodyWithinLimit(request) {
  const contentLength = request.headers.get("Content-Length");
  if (contentLength !== null && Number(contentLength) > MAX_REQUEST_BYTES) {
    throw new Response("Request body is too large.", { status: 413 });
  }
  if (!request.body) {
    return "";
  }
  const reader = request.body.getReader();
  const decoder = new TextDecoder();
  const chunks = [];
  let byteLength = 0;
  try {
    while (true) {
      const { done, value } = await reader.read();
      if (done) {
        break;
      }
      byteLength += value.byteLength;
      if (byteLength > MAX_REQUEST_BYTES) {
        await reader.cancel();
        throw new Response("Request body is too large.", { status: 413 });
      }
      chunks.push(decoder.decode(value, { stream: true }));
    }
  } finally {
    reader.releaseLock();
  }
  chunks.push(decoder.decode());
  return chunks.join("");
}
__name(readBodyWithinLimit, "readBodyWithinLimit");
async function parseJsonBody(request) {
  const contentType = request.headers.get("Content-Type") ?? "";
  const mediaType = contentType.split(";", 1)[0].trim().toLowerCase();
  if (mediaType !== "application/json") {
    throw new Response("Content-Type must be application/json.", { status: 415 });
  }
  const bodyText = await readBodyWithinLimit(request);
  try {
    return JSON.parse(bodyText);
  } catch {
    throw new Response("Invalid JSON.", { status: 400 });
  }
}
__name(parseJsonBody, "parseJsonBody");
function validatePotCycle(value) {
  if (value === null || typeof value !== "object" || Array.isArray(value)) {
    return "Body must be a JSON object.";
  }
  const pot = value;
  if (!isAcceptableString(pot.instanceKey, true, 64) || !INSTANCE_KEY_PATTERN.test(pot.instanceKey)) {
    return "instanceKey must be a 64-character hex SHA-256 digest.";
  }
  if (!isIntegerInRange(pot.territoryId, 1, 1e5)) {
    return "Invalid territoryId.";
  }
  if (!OCCULT_CRESCENT_TERRITORY_IDS.has(pot.territoryId)) {
    return "territoryId must be an Occult Crescent zone.";
  }
  if (!isIntegerInRange(pot.datacenterId, 1, 1e5)) {
    return "Invalid datacenterId.";
  }
  if (!isIntegerInRange(pot.potFateId, 1, 1e5) || !OCCULT_POT_FATE_IDS.has(pot.potFateId)) {
    return "potFateId must be a known Occult Crescent pot FATE.";
  }
  if (!isIntegerInRange(pot.spawnAtUnix, 1e9, 4e9)) {
    return "Invalid spawnAtUnix.";
  }
  const nowUnix = Math.floor(Date.now() / 1e3);
  if (pot.spawnAtUnix > nowUnix + 10 * 60) {
    return "spawnAtUnix is too far in the future.";
  }
  if (pot.spawnAtUnix < nowUnix - POT_CYCLE_MAX_AGE_SECONDS) {
    return "spawnAtUnix is too old.";
  }
  if (!isAcceptableString(pot.installationHash, true, 128)) {
    return "installationHash is required.";
  }
  if (!isAcceptableString(pot.pluginVersion, true, 64)) {
    return "pluginVersion is required.";
  }
  if (!isAcceptableString(pot.observedAtUtc, true, 64)) {
    return "observedAtUtc is required.";
  }
  if (!UTC_TIMESTAMP_PATTERN.test(pot.observedAtUtc)) {
    return "observedAtUtc must be an ISO-8601 UTC timestamp.";
  }
  const observedAt = Date.parse(pot.observedAtUtc);
  const now = Date.now();
  if (!Number.isFinite(observedAt)) {
    return "observedAtUtc is invalid.";
  }
  if (observedAt > now + 10 * 60 * 1e3) {
    return "Observation is too far in the future.";
  }
  if (observedAt < now - 7 * 24 * 60 * 60 * 1e3) {
    return "Observation is too old.";
  }
  return null;
}
__name(validatePotCycle, "validatePotCycle");
async function submitPotCycle(request, env) {
  const body = await parseJsonBody(request);
  const validationError = validatePotCycle(body);
  if (validationError !== null) {
    return jsonResponse({ accepted: false, error: validationError }, 400);
  }
  const pot = body;
  const observedAtUtc = new Date(pot.observedAtUtc).toISOString();
  const instanceKey = pot.instanceKey.trim().toUpperCase();
  const result = await env.DB.prepare(`
    INSERT INTO pot_cycles (
      instance_key, territory_id, datacenter_id, pot_fate_id, spawn_at_unix,
      installation_hash, plugin_version, observed_at_utc
    )
    SELECT ?, ?, ?, ?, ?, ?, ?, ?
    WHERE NOT EXISTS (
      SELECT 1
      FROM pot_cycles
      WHERE installation_hash = ?
        AND instance_key = ?
        AND pot_fate_id = ?
        AND spawn_at_unix = ?
        AND created_at_utc >= datetime('now', '-10 minutes')
    )
  `).bind(
    instanceKey,
    pot.territoryId,
    pot.datacenterId,
    pot.potFateId,
    pot.spawnAtUnix,
    pot.installationHash.trim(),
    pot.pluginVersion.trim(),
    observedAtUtc,
    pot.installationHash.trim(),
    instanceKey,
    pot.potFateId,
    pot.spawnAtUnix
  ).run();
  if (!result.success) {
    return jsonResponse({ accepted: false, error: "Database insert failed." }, 500);
  }
  if (result.meta.changes === 0) {
    return jsonResponse({ accepted: true, duplicate: true });
  }
  return jsonResponse({
    accepted: true,
    duplicate: false,
    potCycleId: result.meta.last_row_id
  }, 201);
}
__name(submitPotCycle, "submitPotCycle");
async function getPotCycle(request, env) {
  const url = new URL(request.url);
  const instanceKey = url.searchParams.get("instanceKey")?.trim() ?? "";
  if (!INSTANCE_KEY_PATTERN.test(instanceKey)) {
    return jsonResponse({ found: false, error: "instanceKey must be a 64-character hex SHA-256 digest." }, 400);
  }
  const minSpawnUnix = Math.floor(Date.now() / 1e3) - POT_CYCLE_MAX_AGE_SECONDS;
  const row = await env.DB.prepare(`
    SELECT instance_key, territory_id, datacenter_id, pot_fate_id, spawn_at_unix, observed_at_utc
    FROM pot_cycles
    WHERE instance_key = ?
      AND spawn_at_unix >= ?
    ORDER BY spawn_at_unix DESC, id DESC
    LIMIT 1
  `).bind(instanceKey.toUpperCase(), minSpawnUnix).first();
  if (row === null) {
    return jsonResponse({ found: false });
  }
  return jsonResponse({
    found: true,
    instanceKey: row.instance_key,
    territoryId: row.territory_id,
    datacenterId: row.datacenter_id,
    potFateId: row.pot_fate_id,
    spawnAtUnix: row.spawn_at_unix,
    observedAtUtc: row.observed_at_utc
  });
}
__name(getPotCycle, "getPotCycle");
async function submitObservation(request, env) {
  const body = await parseJsonBody(request);
  const validationError = validateObservation(body);
  if (validationError !== null) {
    return jsonResponse({ accepted: false, error: validationError }, 400);
  }
  const observation = body;
  const observedAtUtc = new Date(observation.observedAtUtc).toISOString();
  const result = await env.DB.prepare(`
    INSERT INTO observations (
      territory_id, data_id, map_id, world_x, world_y, world_z,
      coffer_type, installation_hash, plugin_version,
      game_version, observed_at_utc
    )
    SELECT ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?
    WHERE NOT EXISTS (
      SELECT 1
      FROM observations
      WHERE installation_hash = ?
        AND territory_id = ?
        AND ABS(world_x - ?) <= 0.1
        AND ABS(world_y - ?) <= 0.1
        AND ABS(world_z - ?) <= 0.1
        AND received_at_utc >= datetime('now', '-10 minutes')
    )
  `).bind(
    observation.territoryId,
    observation.dataId ?? null,
    observation.mapId ?? null,
    observation.worldX,
    observation.worldY,
    observation.worldZ,
    observation.cofferType?.trim() || null,
    observation.installationHash.trim(),
    observation.pluginVersion.trim(),
    observation.gameVersion?.trim() || null,
    observedAtUtc,
    observation.installationHash.trim(),
    observation.territoryId,
    observation.worldX,
    observation.worldY,
    observation.worldZ
  ).run();
  if (!result.success) {
    return jsonResponse({ accepted: false, error: "Database insert failed." }, 500);
  }
  if (result.meta.changes === 0) {
    return jsonResponse({ accepted: true, duplicate: true });
  }
  return jsonResponse({
    accepted: true,
    duplicate: false,
    observationId: result.meta.last_row_id
  }, 201);
}
__name(submitObservation, "submitObservation");
var src_default = {
  async fetch(request, env, ctx) {
    const url = new URL(request.url);
    if (request.method === "GET" && url.pathname === "/health") {
      return jsonResponse({ status: "ok" });
    }
    if (request.method === "GET" && url.pathname === "/api/v1/candidates") {
      try {
        return await listAcceptedCandidatesPublic(request, env);
      } catch (error) {
        if (error instanceof Response) {
          return error;
        }
        console.error(error);
        return jsonResponse({ error: "Unexpected server error." }, 500);
      }
    }
    if (url.pathname.startsWith("/api/v1/admin/")) {
      const authorizationError = authorizeAdmin(request, env);
      if (authorizationError !== null) {
        return authorizationError;
      }
      try {
        if (request.method === "GET" && url.pathname === "/api/v1/admin/candidates") {
          return await listCandidates(request, env);
        }
        if (request.method === "GET" && url.pathname === "/api/v1/admin/export/accepted-candidates") {
          return await exportAcceptedCandidates(request, env);
        }
        const candidateDetailMatch = url.pathname.match(/^\/api\/v1\/admin\/candidates\/(\d+)$/);
        if (candidateDetailMatch !== null && request.method === "GET") {
          const candidateId = parsePositiveInteger(candidateDetailMatch[1]);
          return candidateId === null ? jsonResponse({ error: "Invalid candidate ID." }, 400) : await getCandidateDetail(candidateId, env);
        }
        const candidateReviewMatch = url.pathname.match(/^\/api\/v1\/admin\/candidates\/(\d+)\/review$/);
        if (candidateReviewMatch !== null && request.method === "POST") {
          const candidateId = parsePositiveInteger(candidateReviewMatch[1]);
          return candidateId === null ? jsonResponse({ error: "Invalid candidate ID." }, 400) : await reviewCandidate(request, candidateId, env);
        }
        return jsonResponse({ error: "Not found." }, 404);
      } catch (error) {
        if (error instanceof Response) {
          return error;
        }
        console.error(error);
        return jsonResponse({ error: "Unexpected server error." }, 500);
      }
    }
    if (request.method === "POST" && url.pathname === "/api/v1/observations") {
      try {
        const rateLimitResponse = await enforceObservationRateLimit(request, env);
        if (rateLimitResponse !== null) {
          return rateLimitResponse;
        }
        const response = await submitObservation(request, env);
        ctx.waitUntil(processPendingObservations(env).then((result) => {
          console.log("Observation processor (post-submit)", result);
        }));
        return response;
      } catch (error) {
        if (error instanceof Response) {
          return error;
        }
        console.error(error);
        return jsonResponse({ accepted: false, error: "Unexpected server error." }, 500);
      }
    }
    if (request.method === "POST" && url.pathname === "/api/v1/pot-cycles") {
      try {
        const rateLimitResponse = await enforceObservationRateLimit(request, env);
        if (rateLimitResponse !== null) {
          return rateLimitResponse;
        }
        return await submitPotCycle(request, env);
      } catch (error) {
        if (error instanceof Response) {
          return error;
        }
        console.error(error);
        return jsonResponse({ accepted: false, error: "Unexpected server error." }, 500);
      }
    }
    if (request.method === "GET" && url.pathname === "/api/v1/pot-cycles") {
      try {
        return await getPotCycle(request, env);
      } catch (error) {
        if (error instanceof Response) {
          return error;
        }
        console.error(error);
        return jsonResponse({ found: false, error: "Unexpected server error." }, 500);
      }
    }
    return jsonResponse({ error: "Not found." }, 404);
  },
  async scheduled(_controller, env, _ctx) {
    const result = await processPendingObservations(env);
    console.log("Observation processor completed", result);
  }
};

// node_modules/wrangler/templates/middleware/middleware-ensure-req-body-drained.ts
var drainBody = /* @__PURE__ */ __name(async (request, env, _ctx, middlewareCtx) => {
  try {
    return await middlewareCtx.next(request, env);
  } finally {
    try {
      if (request.body !== null && !request.bodyUsed) {
        const reader = request.body.getReader();
        while (!(await reader.read()).done) {
        }
      }
    } catch (e) {
      console.error("Failed to drain the unused request body.", e);
    }
  }
}, "drainBody");
var middleware_ensure_req_body_drained_default = drainBody;

// node_modules/wrangler/templates/middleware/middleware-miniflare3-json-error.ts
function reduceError(e) {
  return {
    name: e?.name,
    message: e?.message ?? String(e),
    stack: e?.stack,
    cause: e?.cause === void 0 ? void 0 : reduceError(e.cause)
  };
}
__name(reduceError, "reduceError");
var jsonError = /* @__PURE__ */ __name(async (request, env, _ctx, middlewareCtx) => {
  try {
    return await middlewareCtx.next(request, env);
  } catch (e) {
    const error = reduceError(e);
    const body = JSON.stringify(error);
    const headers = {
      "Content-Type": "application/json",
      "MF-Experimental-Error-Stack": "true"
    };
    const encoded = encodeURIComponent(body);
    if (encoded.length <= 8192) {
      headers["MF-Experimental-Error-Stack-Payload"] = encoded;
    }
    return new Response(body, { status: 500, headers });
  }
}, "jsonError");
var middleware_miniflare3_json_error_default = jsonError;

// .wrangler/tmp/bundle-V8GPL7/middleware-insertion-facade.js
var __INTERNAL_WRANGLER_MIDDLEWARE__ = [
  middleware_ensure_req_body_drained_default,
  middleware_miniflare3_json_error_default
];
var middleware_insertion_facade_default = src_default;

// node_modules/wrangler/templates/middleware/common.ts
var __facade_middleware__ = [];
function __facade_register__(...args) {
  __facade_middleware__.push(...args.flat());
}
__name(__facade_register__, "__facade_register__");
function __facade_invokeChain__(request, env, ctx, dispatch, middlewareChain) {
  const [head, ...tail] = middlewareChain;
  const middlewareCtx = {
    dispatch,
    next(newRequest, newEnv) {
      return __facade_invokeChain__(newRequest, newEnv, ctx, dispatch, tail);
    }
  };
  return head(request, env, ctx, middlewareCtx);
}
__name(__facade_invokeChain__, "__facade_invokeChain__");
function __facade_invoke__(request, env, ctx, dispatch, finalMiddleware) {
  return __facade_invokeChain__(request, env, ctx, dispatch, [
    ...__facade_middleware__,
    finalMiddleware
  ]);
}
__name(__facade_invoke__, "__facade_invoke__");

// .wrangler/tmp/bundle-V8GPL7/middleware-loader.entry.ts
var __Facade_ScheduledController__ = class ___Facade_ScheduledController__ {
  constructor(scheduledTime, cron, noRetry) {
    this.scheduledTime = scheduledTime;
    this.cron = cron;
    this.#noRetry = noRetry;
  }
  scheduledTime;
  cron;
  static {
    __name(this, "__Facade_ScheduledController__");
  }
  #noRetry;
  noRetry() {
    if (!(this instanceof ___Facade_ScheduledController__)) {
      throw new TypeError("Illegal invocation");
    }
    this.#noRetry();
  }
};
function wrapExportedHandler(worker) {
  if (__INTERNAL_WRANGLER_MIDDLEWARE__ === void 0 || __INTERNAL_WRANGLER_MIDDLEWARE__.length === 0) {
    return worker;
  }
  for (const middleware of __INTERNAL_WRANGLER_MIDDLEWARE__) {
    __facade_register__(middleware);
  }
  const fetchDispatcher = /* @__PURE__ */ __name(function(request, env, ctx) {
    if (worker.fetch === void 0) {
      throw new Error("Handler does not export a fetch() function.");
    }
    return worker.fetch(request, env, ctx);
  }, "fetchDispatcher");
  return {
    ...worker,
    fetch(request, env, ctx) {
      const dispatcher = /* @__PURE__ */ __name(function(type, init) {
        if (type === "scheduled" && worker.scheduled !== void 0) {
          const controller = new __Facade_ScheduledController__(
            Date.now(),
            init.cron ?? "",
            () => {
            }
          );
          return worker.scheduled(controller, env, ctx);
        }
      }, "dispatcher");
      return __facade_invoke__(request, env, ctx, dispatcher, fetchDispatcher);
    }
  };
}
__name(wrapExportedHandler, "wrapExportedHandler");
function wrapWorkerEntrypoint(klass) {
  if (__INTERNAL_WRANGLER_MIDDLEWARE__ === void 0 || __INTERNAL_WRANGLER_MIDDLEWARE__.length === 0) {
    return klass;
  }
  for (const middleware of __INTERNAL_WRANGLER_MIDDLEWARE__) {
    __facade_register__(middleware);
  }
  return class extends klass {
    #fetchDispatcher = /* @__PURE__ */ __name((request, env, ctx) => {
      this.env = env;
      this.ctx = ctx;
      if (super.fetch === void 0) {
        throw new Error("Entrypoint class does not define a fetch() function.");
      }
      return super.fetch(request);
    }, "#fetchDispatcher");
    #dispatcher = /* @__PURE__ */ __name((type, init) => {
      if (type === "scheduled" && super.scheduled !== void 0) {
        const controller = new __Facade_ScheduledController__(
          Date.now(),
          init.cron ?? "",
          () => {
          }
        );
        return super.scheduled(controller);
      }
    }, "#dispatcher");
    fetch(request) {
      return __facade_invoke__(
        request,
        this.env,
        this.ctx,
        this.#dispatcher,
        this.#fetchDispatcher
      );
    }
  };
}
__name(wrapWorkerEntrypoint, "wrapWorkerEntrypoint");
var WRAPPED_ENTRY;
if (typeof middleware_insertion_facade_default === "object") {
  WRAPPED_ENTRY = wrapExportedHandler(middleware_insertion_facade_default);
} else if (typeof middleware_insertion_facade_default === "function") {
  WRAPPED_ENTRY = wrapWorkerEntrypoint(middleware_insertion_facade_default);
}
var middleware_loader_entry_default = WRAPPED_ENTRY;
export {
  __INTERNAL_WRANGLER_MIDDLEWARE__,
  middleware_loader_entry_default as default
};
//# sourceMappingURL=index.js.map
