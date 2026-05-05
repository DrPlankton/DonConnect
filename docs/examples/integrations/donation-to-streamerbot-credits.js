const DEFAULT_STREAMERBOT_HTTP_URL = "http://127.0.0.1:7474";
const DEFAULT_STREAMERBOT_ACTION_NAME = "Add Donation To Credits";

async function sendDonationToStreamerbotCredits(donation, options = {}) {
  const baseUrl = options.baseUrl || DEFAULT_STREAMERBOT_HTTP_URL;
  const actionName = options.actionName || DEFAULT_STREAMERBOT_ACTION_NAME;

  const args = {
    donorName: donation.donorName || donation.name || donation.username || "Аноним",
    amount: stringifyValue(donation.amount),
    currency: donation.currency || donation.currencyCode || "",
    platform: donation.platform || donation.source || "Донат",
    message: donation.message || donation.text || "",
    creditsSection: donation.creditsSection || options.creditsSection || "Донаты"
  };

  const response = await fetch(`${baseUrl}/DoAction`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json"
    },
    body: JSON.stringify({
      action: {
        name: actionName
      },
      args
    })
  });

  if (!response.ok) {
    throw new Error(`Streamer.bot rejected donation credits request: HTTP ${response.status}`);
  }
}

function stringifyValue(value) {
  if (value === null || value === undefined) {
    return "";
  }

  return String(value);
}

if (typeof module !== "undefined") {
  module.exports = {
    sendDonationToStreamerbotCredits
  };
}

if (typeof window !== "undefined") {
  window.sendDonationToStreamerbotCredits = sendDonationToStreamerbotCredits;
}
