const {
  initPublic,
  isHandshakeComplete,
  encryptPassword,
  decryptPassword,
} = require('./util/passwordUtil.js');

const {
  generatePassword,
  domainRegisterRequest,
  sendUnlockVaultRequest,
  sendHasExistingVaultRequest,
  sendLoginDetailsCountRequest,
  sendLoginDetailsViewRequest,
  sendLoginDetailsPasswordRequest,
  sendCreatePasskeyRequest,
  sendGetPasskeyCredentialRequest,
  sendGetExtraAuthTypeRequest,
  sendSetExtraAuthTypeRequest,
  sendCreatePinRequest
} = require('./util/requestsUtil.js');

const {
  isAuthenticated,
  setTokens,
  authenticatePasskey,
} = require('./util/authUtil.js');

const sourceId = 1;

let loginDetailsCount = 0;
let currentPage = 1;

$(document).ready(async function () {
  initPublic(sourceId, window.crypto);
  await waitForHandshake();
});

function setPageUrlParam() {
  const urlParams = new URLSearchParams(window.location.search);
  const pageParam = urlParams.get('page');

  if (pageParam) {
    currentPage = parseInt(pageParam);
  }

  if (currentPage * 10 > loginDetailsCount && loginDetailsCount > 0) {
    currentPage = Math.ceil(loginDetailsCount / 10);
  }

  if (currentPage !== 1 || !urlParams.has('page')) {
    window.history.replaceState({}, '', `?page=${currentPage}`);
  }
}

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
  } else {
    // User is authenticated and has an existing vault
    $('#page-loader').show();
    $('#vault-login-modal').hide();
    $('#vault-login-modal-inner').removeClass('show');

    // Get count of login details
    loginDetailsCount = await sendLoginDetailsCountRequest();

    setPageUrlParam();

    await refreshLoginDetailsTable(currentPage);

    $('#page-loader').hide();
    $('#passwords-options').show();
  }
}

async function refreshLoginDetailsTable(page) {
  // Retrieve the requested batch of login details.
  $('#loading-passwords-table').show();
  $('#passwords-table').hide();

  let loginDetails = await sendLoginDetailsViewRequest(page);

  if (loginDetailsCount > 0)
    $('#details-current-min').text((page - 1) * 10 + 1);
  else
    $('#details-current-min').text(0);

  $('#details-current-max').text(Math.min(page * 10, loginDetailsCount));
  $('#details-max').text(loginDetailsCount);

  // Populate the login details table
  await populateLoginDetailsTable(page, loginDetails);

  $('#loading-passwords-table').hide();
  $('#passwords-table').show();

  $('[id^="save-details-"]').on('click', async function () {
    const idText = $(this).attr('id');
    const id = parseInt(idText.split('-')[2]);
    console.log('SAVE Clicked button ID:', id);
  });

  $('[id^="delete-details-"]').on('click', async function () {
    const idText = $(this).attr('id');
    const id = parseInt(idText.split('-')[2]);
    console.log('DELETE Clicked button ID:', id);
  });

  $('[id^="password-details-"]').on('click', async function () {
    const idText = $(this).attr('id');
    const id = parseInt(idText.split('-')[2]);
    console.log('DELETE Clicked button ID:', id);
  });

  $('[id^="passwords-page-"]').on('click', async function () {
    const idText = $(this).attr('id');
    const id = parseInt(idText.split('-')[2]);

    if (id != currentPage) {
      window.history.replaceState({}, '', `?page=${id}`);
      currentPage = id;
      await refreshLoginDetailsTable(id);
    }
  });

  $('[id^="password-toggle-"]').on('click', async function () {
    if ($(this).attr('role')) {
      // We are already retreving the password
      return;
    }
    const idText = $(this).attr('id');
    const id = parseInt(idText.split('-')[2]);

    if ($(this).hasClass('bi-eye')) {
      // User wants to view password
      // Remove the eye icon and replace it with a spinner
      $(this)
        .removeClass('bi-eye')
        .addClass('spinner-border spinner-border-sm text-secondary');
      $(this).attr('role', 'status');

      const domainLoginPasswordRequestBody = {
        sourceId: sourceId,
        loginDetailsId: id,
      };
      const encryptedPasswordB64 = await sendLoginDetailsPasswordRequest(
        domainLoginPasswordRequestBody
      );
      const decryptedPassword = await decryptPassword(encryptedPasswordB64);

      // Replace the password with the decrypted password
      $('#password-input-' + id).val(decryptedPassword);
      $('#password-input-' + id).attr('type', 'text');

      // Replace the spinner with the eye icon
      $(this)
        .removeClass('spinner-border spinner-border-sm text-secondary')
        .addClass('bi-eye-slash');
      $(this).removeAttr('role');
    } else {
      // Hide the password
      $('#password-input-' + id).attr('type', 'password');
      $('#password-input-' + id).val('******************************');

      // Replace the eye-slash icon with the eye icon
      $(this).removeClass('bi-eye-slash').addClass('bi-eye');
    }
  });
}

