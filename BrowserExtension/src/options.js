const {
  initPublic,
  isHandshakeComplete,
  encryptPassword,
  fetchPassphrase,
} = require('./util/passwordUtil.js');

const {
  sendUnlockVaultRequest,
  sendHasExistingVaultRequest,
  sendUpdateVaultPassphraseRequest,
  sendLockVaultRequest,
  sendExportVaultRequest,
  isAbsolutePathValid,
  sendGetVaultInternetAccessRequest,
  sendSetVaultInternetAccessRequest,
} = require('./util/requestsUtil.js');

const { isAuthenticated, setTokens } = require('./util/authUtil.js');

const sourceId = 1;

$(document).ready(async function () {
  initPublic(sourceId, window.crypto);

  await waitForHandshake();
});

async function setElements() {
  const isAuthenticatedResult = await isAuthenticated();
  const hasExistingVault = await sendHasExistingVaultRequest();
  const handshakeComplete = isHandshakeComplete();

  if (!handshakeComplete) return;

  if (!isAuthenticatedResult && hasExistingVault) {
    // Show login modal
    $('#vault-login-modal').show();
    $('#vault-login-modal-inner').addClass('show');
  } else if (!hasExistingVault) {
    // Open setup
    window.location.replace('./setup.html');
  } else if (isAuthenticatedResult && hasExistingVault) {
    // User is authenticated and has an existing vault
    // Set the vault internet access switch
    const vaultInternetAccess = await sendGetVaultInternetAccessRequest();
    $('#vault-internet-access-checkbox').prop('checked', vaultInternetAccess);

    $('#vault-login-modal').hide();
    $('#vault-login-modal-inner').removeClass('show');
    $('#page-loader').hide();
  }
}

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

  const words = passphrase.split(' ');
  const isValid =
    words.length >= 4 &&
    words.length <= 10 &&
    words.every((word) => word === word.toLowerCase());

  if (!isValid) {
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
    const accessToken = response.accessToken;
    const refreshToken = response.refreshToken;

    // Store accessToken and refreshToken in a secure HttpOnly cookie
    setTokens(accessToken, refreshToken);

    await setElements();
  }
});

// Function to wait for handshake to complete and show the appropriate UI
async function waitForHandshake(secondsRemaining = 3) {
  // Wait 100ms before checking if handshake is complete
  await new Promise((resolve) => setTimeout(resolve, 100));

  if (!isHandshakeComplete()) {
    $('#page-loader').hide();
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

    await setElements();
  }
}

// Passphrase word count
var wordCount = 4;

// Update word count input value
function updateWordCount() {
  $('#wordCount').val('Word count: ' + wordCount);
}

// Decrease word count
$('#decreaseWordCount').click(function () {
  if (wordCount > 4) {
    wordCount--;
    updateWordCount();
  }
});

// Increase word count
$('#increaseWordCount').click(function () {
  if (wordCount < 10) {
    wordCount++;
    updateWordCount();
  }
});

$('#generatePassphrase').on('click', async function () {
  try {
    // Get the value of the wordCount input field
    let wordCount = $('#wordCount').val();
    wordCount = parseInt(wordCount.match(/\d+/)[0]);

    // Generate a secure passphrase
    const passphrase = await fetchPassphrase(sourceId, wordCount);

    // Set the generated passphrase to the input field
    $('#passPhraseInput').val(passphrase);
    $('#passPhraseInput').removeClass('is-invalid').addClass('is-invalid-lite');
  } catch (error) {
    $('#passPhraseInput').val(
      'Something went wrong: is the vault service running?'
    );
  }
});

$('#passPhraseInput').on('input', function () {
  $('#passPhraseInput').removeClass('is-invalid').addClass('is-invalid-lite');
});

