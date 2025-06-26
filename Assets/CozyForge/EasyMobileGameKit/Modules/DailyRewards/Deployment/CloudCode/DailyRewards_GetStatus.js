const _ = require("lodash-4.17");
const { SettingsApi } = require("@unity-services/remote-config-1.1");
const { DataApi } = require("@unity-services/cloud-save-1.2");

const badRequestError = 400;
const tooManyRequestsError = 429;

module.exports = async ({ context, logger }) => {
  try {
    const { projectId, playerId, environmentId, accessToken } = context;
    const remoteConfig = new SettingsApi({ accessToken });
    const cloudSave = new DataApi({ accessToken });

    const services = { projectId, playerId, environmentId, remoteConfig, cloudSave, logger };

    const epochTime = _.now();
    logger.info("Current epochTime: " + epochTime);

    let eventState = {
      epochTime,
      result: {
        success: true,
        firstVisit: false,
        daysRemaining: 0,
        secondsTillClaimable: 0,
        secondsTillNextDay: 0
      }
    };

    await readInitialState(services, eventState);

    // Check if the player missed their login threshold
    const missedThreshold = checkMissedThreshold(services, eventState);
    if (missedThreshold) {
      // Player missed too many days in a row. Reset their progress to a fresh start.
      await resetPlayerProgress(services, logger, eventState.epochTime);

      // Re-read initial state after reset to simulate a fresh start
      await readInitialState(services, eventState);
    }

    updateState(services, eventState);

    eventState.result.daysClaimed = eventState.playerStatus.daysClaimed;

    logger.info("Result: " + JSON.stringify(eventState.result));
    return eventState.result;
  } catch (error) {
    transformAndThrowCaughtError(error);
  }
};

async function readInitialState(services, eventState) {
  const promiseResponses = await Promise.all([
    getRemoteConfigData(services),
    getEventStartEpochTime(services),
    getPlayerStatus(services)
  ]);

  eventState.configData = promiseResponses[0];
  services.logger.info("Initial configData: " + JSON.stringify(eventState.configData));

  eventState.startEpochTime = promiseResponses[1];
  services.logger.info("Initial startEpochTime: " + JSON.stringify(eventState.startEpochTime));

  eventState.playerStatus = promiseResponses[2];
  services.logger.info("Initial playerStatus: " + JSON.stringify(eventState.playerStatus));

  // Setup start epoch time if not set
  if (eventState.startEpochTime === undefined) {
    eventState.startEpochTime = await setEventStartEpochTimeForDemonstrating(services, eventState);
  }

  // Ensure playerStatus is always defined and valid
  if (!eventState.playerStatus) {
    // No playerStatus means new player scenario
    eventState.playerStatus = await startEventForPlayer(services, eventState);
    eventState.result.firstVisit = true;
  } else {
    // Validate and fix playerStatus if missing fields
    let changed = false;

    if (typeof eventState.playerStatus.daysClaimed !== 'number') {
      eventState.playerStatus.daysClaimed = 0;
      changed = true;
    }

    if (typeof eventState.playerStatus.lastClaimTime !== 'number') {
      eventState.playerStatus.lastClaimTime = 0;
      changed = true;
    }

    if (typeof eventState.playerStatus.startEpochTime !== 'number') {
      eventState.playerStatus.startEpochTime = eventState.startEpochTime;
      changed = true;
    }

    if (changed) {
      // Write back the repaired playerStatus to Cloud Save
      await services.cloudSave.setItem(services.projectId, services.playerId, {
        key: "DAILY_REWARDS_STATUS",
        value: JSON.stringify(eventState.playerStatus)
      });
      eventState.result.firstVisit = true;
    } else if (eventState.playerStatus.daysClaimed === 0) {
      eventState.result.firstVisit = true;
    }
  }

  Object.assign(eventState.result, eventState.configData);
}

function updateState(services, eventState) {
  eventState.eventTotalSeconds = eventState.configData.totalDays * eventState.configData.secondsPerDay;
  eventState.eventSecondsPassed = (eventState.epochTime - eventState.startEpochTime) / 1000;
  eventState.result.isStarted = eventState.eventSecondsPassed >= 0;

  if (eventState.result.isStarted) {
    // Calculate eventDay and lastDayClaimed using the eventSecondsPassed
    eventState.eventDay = Math.floor((eventState.epochTime - eventState.startEpochTime) / (eventState.configData.secondsPerDay * 1000));
    eventState.lastDayClaimed = Math.floor((eventState.playerStatus.lastClaimTime - eventState.startEpochTime) / (eventState.configData.secondsPerDay * 1000));

    eventState.result.secondsTillNextDay = (eventState.eventDay + 1) * eventState.configData.secondsPerDay - eventState.eventSecondsPassed;

    if (eventState.eventDay > eventState.lastDayClaimed) {
      eventState.result.secondsTillClaimable = 0;
      eventState.result.daysRemaining = eventState.configData.totalDays - eventState.eventDay;
    } else {
      eventState.result.secondsTillClaimable = eventState.result.secondsTillNextDay;
      eventState.result.daysRemaining = eventState.configData.totalDays - eventState.eventDay - 1;
    }
  }

  eventState.result.daysClaimed = eventState.playerStatus.daysClaimed;
}

