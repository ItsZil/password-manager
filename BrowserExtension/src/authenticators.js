const {
  initPublic,
  isHandshakeComplete,
  encryptPassword,
  decryptPassword,
} = require('./util/passwordUtil.js');

const {
  sendUnlockVaultRequest,
  sendHasExistingVaultRequest,
  sendAuthenticatorCountRequest,
  sendGetAuthenticatorViewRequest,
  sendGetAuthenticatorCodeRequest,
  sendAllLoginDetailsViewRequest,
  sendCreateAuthenticatorRequest,
  sendDeleteAuthenticatorRequest,
} = require('./util/requestsUtil.js');

const { isAuthenticated, setTokens } = require('./util/authUtil.js');

const sourceId = 1;

let authenticatorsCount = 0;
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

  if (currentPage * 10 > authenticatorsCount && authenticatorsCount > 0) {
    currentPage = Math.ceil(authenticatorsCount / 10);
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
    authenticatorsCount = await sendAuthenticatorCountRequest();

    setPageUrlParam();

    await refreshAuthenticatorsTable(currentPage);

    $('#page-loader').hide();
    $('#authenticators-options').show();
  }
}

async function refreshAuthenticatorsTable(page) {
  // Retrieve the requested batch of authenticators.
  $('#loading-authenticators-table').show();
  $('#authenticators-table').hide();

  if (authenticatorsCount > 0)
    $('#authenticators-current-min').text((page - 1) * 10 + 1);
  else $('#authenticators-current-min').text(0);

  $('#authenticators-current-max').text(
    Math.min(page * 10, authenticatorsCount)
  );
  $('#authenticators-max').text(authenticatorsCount);

  if (authenticatorsCount > 0) {
    // Populate the authenticators table
    let authenticators = await sendGetAuthenticatorViewRequest(page);
    await populateAuthenticatorsTable(page, authenticators);
  }

  $('#loading-authenticators-table').hide();
  $('#authenticators-table').show();

  $('[id^="retrieve-authenticator-code-"]').on('click', async function () {
    if ($(this).attr('role')) {
      // We are already retreving the code
      return;
    }
    const idText = $(this).attr('id');
    const id = parseInt(idText.split('-')[3]);

    // Set loading indicators
    $(this).attr('role', 'status');
    $('#retrieve-authenticator-code-' + id).removeClass('bi-arrow-clockwise');
    $('#retrieve-authenticator-code-spinner-' + id).show();

    const timestamp = new Date().toISOString();
    const timestampUri = encodeURIComponent(timestamp);
    const codeJson = await sendGetAuthenticatorCodeRequest(id, timestampUri);

    const codeInputElement = $('#authenticator-input-' + id);
    codeInputElement.val('Something went wrong.');
    if (!codeJson) {
      codeInputElement.val('Something went wrong.');
      return;
    }

    // Code is an unknown digit number not separated by anything
    // Split the code into 2 groups seperated by a ' - '
    const code = codeJson.code;
    const codeGroups = code.match(/.{1,3}/g);
    codeInputElement.val(codeGroups.join(' '));

    $(this).removeAttr('role');
    $('#retrieve-authenticator-code-' + id).addClass('bi-arrow-clockwise');
    $('#retrieve-authenticator-code-spinner-' + id).hide();

    // Update the last accessed time
    const lastAccessedTime = formatDate(new Date());
    $('#last-accessed-' + id).text(lastAccessedTime);
  });

  $('[id^="delete-authenticator-"]').on('click', async function () {
    const idText = $(this).attr('id');
    const id = parseInt(idText.split('-')[2]);

    const domain = $('#domain-' + id).text();
    const username = $('#username-' + id).text();

    $('#authenticators-deletion-id').text(id);
    $('#delete-confirm-domain-username').html(domain + '<br>' + username);
    document
      .getElementById('show-authenticators-delete-confirm-modal-button')
      .click();
  });

  $('#confirm-authenticator-deletion-button').on('click', async function () {
    const idText = $('#authenticators-deletion-id').text();
    const id = parseInt(idText);

    const deleted = await sendDeleteAuthenticatorRequest(id);
    if (deleted) {
      document.getElementById('close-delete-confirm-modal-button').click();
      await refreshAuthenticatorsTable(currentPage);
    } else {
      $('#delete-confirm-error').text(
        'Something went wrong. Please try again.'
      );
    }
  });
}