$('#save-new-passphrase-button').on('click', async function () {
  const passPhrase = $('#passPhraseInput').val();
  const passPhraseIsEmpty = passPhrase.trim().length == 0;
  const passPhraseIsNotValid =
    passPhrase.split(' ').length < 4 || passPhrase.split(' ').length > 10;
  if (passPhraseIsEmpty || passPhraseIsNotValid) {
    $('#passPhraseInput').addClass('is-invalid').addClass('is-invalid-lite');
    return;
  }

  const vaultKey = await encryptPassword(passPhrase);
  const updateVaultPassphraseRequest = {
    sourceId: sourceId,
    vaultRawKeyBase64: vaultKey,
  };

  document
    .getElementById('show-passphrase-update-progresss-modal-button')
    .click();

  const updated = await sendUpdateVaultPassphraseRequest(
    updateVaultPassphraseRequest
  );
  if (updated) {
    document
      .getElementById('show-passphrase-update-progresss-modal-button')
      .click();

    // Log out the user.
    setTokens(null, null);

    // Reload the page so they are prompted to login.
    location.reload();
  } else {
    // Show failure UI
    document
      .getElementById('show-passphrase-update-progresss-modal-button')
      .click();
    document
      .getElementById('show-passphrase-update-failure-modal-button')
      .click();
  }
});

$('#confirm-log-out-button').on('click', async function () {
  // Remove tokens from secure HttpOnly cookie
  setTokens(null, null);

  // Make a request for the server to invalidate all tokens and close connection to database
  await sendLockVaultRequest();

  // Reload the page so they are prompted to login.
  location.reload();
});

$('#vaultPathInput').on('input', function () {
  $('#vaultPathError').hide();
  $('#vaultPathInput').removeClass('is-invalid').removeClass('is-invalid-lite');
});

$('#export-vault-button').on('click', async function () {
  const path = $('#vaultPathInput').val();
  const isPathValid = await validatePath(path);
  if (!isPathValid) {
    $('#vaultPathInput').addClass('is-invalid').addClass('is-invalid-lite');
    return;
  }

  document.getElementById('show-export-vault-progresss-modal-button').click();

  const exportVaultFolder = encodeURIComponent(path);
  const exportVaultRequest = {
    absolutePathUri: exportVaultFolder,
  };

  const exportVaultResponse = await sendExportVaultRequest(exportVaultRequest);
  document.getElementById('show-export-vault-progresss-modal-button').click();

  if (exportVaultResponse) {
    // Success, show a modal with the path to the exported vault
    const pathToExportedVault = decodeURIComponent(
      exportVaultResponse.absolutePathUri
    );
    $('#exported-vault-success-path').text(pathToExportedVault);
    document.getElementById('show-vault-export-success-modal-button').click();
  } else {
    // Show a failure modal
    document.getElementById('show-vault-export-failure-modal-button').click();
  }
});

// Function to validate path
async function validatePath(path) {
  if (!path || typeof path !== 'string') {
    $('#vaultPathError').hide();
    return false; // Path is empty or not a string
  }

  path = path.trim();

  if (!path.match(/^[a-zA-Z]:\\/)) {
    $('#vaultPathError').hide();
    return false; // Path is not absolute
  }

  // Check if path exists
  const absolutePathUri = encodeURIComponent(path);
  const isPathValid = await isAbsolutePathValid(absolutePathUri);
  if (!isPathValid) {
    $('#vaultPathError').show();
  }
  return isPathValid;
}

$('#vault-internet-access-checkbox').on('change', async function () {
  if ($(this).is(':checked')) {
    // Checked, enable internet access
    document.getElementById('show-enable-internet-access-modal-button').click();
  } else {
    // Not checked, disable internet access
    const disabledSuccessfully = await sendSetVaultInternetAccessRequest(false);
    $('#vault-internet-access-checkbox').prop('checked', !disabledSuccessfully);
  }
});

$('#confirm-enable-internet-access-button').on('click', async function () {
  const enabledSuccessfully = await sendSetVaultInternetAccessRequest(true);
  $('#vault-internet-access-checkbox').prop('checked', enabledSuccessfully);
  document.getElementById('show-enable-internet-access-modal-button').click();
});
