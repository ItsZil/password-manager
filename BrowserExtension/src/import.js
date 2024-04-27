const {
  initPublic,
  isHandshakeComplete,
  encryptPassword,
} = require('./util/passwordUtil.js');
const {
  setServerAddress,
  isAbsolutePathValid,
  sendSetupVaultRequest,
  sendHasExistingVaultRequest,
} = require('./util/requestsUtil.js');

const { isAuthenticated, setTokens } = require('./util/authUtil.js');

const sourceId = Math.floor(Math.random() * 1000000);

$(document).ready(async function () {
  initPublic(sourceId, window.crypto);
  await waitForHandshake();

  const isAuthenticatedResult = await isAuthenticated();
  const hasExistingVault = await sendHasExistingVaultRequest();
  if (isAuthenticatedResult && hasExistingVault) {
    // Open the options page
    chrome.runtime.openOptionsPage();

    // Close the setup page
    window.close();
  }

  // Validate custom path when user enters input
  $('#vaultPathInput').on('input', async function () {
    var vaultPath = $(this).val();
    var isValid = await validatePath(vaultPath);
    if (isValid) {
      $('#vaultPathInput')
        .removeClass('is-invalid')
        .removeClass('is-invalid-lite')
        .addClass('is-valid')
        .addClass('is-valid-lite');
      $('#vaultPathError').hide();
    } else {
      $('#vaultPathInput')
        .removeClass('is-valid')
        .removeClass('is-valid-lite')
        .addClass('is-invalid')
        .addClass('is-invalid-lite');
    }
  });

  // Validate pass phrase when user enters input. Has to be 4 - 10 words and all lowercase
  $('#passPhraseInput').on('input', function () {
    const passphrase = $(this).val();
    const isValid = validatePassphrase(passphrase);
    if (isValid) {
      $('#passPhraseInput')
        .removeClass('is-invalid')
        .removeClass('is-invalid-lite')
        .addClass('is-valid')
        .addClass('is-valid-lite');
    } else {
      $('#passPhraseInput')
        .removeClass('is-valid')
        .removeClass('is-valid-lite')
        .addClass('is-invalid')
        .addClass('is-invalid-lite');
    }
  });

  // Import button
  $('#import-vault').on('click', async function () {
    // Retrieve vault location
    let vaultPath = $('#vaultPathInput').val();
    const isValid = await validatePath(vaultPath);
    if (isValid) {
      vaultPath = encodeURIComponent(vaultPath);
    } else {
      return;
    }

    // Use the pass phrase or random password as the pragma key
    const passPhrase = $('#passPhraseInput').val();
    if (!validatePassphrase(passPhrase)) {
      return;
    }
    const vaultKey = await encryptPassword(passPhrase);

    const importVaultRequestBody = {
      absolutePathUri: vaultPath,
      vaultRawKeyBase64: vaultKey,
    };

    $('#vault-import-progress-modal').show();

    const importSucceeded = await sendSetupVaultRequest(importVaultRequestBody);
    if (importSucceeded) {
      // Show success UI
      $('#vault-import-progress-modal').hide();
      $('#page-title').text('Your vault is ready');

      $('#setup-step').removeClass('active');
      $('#done-step').addClass('active');

      $('#setup-fields').hide();
      $('#setup-complete-message').show();

      const accessToken = tokenResponse.accessToken;
      const refreshToken = tokenResponse.refreshToken;

      await setTokens(accessToken, refreshToken);
    } else {
      // Show failure UI
      $('#vault-import-progress-modal').hide();
      $('#vault-import-failure-modal').show();
    }
  });
});

$('#set-vault-server-address-button').on('click', async function () {
  const serverAddress = $('#vault-server-address-input').val();

  if (serverAddress.trim().length < 1) {
    $('#vault-server-address-input')
      .addClass('is-invalid')
      .addClass('is-invalid-lite');
  } else {
    await setServerAddress(serverAddress);
  }
});

// Function to wait for handshake to complete and show the appropriate UI
async function waitForHandshake(secondsRemaining = 3) {
  // Wait 100ms before checking if handshake is complete
  await new Promise((resolve) => setTimeout(resolve, 100));

  if (!isHandshakeComplete()) {
    $('#initial-wait-for-handshake').hide();
    $('#handshake-complete').hide();
    $('#waiting-for-handshake').show();
    $('#handshake-retry-text').text(`Retrying in ${secondsRemaining} seconds`);

    if (secondsRemaining === 0) {
      setTimeout(waitForHandshake, 0); // Retry immediately
    } else {
      setTimeout(() => waitForHandshake(secondsRemaining - 1), 1000); // Wait for 1 second and retry

      const receptionClasses = [
        'bi-reception-4',
        'bi-reception-2',
        'bi-reception-0',
      ];
      const receptionClass = receptionClasses[secondsRemaining - 1];

      $('#handshake-retry-icon')
        .removeClass(receptionClasses.join(' '))
        .addClass(receptionClass);
    }
  } else {
    $('#initial-wait-for-handshake').hide();
    $('#waiting-for-handshake').hide();
    $('#handshake-complete').show();
  }
}

// Function to validate path
async function validatePath(path) {
  if (!path || typeof path !== 'string') {
    $('#vaultPathError').hide();
    return false; // Path is empty or not a string
  }

  path = path.trim();

  if (!path.match(/^[a-zA-Z]:\\/) || !path.endsWith('.db')) {
    $('#vaultPathError').hide();
    return false; // Path is not correct
  }

  // Check if path exists
  const absolutePathUri = encodeURIComponent(path);
  const isPathValid = await isAbsolutePathValid(absolutePathUri);
  if (!isPathValid) {
    $('#vaultPathError').show();
  }
  return isPathValid;
}

function validatePassphrase(passphrase) {
  return passphrase.length < 1;
}

$('#restart-import-button').on('click', function () {
  // Reload the page
  location.reload();
});
