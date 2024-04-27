'use strict';

const {
  setServerAddress,
  checkIfServerReachable,
  sendHasExistingVaultRequest,
  sendUnlockVaultRequest,
  sendLockVaultRequest,
  sendLoginDetailsCountRequest,
  sendAuthenticatorCountRequest,
} = require('./util/requestsUtil.js');

const {
  initPublic,
  isHandshakeComplete,
  encryptPassword,
} = require('./util/passwordUtil.js');

const { isAuthenticated, setTokens } = require('./util/authUtil.js');

const sourceId = Math.floor(Math.random() * 1000000);

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
  $('#passphrase-input')
    .removeClass('is-invalid')
    .removeClass('is-invalid-lite');
});

$('#vault-server-address-input').on('input', function () {
  $('#vault-login-server-address-input')
    .removeClass('is-invalid')
    .removeClass('is-invalid-lite');
});

$('#toggle-passphrase-visibility').on('click', function () {
  const input = $('#passphrase-input');

  if (input.attr('type') === 'password') {
    input.attr('type', 'text');
  } else {
    input.attr('type', 'password');
  }
});

$('#unlock-vault-button').on('click', async function () {
  if (!isHandshakeComplete()) {
    return;
  }
  const passphrase = $('#passphrase-input').val();
  const passphraseIsValid = passphrase.trim().length > 1;

  if (!passphraseIsValid) {
    $('#passphrase-input').addClass('is-invalid').addClass('is-invalid-lite');
    return false;
  }

  const encryptedPassphrase = await encryptPassword(passphrase);
  const unlockVaultRequestBody = {
    sourceId: sourceId,
    passphraseBase64: encryptedPassphrase,
  };

  // Set UI elements to indicate that we are loading
  $('#passphrase-input-fields').hide();
  $('#unlock-in-progress').show();

  const response = await sendUnlockVaultRequest(unlockVaultRequestBody);

  if (response == false) {
    // Unlock failed.
    $('#passphrase-input-fields').show();
    $('#unlock-in-progress').hide();
    $('#passphrase-input').addClass('is-invalid').addClass('is-invalid-lite');
  } else {
    // Unlock succeeded.
    isUserAuthenticated = true;

    const accessToken = response.accessToken;
    const refreshToken = response.refreshToken;

    // Store accessToken and refreshToken in a secure HttpOnly cookie
    await setTokens(accessToken, refreshToken);

    await setElements();
  }
});

$(async () => {
  await initPublic(sourceId, window.crypto);

  serverIsUp = await checkIfServerReachable();
  isUserAuthenticated = await isAuthenticated();

  await setElements();
  await ConfirmServerStatus();
  await ConfirmIsAuthenticated();
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
  } else {
    $('#connection-status-ok').hide();
    $('#connection-status-error').show();

    return;
  }

  if (hasExistingVault == null)
    hasExistingVault = await sendHasExistingVaultRequest();

  const initialSetupElement = $('#initial-setup');
  const authenticatedReadyElement = $('#authenticated-ready');
  const notAuthenticatedElement = $('#not-authenticated');
  const footerElement = $('#footer');

  if (serverIsUp && hasExistingVault) {
    // Display the default popup.
    initialSetupElement.hide();

    if (isUserAuthenticated) {
      // User is authenticated, display all elements.
      authenticatedReadyElement.show();
      notAuthenticatedElement.hide();

      $('#passphrase-input-fields').hide();
      $('#unlock-in-progress').hide();
      $('#passphrase-input')
        .removeClass('is-invalid')
        .removeClass('is-invalid-lite');

      footerElement.show();
      $('#connection-ok-icon')
        .removeClass('bi-database-fill-lock')
        .addClass('bi-database-fill-check');

      const passwordCount = await sendLoginDetailsCountRequest();
      const authenticatorCount = await sendAuthenticatorCountRequest();

      $('#vault-passwords-count').text('Number of passwords: ' + passwordCount);
      $('#vault-authenticators-count').text(
        'Number of authenticators: ' + authenticatorCount
      );
    } else {
      // User is not authenticated, display only login element.
      notAuthenticatedElement.show();
      authenticatedReadyElement.hide();
      footerElement.hide();

      $('#connection-ok-icon')
        .removeClass('bi-database-fill-check')
        .addClass('bi-database-fill-lock');
    }
  } else if (serverIsUp && !hasExistingVault) {
    // Display setup options.
    initialSetupElement.show();
    authenticatedReadyElement.hide();
  }
}

$('#set-vault-server-address-button').on('click', async function () {
  const serverAddress = $('#vault-server-address-input').val();

  if (serverAddress.trim().length < 1) {
    $('#vault-server-address-input')
      .addClass('is-invalid')
      .addClass('is-invalid-lite');
  } else {
    await setServerAddress(serverAddress);
    serverIsUp = await checkIfServerReachable();
  }
});

$('#passwords-button').on('click', function () {
  // Open the passwords page in a new tab.
  open('passwords.html');
});

$('#authenticators-button').on('click', function () {
  // Open the authenticators page in a new tab.
  open('authenticators.html');
});

$('#options-button').on('click', function () {
  // Open the options page in a new tab.
  open('options.html');
});

$('#lock-vault-button').on('click', async function () {
  // Remove tokens from secure HttpOnly cookie
  await setTokens(null, null);
  isUserAuthenticated = false;
  await setElements();

  // Make a request for the server to invalidate all tokens and close connection to database
  await sendLockVaultRequest();
});