async function populateLoginDetailsTable(page, loginDetails) {
  // Set up pagination
  const totalPages = calculateTotalPages(loginDetailsCount);
  generatePagination(page, totalPages);

  // Reference to tbody element
  var tbody = $('#login-details-tbody');

  // Clear existing rows
  tbody.empty();

  const extraAuthTypes = ['None', 'PIN', 'Passkey', 'Passphrase']

  // Array to store promises for extraAuthType requests
  const extraAuthPromises = [];

  // Loop through each login detail object
  loginDetails.forEach(loginDetail => {
    // Get the extra auth type and push the promise to the array
    extraAuthPromises.push(sendGetExtraAuthTypeRequest(loginDetail.detailsId));
  });

  // Wait for all extraAuthType requests to complete
  const extraAuthResults = await Promise.all(extraAuthPromises);

  // Loop through each login detail object
  $.each(loginDetails, async function (index, loginDetail) {
    // Create a new row
    var row = $('<tr>');

    // Add columns to the row
    row.append('<td>' + loginDetail.detailsId + '</td>'); // ID
    row.append(
      '<td><a href="http://' +
        loginDetail.domain +
        '" target="_blank" class="text-reset">' +
        loginDetail.domain +
        '</a></td>'
    ); // Domain
    row.append(
      '<td>' +
        '<input type="text" class="form-control" id="username-' +
        loginDetail.detailsId +
        '-input" value="' +
        loginDetail.username +
        '">' +
        '</td>'
    ); // Username input field
    row.append(
      '<td>' +
        '<div class="input-group input-group-flat">' +
        '<input type="password" class="form-control" id="password-input-' +
        loginDetail.detailsId +
        '" value="******************************" readonly>' +
        '<span class="input-group-text">' +
        '<a href="#" class="link-secondary bi-eye" id="password-toggle-' +
        loginDetail.detailsId +
        '"></a>' +
        '</span>' +
        '</div>' +
        '</td>'
    ); // Password input field and eye icon
    // Extra auth type
    const selectId = 'extra-auth-select-' + loginDetail.detailsId;
    const extraAuthValue = Array.isArray(extraAuthResults[index]) ? extraAuthResults[index] : [extraAuthResults[index]]; // Ensure extraAuthValue is an array
    const selectOptions = extraAuthTypes.map(type => `<option ${extraAuthValue.includes(type) ? 'selected' : ''}>${type}</option>`).join('');
    row.append(
      '<td>' +
      `<select id="${selectId}" class="form-select">${selectOptions}</select>` +
      '</td>'
    ); // Extra auth select dropdown
    row.append('<td>' + formatDate(loginDetail.lastUsedDate) + '</td>'); // Last Accessed
    row.append(
      '<td class="text-end">' +
        '<a href="#" class="me-2" id="delete-details-' +
        loginDetail.detailsId +
        '"><i class="bi bi-trash3-fill" style="color: darkred; font-size: 21px"></i></a>' +
        '<a href="#" id=save-details-' +
        loginDetail.detailsId +
        '"><i class="bi bi-floppy-fill" style="color: #0054a6; font-size: 21px"></i></a>' +
        '</td>'
    ); // Action buttons

    // Append the row to the tbody
    tbody.append(row);
  });
}

// Function to calculate total number of pages
function calculateTotalPages(totalCount) {
  return Math.ceil(totalCount / 10);
}

// Function to generate pagination links
function generatePagination(page, totalPages) {
  // Reference to pagination ul element
  var paginationUl = $('#pagination');

  // Clear existing pagination links
  paginationUl.empty();

  // Add previous page link
  paginationUl.append(
    '<li class="page-item"><a class="page-link" href="#" tabindex="-1"><i class="bi bi-chevron-left"></i></a></li>'
  );

  // Add page links
  for (var i = 1; i <= totalPages; i++) {
    if (i === page) {
      paginationUl.append(
        '<li class="page-item active"><a class="page-link" href="#" id="passwords-page-' +
          i +
          '">' +
          i +
          '</a></li>'
      );
    } else {
      paginationUl.append(
        '<li class="page-item"><a class="page-link" href="#" id="passwords-page-' +
          i +
          '">' +
          i +
          '</a></li>'
      );
    }
  }

  // Add next page link
  paginationUl.append(
    '<li class="page-item"><a class="page-link" href="#"><i class="bi bi-chevron-right"></i></a></li>'
  );
}

