const {
  initPublic,
  isHandshakeComplete,
  fetchPassphrase,
  encryptPassword,
} = require('./util/passwordUtil.js');

const {
  setServerAddress,
  isAbsolutePathValid,
  sendSetupVaultRequest,
  domainRegisterRequest,
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

  // Show/hide custom path input based on radio button selection
  $('input[type=radio][name=select-vault-location]').change(function () {
    if (this.id === 'custom-path-location') {
      $('#customPathInput').show();
      $('#customPathValidationLabel').show();
      $('#customPathTooltip').show();
    } else if (this.id === 'my-documents-location') {
      $('#customPathInput').hide();
      $('#customPathValidationLabel').hide();
      $('#customPathTooltip').hide();
    }
  });

  // Validate custom path when user enters input
  $('#customPath').on('input', async function () {
    var customPath = $(this).val();
    var isValid = await validatePath(customPath);
    if (isValid) {
      $('#customPath')
        .removeClass('is-invalid')
        .removeClass('is-invalid-lite')
        .addClass('is-valid')
        .addClass('is-valid-lite');
      $('#customPathError').hide();
    } else {
      $('#customPath')
        .removeClass('is-valid')
        .removeClass('is-valid-lite')
        .addClass('is-invalid')
        .addClass('is-invalid-lite');
    }
  });

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

  // Complete setup button
  $('#initialize-vault').on('click', async function () {
    // Retrieve vault location
    let vaultLocation;
    const useCustomPath = $('#custom-path-location').is(':checked');
    if (useCustomPath) {
      const customPath = $('#customPath').val();
      const isValid = await validatePath(customPath);
      if (isValid) {
        vaultLocation = encodeURIComponent(customPath);
      }
    }

    // Use the pass phrase or random password as the pragma key
    const passPhrase = $('#passPhraseInput').val();
    const passPhraseIsEmpty = passPhrase.trim().length == 0;
    if (passPhraseIsEmpty) return;

    const vaultKey = await encryptPassword(passPhrase);

    const setupVaultRequestBody = {
      sourceId: sourceId,
      absolutePathUri: vaultLocation,
      vaultRawKeyBase64: vaultKey,
    };

    $('#vault-creation-progress-modal').show();

    const tokenResponse = await sendSetupVaultRequest(setupVaultRequestBody);
    if (tokenResponse) {
      // Show success UI
      $('#vault-creation-progress-modal').hide();
      $('#page-title').text('Your vault is ready');

      $('#setup-step').removeClass('active');
      $('#done-step').addClass('active');

      $('#setup-fields').hide();
      $('#setup-complete-message').show();

      $('#import-hint').hide();

      const accessToken = tokenResponse.accessToken;
      const refreshToken = tokenResponse.refreshToken;

      await setTokens(accessToken, refreshToken);
    } else {
      // Show failure UI
      $('#vault-creation-progress-modal').hide();
      $('#vault-creation-failure-modal').show();
    }
  });
});

$('#create-test-details').on('click', async function () {
  let password = 'Password123';
  const encryptedPassword = await encryptPassword(password);
  const domainRegisterRequestBody = {
    sourceId: sourceId,
    domain: 'practicetestautomation.com',
    username: 'student',
    password: encryptedPassword,
  };
  await domainRegisterRequest(domainRegisterRequestBody);
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
  } catch (error) {
    $('#passPhraseInput').val(
      'Something went wrong: is the vault service running?'
    );
  }
});

$('#set-vault-server-address-button').on('click', async function () {
  const serverAddress = $('#vault-server-address-input').val();

  if (serverAddress.trim().length < 1) {
    $('#vault-server-address-input')
      .addClass('is-invalid')
      .addClass('is-invalid-lite');
  }
  else {
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
    $('#customPathError').hide();
    return false; // Path is empty or not a string
  }

  path = path.trim();

  if (!path.match(/^[a-zA-Z]:\\/)) {
    $('#customPathError').hide();
    return false; // Path is not absolute
  }

  // Check if path exists
  const absolutePathUri = encodeURIComponent(path);
  const isPathValid = await isAbsolutePathValid(absolutePathUri);
  if (!isPathValid) {
    $('#customPathError').show();
  }
  return isPathValid;
}

$('#restart-setup-button').on('click', function () {
  // Reload the page
  location.reload();
});
