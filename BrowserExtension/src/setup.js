const { initPublic, isHandshakeComplete, fetchPassphrase } = require('./util/passwordUtil.js');
const { isAbsolutePathValid } = require('./util/requestsUtil.js');

// Constants
const ServerUrl = 'https://localhost:5271'; // TODO: Do not hardcode like this?
$(document).ready(async function () {
  initPublic(window.crypto);
  await waitForHandshake();

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

  $('input[type=radio][name=select-master-password]').change(function () {
    if (this.id === 'use-passkey') {
      $('#passphraseSettings').hide();
    } else if (this.id === 'use-pass-phrase') {
      $('#passphraseSettings').show();
    }
  });

  // Validate custom path when user enters input
  $('#customPath').on('input', async function () {
    var customPath = $(this).val();
    var isValid = await validatePath(customPath);
    if (isValid) {
      $('#customPath').removeClass('is-invalid').removeClass('is-invalid-lite').addClass('is-valid').addClass('is-valid-lite');
      $('#customPathError').hide();
    } else {
      $('#customPath').removeClass('is-valid').removeClass('is-valid-lite').addClass('is-invalid').addClass('is-invalid-lite');
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
  $('#initialize-vault').click(async function () {
    try {
      const usePasskey = $('#use-passkey').is(':checked');

      if (usePasskey) {
        // Check if WebAuthn is supported by the browser
        if (!window.PublicKeyCredential) {
          throw new Error('WebAuthn is not supported by this browser.');
        }

        // TODO
        let credential = await navigator.credentials.create({
          publicKey: {
            challenge: new Uint8Array([117, 61, 252, 231, 191, 241]),
            rp: { name: "Vault" },
            user: {
              id: new Uint8Array([79, 252, 83, 72, 214, 7, 89, 26]),
              name: "jamiedoe",
              displayName: "Jamie Doe"
            },
            pubKeyCredParams: [{ type: "public-key", alg: -7 }]
          }
        });

        // Handle successful registration
        console.log('Credential registered successfully:', credential);
      }
    } catch (error) {
      // Handle errors
      console.error('Error during credential registration:', error);
    }
  });
});

$('#generatePassphrase').on('click', async function () {
  try {
    // Get the value of the wordCount input field
    let wordCount = $('#wordCount').val();
    wordCount = parseInt(wordCount.match(/\d+/)[0]);

    // Generate a secure passphrase
    const passphrase = await fetchPassphrase(wordCount);

    // Set the generated passphrase to the input field
    $('#passPhraseInput').val(passphrase);
  } catch (error) {
    $('#passPhraseInput').val('Something went wrong: is the vault service running?');
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

      const receptionClasses = ['bi-reception-4', 'bi-reception-2', 'bi-reception-0'];
      const receptionClass = receptionClasses[secondsRemaining-1];

      $('#handshake-retry-icon').removeClass(receptionClasses.join(' ')).addClass(receptionClass);
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

  if (!path.startsWith('/') && !path.match(/^[a-zA-Z]:\\/)) {
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