function formatDate(dateString) {
  // Format to YYYY-MM-DD HH:MM:SS
  var date = new Date(dateString);
  var formattedDate = date.toLocaleString('lt-LT', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
  return formattedDate;
}

$('#generate-new-details-password').on('click', async function () {
  const generatedEncryptedPassword = await generatePassword(sourceId);
  const decryptedPassword = await decryptPassword(generatedEncryptedPassword);

  $('#create-new-details-password-input').removeClass(
    'is-invalid is-invalid-lite'
  );
  $('#create-new-details-password-input').val(decryptedPassword);
});

$('#creation-extra-auth-selection').on('change', function () {
  const selectedValue = $(this).val();

  switch (selectedValue) {
    case 'pin-extra-auth':
      $('#extra-auth-pin-setup').show();
      break;
    default:
      $('#extra-auth-pin-setup').hide();
      break;
  }
  $('#create-error-text').hide();
});

$('#extra-auth-pin-input').on('input', function () {
  // Remove non-digit characters from input
  var inputValue = $(this).val().replace(/\D/g, '');

  // Update input value with only digits
  $(this).val(inputValue);
  $('#extra-auth-pin-input').removeClass('is-invalid is-invalid-lite');
  $('#create-error-text').hide();
});

$('#create-new-details-domain-input').on('input', function () {
  $('#create-new-details-domain-input').removeClass(
    'is-invalid is-invalid-lite'
  );
  $('#create-error-text').hide();
});

$('#create-new-details-username-input').on('input', function () {
  $('#create-new-details-username-input').removeClass(
    'is-invalid is-invalid-lite'
  );
  $('#create-error-text').hide();
});

async function setupPasskey(loginDetailsId, domain) {
  const randomChallenge = window.crypto.getRandomValues(new Uint8Array(16));
  const randomUserId = window.crypto.getRandomValues(new Uint8Array(16));

  const publicKeyCredentialCreationOptions = {
    challenge: randomChallenge,
    rp: {
      name: 'Password Manager Vault',
    },
    user: {
      id: randomUserId,
      name: 'Vault Authentication: ' + domain,
      displayName: 'Vault Authentication: ' + domain,
    },
    pubKeyCredParams: [
      { alg: -7, type: 'public-key' },
      { alg: -257, type: 'public-key' },
    ],
    userVerifiation: 'required',
  };

  let credential;
  try {
    credential = await navigator.credentials.create({
      publicKey: publicKeyCredentialCreationOptions,
    });
  } catch (error) {
    console.error('Error creating passkey credential:', error);
    return false;
  }

  const credentialPublicKey = credential.response.getPublicKey();
  const algorithmId = credential.response.getPublicKeyAlgorithm();

  // Base64 encoded values to store in database
  const userIdBase64 = btoa(
    String.fromCharCode.apply(null, new Uint8Array(randomUserId))
  );
  const credentialIdBase64 = credential.id;
  const randomChallengeBase64 = btoa(
    String.fromCharCode.apply(null, new Uint8Array(randomChallenge))
  );
  const credentialPublicKeyBase64 = btoa(
    String.fromCharCode.apply(null, new Uint8Array(credentialPublicKey))
  );

  // Create the PasskeyCreationRequest object
  const encryptedChallenge = await encryptPassword(randomChallengeBase64);
  const createPasskeyRequestBody = {
    sourceId: sourceId,
    userId: userIdBase64,
    credentialId: credentialIdBase64,
    publicKey: credentialPublicKeyBase64,
    challenge: encryptedChallenge,
    loginDetailsId: loginDetailsId,
    algorithmId: algorithmId,
  };

  // Send the passkey to the server for storage
  const passkeySaved = await sendCreatePasskeyRequest(createPasskeyRequestBody);
  if (!passkeySaved) {
    $('#create-error-text').text(
      'Something went wrong setting up your passkey. Your login details have been created, edit them to try again.'
    );
    $('#create-error-text').show();

    return false;
  }
  return true;
}

// Function to start the passkey authentication process
async function startPasskeyAuth(loginDetailsId) {
  // Retrieve the passkey credential from the server and decrypt the challenge
  const passkeyCredential = await sendGetPasskeyCredentialRequest(
    sourceId,
    loginDetailsId
  );
  if (!passkeyCredential) return false;

  // Authenticate the passkey credential
  const challenge = await decryptPassword(passkeyCredential.challenge);
  await authenticatePasskey(passkeyCredential, challenge, loginDetailsId);
}

function parseDomain() {
  // Verify domain
  let domain = $('#create-new-details-domain-input').val().trim();
  if (!domain || !domain.includes('.')) {
    $('#create-new-details-domain-input').addClass(
      'is-invalid is-invalid-lite'
    );

    $('#create-error-text').text('Please enter a valid domain.');
    $('#create-error-text').show();
    return false;
  }
  // Parse domain
  domain = domain.replace(/^(https?:\/\/)?(www\.)?/, ''); // Remove protocol
  domain = domain.replace(/\/[^\/]*$/, ''); // Remove everything after /
  domain = domain.replace(/^[^a-zA-Z0-9]*/, ''); // Remove non-letter and non-number characters at the beginning
  $('#create-new-details-domain-input').val(domain);

  return domain;
}

$('#finish-create-details-button').on('click', async function () {
  $('#create-error-text').hide();
  const domain = parseDomain();

  const username = $('#create-new-details-username-input').val();
  if (username.length < 1) {
    $('#create-new-details-username-input').addClass(
      'is-invalid is-invalid-lite'
    );

    $('#create-error-text').text('Please enter a valid username.');
    $('#create-error-text').show();
    return;
  }

  // Ensure password is at least 8 characters long
  let password = $('#create-new-details-password-input').val();
  if (password.length < 8) {
    $('#create-new-details-password-input').addClass(
      'is-invalid is-invalid-lite'
    );

    $('#create-error-text').text(
      'Your password must be at least 8 characters long.'
    );
    $('#create-error-text').show();
    return;
  }
  const pinInput = $('#extra-auth-pin-input').val();
  if (pinInput.length !== 4) {
    $('#extra-auth-pin-input').addClass('is-invalid is-invalid-lite');

    $('#create-error-text').text('Your PIN code must be exactly 4 digits.');
    $('#create-error-text').show();
    return;
  }

  // Set up loading UI
  $('#finish-create-details-button').addClass('disabled');
  $('#finish-create-details-icon').hide();
  $('#finish-create-details-spinner').show();

  // We need to create the LoginDetails first in order to link the extra authentication to it.
  const encryptedPassword = await encryptPassword(password);
  const domainRegisterRequestBody = {
    sourceId: sourceId,
    domain: domain,
    username: 'student',
    password: encryptedPassword,
  };

  const createdDetails = await domainRegisterRequest(domainRegisterRequestBody);
  if (createdDetails.id == null) {
    if (createdDetails == 409) {
      $('#create-error-text').text(
        'You already have login details for this website and username.'
      );
    } else if (createdDetails != 200) {
      $('#create-error-text').text(
        'Something went wrong creating login details - please try again.'
      );
    }
    $('#create-error-text').show();
    $('#finish-create-details-button').removeClass('disabled');
    $('#finish-create-details-icon').show();
    $('#finish-create-details-spinner').hide();

    return;
  }

  // Process extra authentication on autofill
  const selectedExtraAuth = $('#creation-extra-auth-selection').val();
  let extraAuthSetupResult = true;
  switch (selectedExtraAuth) {
    case 'pin-extra-auth':
      // Retrieve the PIN input value
      const pinInput = $('#extra-auth-pin-input').val();

      // Create the PIN code in the database
      const encryptedPin = await encryptPassword(pinInput);
      const createPinRequestBody = {
        sourceId: sourceId,
        loginDetailsId: createdDetails.id,
        pinCode: encryptedPin,
      };

      const createdPin = await sendCreatePinRequest(createPinRequestBody);
      extraAuthSetupResult = await sendSetExtraAuthTypeRequest(createdDetails.id, 2);

      break;
    case 'passkey-extra-auth':
      const passkeySetupResult = await setupPasskey(
        createdDetails.id,
        createdDetails.domain
      );

      extraAuthSetupResult = await sendSetExtraAuthTypeRequest(createdDetails.id, 3);
      break;
    case 'passphrase-extra-auth':
      extraAuthSetupResult = await sendSetExtraAuthTypeRequest(createdDetails.id, 4);
      break;
  }

  if (createdDetails.id <= currentPage * 10) {
    await refreshLoginDetailsTable(currentPage);
  }

  $('#finish-create-details-button').removeClass('disabled');
  $('#finish-create-details-icon').show();
  $('#finish-create-details-spinner').hide();

  // Reset all fields to default values and close the modal
  $('#create-new-details-domain-input').val('');
  $('#create-new-details-username-input').val('');
  $('#create-new-details-password-input').val('');
  $('#create-details-modal-close').click();

  if (!extraAuthSetupResult) {
    // Something went wrong setting up extra authentication, show an error modal
    document.getElementById('show-extra-auth-failed-modal-button').click();
  }
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
