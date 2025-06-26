const _ = require("lodash-4.17");
const { CurrenciesApi } = require("@unity-services/economy-2.3");

const badRequestError = 400;
const tooManyRequestsError = 429;

module.exports = async ({ params, context, logger }) => {
  try {
    const { projectId, playerId, accessToken } = context;
    const economy = new CurrenciesApi({ accessToken });

    // Get parameters from params
    const amount = params.amount;
    const currencyId = params.currencyId;

    // Prepare the request to modify the currency balance
    const currencyModifyBalanceRequest = {
      amount: amount
    };

    const requestParameters = {
      projectId: projectId,
      playerId: playerId,
      currencyId: currencyId,
      currencyModifyBalanceRequest
    };

    // Call the Economy API to increment the player's currency balance
    const response = await economy.incrementPlayerCurrencyBalance(requestParameters);

    // Return the new balance
    return {
      success: true,
      newBalance: response.data.balance
    };
  } catch (error) {
    transformAndThrowCaughtError(error);
  }
};

// Error transformation helper function
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
      _.forEach(error.response.data.errors, (error) => {
        arr = _.concat(arr, error.messages);
      });
      result.details = arr;
    }
  } else {
    result.name = error.name;
    result.message = error.message;
  }

  throw new Error(JSON.stringify(result));
}

module.exports.params = {
  amount: "numeric",
  currencyId: "string"
};