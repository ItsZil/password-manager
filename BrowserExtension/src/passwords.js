const {
  initPublic,
  isHandshakeComplete,
  encryptPassword,
  decryptPassword
} = require('./util/passwordUtil.js');

const {
  domainRegisterRequest,
  sendUnlockVaultRequest,
  sendHasExistingVaultRequest,
  sendLoginDetailsCountRequest,
  sendLoginDetailsViewRequest,
  sendLoginDetailsPasswordRequest
} = require('./util/requestsUtil.js');

const { isAuthenticated, setTokens } = require('./util/authUtil.js');

const sourceId = 2;

let loginDetailsCount = 0;

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
  } else if (!hasExistingVault) {
    // Open setup
    window.location.replace('./setup.html');
  } else {
    // User is authenticated and has an existing vault
    $('#page-loader').show();
    $('#vault-login-modal').hide();

    // Get count of login details
    loginDetailsCount = await sendLoginDetailsCountRequest();

    await refreshLoginDetailsTable(1);

    $('#page-loader').hide();
    $('#passwords-options').show();
  }
}

async function refreshLoginDetailsTable(page) {
  // Retrieve the requested batch of login details.
  let loginDetails = await sendLoginDetailsViewRequest(page);

  // Duplicate the login details array elements by 20
  loginDetails = loginDetails.concat(loginDetails);
  loginDetails = loginDetails.concat(loginDetails);
  loginDetails = loginDetails.concat(loginDetails);
  loginDetails = loginDetails.concat(loginDetails);

  // Delete any elements past 10
  loginDetails.splice(10);

  loginDetailsCount = loginDetails.length;


  $('#details-current-min').text((page - 1) * 10 + 1);
  $('#details-current-max').text(Math.min(page * 10, loginDetailsCount));
  $('#details-max').text(loginDetailsCount);

  // Populate the login details table
  populateLoginDetailsTable(page, loginDetails);

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
      $(this).removeClass('bi-eye').addClass('spinner-border spinner-border-sm text-secondary');
      $(this).attr('role', 'status');

      const domainLoginPasswordRequestBody = {
        sourceId: sourceId,
        loginDetailsId: id
      }
      const encryptedPasswordB64 = await sendLoginDetailsPasswordRequest(domainLoginPasswordRequestBody);
      const decryptedPassword = await decryptPassword(encryptedPasswordB64);

      // Replace the password with the decrypted password
      $('#password-input-' + id).val(decryptedPassword);
      $('#password-input-' + id).attr('type', 'text');

      // Replace the spinner with the eye icon
      $(this).removeClass('spinner-border spinner-border-sm text-secondary').addClass('bi-eye-slash');
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

function populateLoginDetailsTable(page, loginDetails) {
  // Set up pagination
  const totalPages = calculateTotalPages(loginDetailsCount);
  generatePagination(page, totalPages);

  // Reference to tbody element
  var tbody = $('#login-details-tbody');

  // Clear existing rows
  tbody.empty();

  // Loop through each login detail object
  $.each(loginDetails, function (index, loginDetail) {
    // Create a new row
    var row = $('<tr>');

    // Add columns to the row
    row.append('<td>' + loginDetail.detailsId + '</td>'); // ID
    row.append(
      '<td><a href="http://' + loginDetail.domain + '" target="_blank" class="text-reset">' +
        loginDetail.domain +
        '</a></td>'
    ); // Domain
    row.append(
      '<td>' +
      '<input type="text" class="form-control" id="username-' +
      loginDetail.detailsId +
      '-input" value="' + loginDetail.username + '">' +
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
    row.append('<td> ' + 'TODO' + '</td>'); // Extra auth
    row.append('<td>' + formatDate(loginDetail.lastUsedDate) + '</td>'); // Last Accessed
    row.append(
      '<td class="text-end">' +
        '<a href="#" class="me-2" id="delete-details-' + loginDetail.detailsId + '"><i class="bi bi-trash3-fill" style="color: darkred; font-size: 21px"></i></a>' +
        '<a href="#" id=save-details-' + loginDetail.detailsId + '"><i class="bi bi-floppy-fill" style="color: #0054a6; font-size: 21px"></i></a>' +
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
  paginationUl.append('<li class="page-item"><a class="page-link" href="#" tabindex="-1"><i class="bi bi-chevron-left"></i></a></li>');

  // Add page links
  for (var i = 1; i <= totalPages; i++) {
    if (i === page) {
      paginationUl.append('<li class="page-item active"><a class="page-link" href="#">' + i + '</a></li>');
    } else {
      paginationUl.append('<li class="page-item"><a class="page-link" href="#">' + i + '</a></li>');
    }
  }

  // Add next page link
  paginationUl.append('<li class="page-item"><a class="page-link" href="#"><i class="bi bi-chevron-right"></i></a></li>');
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
