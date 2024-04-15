'use strict';

const {
  checkIfServerReachable,
  sendHasExistingVaultRequest,
  sendUnlockVaultRequest
} = require('./util/requestsUtil.js');

const {
  isAuthenticated
} = require('./util/authUtil.js');

let serverIsUp = false;
let isUserAuthenticated = false;

let hasExistingVault = null;

$('#newVaultBtn').on('click', function () {
  open('setup.html');
});

$('#importVaultBtn').on('click', function () {
  open('import.html');
});

$('#passphrase-input').on('input', function () {
  $('#passphrase-input').removeClass('is-invalid').removeClass('is-invalid-lite');
});

$('#toggle-passphrase-visibility').on('click', function () {
  const input = $('#passphrase-input');
  const icon = $('#toggle-passphrase-visibility-icon');

  if (input.attr('type') === 'password') {
    input.attr('type', 'text');
  } else {
    input.attr('type', 'password');
  }
});

$('#unlock-vault-button').on('click', async function () {
  const passphrase = $('#passphrase-input').val();

  const words = passphrase.split(' ');
  const isValid = words.length >= 4 && words.length <= 10 && words.every((word) => word === word.toLowerCase());

  if (!isValid) {
    $('#passphrase-input').addClass('is-invalid').addClass('is-invalid-lite');
    return false;
  }

  const response = await sendUnlockVaultRequest(passphrase);
});

$(async () => {
  serverIsUp = await checkIfServerReachable();
  isUserAuthenticated = await isAuthenticated();

  await setElements();
  await ConfirmServerStatus();
});

// Check every 2 seconds if the server is reachable
async function ConfirmServerStatus() {
  setInterval(async () => {
    serverIsUp = await checkIfServerReachable();
    await setElements();
  }, 2000);
}

// Check every 4 seconds if the user is authenticated
async function ConfirmIsAuthenticated() {
  setInterval(async () => {
    isUserAuthenticated = await isAuthenticated();
  }, 4000);
}

async function setElements() {
  if (serverIsUp) {
    $('#connection-status-ok').show();
    $('#connection-status-fail').hide();
  }
  else {
    $('#connection-status-ok').hide();
    $('#connection-status-error').show();

    return;
  }

  if (hasExistingVault == null)
    hasExistingVault = await sendHasExistingVaultRequest();

  chrome.storage.local.get(['setup_complete'], function (result) {
    const initialSetupElement = $('#initial-setup');
    const authenticatedReadyElement = $('#authenticated-ready');
    const notAuthenticatedElement = $('#not-authenticated');

    if (serverIsUp && result.setup_complete && hasExistingVault) {
      // Display the default popup.
      initialSetupElement.hide();

      if (isUserAuthenticated) {
        // User is authenticated, display all elements.
        authenticatedReadyElement.show();
        notAuthenticatedElement.hide();

        $('#connection-ok-icon').removeClass('bi-database-fill-lock').addClass('bi-database-fill-check');
      } else {
        // User is not authenticated, display only login element.
        notAuthenticatedElement.show();
        authenticatedReadyElement.hide();

        $('#connection-ok-icon').removeClass('bi-database-fill-check').addClass('bi-database-fill-lock');
      }
    } else if (serverIsUp && !hasExistingVault) {
      // Display setup options.
      initialSetupElement.show();
      authenticatedReadyElement.hide();
    }
  });
}
