import axios from "axios";
import { faker } from "@faker-js/faker";
import config from "./config.js";
import { actions } from "./actions.js";

const api = axios.create({
  baseURL: config.apiBase,
  timeout: 5000,
});

// Generate a random action with dynamic value
function randomAction() {
  const act = actions[Math.floor(Math.random() * actions.length)];
  return {
    name: act.name,
    value: typeof act.value === "function" ? act.value() : act.value,
  };
}

// Generate a tracking event
function generateEvent(tenant, sessionID, identity, platform, appVersion) {
  const act = randomAction();

  return {
    tenant_id: tenant,
    tracking: {
      name: act.name,
      value: JSON.stringify(act.value),
      session_id: sessionID,
      identity: identity,
      platform: platform,
      app_version: appVersion
    }
  };
}


// Send events in batch mode
async function sendBatch(events) {
  try {
    await api.post("/batch", { tracks: events });
  } catch (err) {
    console.error("Batch error:", err.message);
  }
}

// Send events individually
async function sendOne(event) {
  try {
    await api.post("/track", event);
  } catch (err) {
    console.error("Track error:", err.response.status, err.response.data);
  }
}

// Simulate one fake user
async function simulateUser() {
  const tenant = faker.helpers.arrayElement(config.tenants);
  const sessionID = faker.string.uuid();
  const identity = faker.string.uuid();
  console.log('Simulating user:', identity, 'on tenant:', tenant);

  const platform = faker.helpers.arrayElement(["Windows", "macOS", "Linux"]);
  const appVersion = faker.system.semver();

  let buffer = [];

  // START_APP event
  const startEvent = {
    tenant_id: tenant,
    tracking: {
      name: "start_app",
      value: "",
      identity: identity,
      session_id: sessionID,
      platform: platform,
      app_version: appVersion,
    },
  };
  await sendOne(startEvent);

  // main events
  for (let i = 0; i < config.eventsPerUser; i++) {
    const evt = generateEvent(tenant, sessionID, identity, platform, appVersion);

    if (config.batchMode) {
      buffer.push(evt);

      // Send when buffer full
      if (buffer.length >= config.batchSize) {
        await sendBatch(buffer);
        buffer = [];
      }
    } else {
      await sendOne(evt);
    }
  }

  // CLOSE_APP event
  const closeEvent = {
    tenant_id: tenant,
    tracking: {
      name: "close_app",
      value: "",
      session_id: sessionID,
      identity: identity,
      platform: platform,
      app_version: appVersion,
    },
  };
  await sendOne(closeEvent);

  // Flush last batch
  if (config.batchMode && buffer.length > 0) {
    await sendBatch(buffer);
  }
}

// Main runner
async function runLoadTest() {
  console.log("🚀 Starting Vortex load test…");

  const workers = [];
  const limit = config.concurrency;
  let running = 0;

  for (let i = 0; i < config.users; i++) {
    // concurrency limiter
    while (running >= limit) {
      await new Promise((r) => setTimeout(r, 20));
    }

    running++;
    workers.push(
      simulateUser().finally(() => (running--))
    );
  }

  await Promise.all(workers);

  console.log("✅ Load test completed!");
}

runLoadTest();