async function populateAuthenticatorsTable(page, authenticators) {
  // Set up pagination
  const totalPages = calculateTotalPages(authenticatorsCount);
  generatePagination(page, totalPages);

  // Reference to tbody element
  var tbody = $('#authenticators-tbody');

  // Clear existing rows
  tbody.empty();

  // Loop through each login detail object
  $.each(authenticators, async function (index, authenticator) {
    // Create a new row
    var row = $('<tr>');

    // Add columns to the row
    row.append('<td>' + authenticator.authenticatorId + '</td>'); // ID
    row.append(
      '<td><a id="domain-' +
        authenticator.authenticatorId +
        '" href="http://' +
        authenticator.domain +
        '" target="_blank" class="text-reset">' +
        authenticator.domain +
        '</a></td>'
    ); // Domain
    row.append(
      '<td id="username-' +
        authenticator.authenticatorId +
        '">' +
        authenticator.username +
        '</td>'
    ); // Username
    row.append(
      '<td>' +
        '<div class="input-group input-group-flat">' +
        '<input type="text" class="form-control" id="authenticator-input-' +
        authenticator.authenticatorId +
        '" readonly>' +
        '<button type="button" class="bi bi-arrow-clockwise btn" id="retrieve-authenticator-code-' +
        authenticator.authenticatorId +
        '">' +
        '<div class="spinner-border spinner-border-sm ms-0 me-0"' +
        'id = "retrieve-authenticator-code-spinner-' +
        authenticator.authenticatorId +
        '"' +
        'style = "display: none; width: 1em; height: 1em" ></div>' +
        '</button > ' +
        '</div>' +
        '</td>'
    ); // Authenticator code readonly input field and retrieval button
    row.append(
      '<td id="last-accessed-' +
        authenticator.authenticatorId +
        '">' +
        formatDate(authenticator.lastUsedDate) +
        '</td>'
    ); // Last Accessed
    row.append(
      '<td class="text-end">' +
        '<a href="#" class="me-2" id="delete-authenticator-' +
        authenticator.authenticatorId +
        '"><i class="bi bi-trash3-fill" style="color: darkred; font-size: 21px"></i></a>' +
        '</td>'
    ); // Delete button

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
        '<li class="page-item active"><a class="page-link" href="#" id="authenticators-page-' +
          i +
          '">' +
          i +
          '</a></li>'
      );
    } else {
      paginationUl.append(
        '<li class="page-item"><a class="page-link" href="#" id="authenticators-page-' +
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

$('#create-authenticator-button').on('click', async function () {
  // Clear all options except first
  const selectElement = document.getElementById('login-details');
  for (var i = selectElement.options.length - 1; i > 0; i--) {
    selectElement.remove(i);
  }

  // Reset fields
  $('#create-error-text').hide();
  $('#create-new-authenticator-secret-input').removeClass(
    'is-invalid is-invalid-lite'
  );

  document.getElementById('show-create-authenticator-modal-button').click();
  const allLoginDetails = await sendAllLoginDetailsViewRequest();
  populateLoginDetailsInModal(allLoginDetails);
});

// Function to populate the dropdown list
function populateLoginDetailsInModal(allLoginDetails) {
  var selectElement = document.getElementById('login-details');

  // Populate options from the allLoginDetails array
  allLoginDetails.forEach(function (detail) {
    var option = document.createElement('option');
    option.text = detail.domain + ' - ' + detail.username;
    option.value = detail.detailsId;
    selectElement.add(option);
  });
}

$('#create-new-authenticator-secret-input').on('input', function () {
  $('#create-new-authenticator-secret-input').removeClass(
    'is-invalid is-invalid-lite'
  );
  $('#create-error-text').hide();
});

$('#login-details').on('change', function () {
  // Remove error text if a website is selected
  const loginDetailsId = $('#login-details').val();
  if (loginDetailsId != '-1') {
    $('#create-error-text').hide();
  }
});

$('#finish-create-authenticator-button').on('click', async function () {
  $('#create-error-text').hide();

  // Ensure a login detail has been selected.
  const loginDetailsId = $('#login-details').val();
  if (loginDetailsId == '-1') {
    $('#create-error-text').text('Please select a website.');
    $('#create-error-text').show();
    return;
  }

  // Ensure the secret key is at least 8 characters long
  const secretKey = $('#create-new-authenticator-secret-input').val();
  if (secretKey.length < 8) {
    $('#create-new-authenticator-secret-input')
      .addClass('is-invalid')
      .addClass('is-invalid-lite');
    $('#create-error-text').text('Your secret key does not look right.');
    $('#create-error-text').show();
    return;
  }

  // Set up loading UI
  $('#finish-create-authenticator-button').addClass('disabled');
  $('#finish-create-authenticator-icon').hide();
  $('#finish-create-authenticator-spinner').show();

  // Create the authenticator
  const encryptedSecret = await encryptPassword(secretKey);
  const timestamp = new Date().toISOString();

  const createAuthenticatorRequestBody = {
    sourceId: sourceId,
    loginDetailsId: loginDetailsId,
    secretKey: encryptedSecret,
    timestamp: timestamp,
  };

  const createAuthenticatorResponse = await sendCreateAuthenticatorRequest(
    createAuthenticatorRequestBody
  );
  if (createAuthenticatorResponse.code == null) {
    if (createAuthenticatorResponse == 409) {
      $('#create-error-text').text(
        'You already have an authenticator for this website and username.'
      );
    } else {
      $('#create-error-text').text(
        'Something went wrong creating authenticator - please try again.'
      );
    }
    $('#create-error-text').show();
    $('#finish-create-authenticator-button').removeClass('disabled');
    $('#finish-create-authenticator-icon').show();
    $('#finish-create-authenticator-spinner').hide();
    return;
  }

  if (createAuthenticatorResponse.authenticatorId <= currentPage * 10) {
    await refreshAuthenticatorsTable(currentPage);
  }

  $('#finish-create-authenticator-button').removeClass('disabled');
  $('#finish-create-authenticator-icon').show();
  $('#finish-create-authenticator-spinner').hide();

  // Reset all fields to default values and close the modal
  $('#create-new-authenticator-secret-input').val('');
  document.getElementById('show-create-authenticator-modal-button').click();
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

  const isValid = passphrase.trim().length > 1;

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