function checkMissedThreshold(services, eventState) {
  // If player has never claimed a day, no need to check
  if (!eventState.playerStatus || eventState.playerStatus.daysClaimed === 0) {
    return false;
  }

  // If we have lastClaimTime and current configData
  if (eventState.playerStatus.lastClaimTime > 0 && eventState.configData && eventState.configData.secondsPerDay) {
    const timeSinceLastClaim = eventState.epochTime - eventState.playerStatus.lastClaimTime;
    const allowedMissTime = eventState.configData.secondsPerDay * 2 * 1000; // 2x secondsPerDay in ms

    if (timeSinceLastClaim > allowedMissTime) {
      services.logger.info("Player missed threshold. Time since last claim: " + timeSinceLastClaim + " ms, allowed: " + allowedMissTime + " ms. Resetting progress.");
      return true;
    }
  }

  return false;
}

async function resetPlayerProgress(services, logger, currentEpochTime) {
  logger.info("Resetting player progress to initial state instead of deleting keys.");

  // Overwrite the DAILY_REWARDS_START_EPOCH_TIME to simulate a fresh event start
  await services.cloudSave.setItem(services.projectId, services.playerId, {
    key: "DAILY_REWARDS_START_EPOCH_TIME",
    value: currentEpochTime
  });

  const freshPlayerStatus = {
    startEpochTime: currentEpochTime,
    daysClaimed: 0,
    lastClaimTime: 0
  };

  // Overwrite DAILY_REWARDS_STATUS with a fresh state
  await services.cloudSave.setItem(services.projectId, services.playerId, {
    key: "DAILY_REWARDS_STATUS",
    value: JSON.stringify(freshPlayerStatus)
  });

  logger.info("Player progress reset to fresh start state.");
}

async function getRemoteConfigData(services) {
  const response = await services.remoteConfig.assignSettingsGet(
      services.projectId,
      services.environmentId,
      'settings',
      ["DAILY_REWARDS_CONFIG"]
  );

  if (response.data.configs &&
      response.data.configs.settings &&
      response.data.configs.settings.DAILY_REWARDS_CONFIG) {
    return response.data.configs.settings.DAILY_REWARDS_CONFIG;
  }

  throw new RemoteConfigKeyMissingError("Failed to get DAILY_REWARDS_CONFIG.");
}

async function getEventStartEpochTime(services) {
  return await getCloudSaveResult(services, "DAILY_REWARDS_START_EPOCH_TIME");
}

async function getPlayerStatus(services) {
  const results = await getCloudSaveResult(services, "DAILY_REWARDS_STATUS");
  if (results) {
    return JSON.parse(results);
  }

  return undefined;
}

async function getCloudSaveResult(services, key) {
  const response = await services.cloudSave.getItems(services.projectId, services.playerId, [ key ]);

  if (response.data.results &&
      response.data.results.length > 0 &&
      response.data.results[0]) {
    return response.data.results[0].value;
  }

  return undefined;
}

async function setEventStartEpochTimeForDemonstrating(services, eventState) {
  services.logger.info("Setting start event time in Cloud Save. This would normally be set in Remote Config.");
  await services.cloudSave.setItem(services.projectId, services.playerId, { key: "DAILY_REWARDS_START_EPOCH_TIME", value: eventState.epochTime });

  return eventState.epochTime;
}

async function startEventForPlayer(services, eventState) {
  const playerStatus = {
    startEpochTime: eventState.startEpochTime,
    daysClaimed: 0,
    lastClaimTime: 0
  };

  await services.cloudSave.setItem(services.projectId, services.playerId, {
    key: "DAILY_REWARDS_STATUS",
    value: JSON.stringify(playerStatus)
  });

  services.logger.info("New player status: " + JSON.stringify(playerStatus));
  return playerStatus;
}

function transformAndThrowCaughtError(error) {
  let result = {
    status: 0,
    name: "",
    message: "",
    retryAfter: null,
    details: ""
  };

  if (error.response) {
    result.status = error.response.data.status ? error.response.data.status : 0;
    result.name = error.response.data.title ? error.response.data.title : "Unknown Error";
    result.message = error.response.data.detail ? error.response.data.detail : error.response.data;

    if (error.response.status === tooManyRequestsError) {
      result.retryAfter = error.response.headers['retry-after'];
    } else if (error.response.status === badRequestError) {
      let arr = [];
      _.forEach(error.response.data.errors, error => {
        arr = _.concat(arr, error.messages);
      });
      result.details = arr;
    }
  } else {
    if (error instanceof CloudCodeCustomError) {
      result.status = error.status;
    }
    result.name = error.name;
    result.message = error.message;
  }

  throw new Error(JSON.stringify(result));
}

class CloudCodeCustomError extends Error {
  constructor(message) {
    super(message);
    this.name = "CloudCodeCustomError";
    this.status = 1;
  }
}

class RemoteConfigKeyMissingError extends CloudCodeCustomError {
  constructor(message) {
    super(message);
    this.name = "RemoteConfigKeyMissingError";
    this.status = 2;
  }
}

module.exports.params = {};
