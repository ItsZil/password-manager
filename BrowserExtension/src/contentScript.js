'use strict';

const { getUserPasskeyCredentials } = require('./util/authUtil.js');
const { str2ab } = require('./util/passwordUtil.js');

// Function to check if an element is an input field
function isInputField(element) {
  return element.tagName === 'INPUT' || element.tagName === 'TEXTAREA';
}

function getAllInputFields() {
  const inputFields = document.querySelectorAll(
    'input[type="email"], input[type="password"], input[type="text"], input[type="tel"], textarea'
  );

  if (inputFields.length > 0) {
    // Extract information about input fields
    const inputFieldInfo = Array.from(inputFields).map((inputField) => ({
      type: inputField.type,
      id: inputField.id,
      name: inputField.name,
      value: inputField.value,
    }));
    return inputFieldInfo;
  }
  return null;
}

// Function to parse the page and check for input fields
function checkForInputFields() {
  const inputFieldInfo = getAllInputFields();

  // Grab the page domain
  var domain = parseDomain();

  // Notify the background script that input fields are found
  chrome.runtime.sendMessage(
    {
      type: 'AUTOFILL_LOGIN_DETAILS',
      payload: {
        hasInputFields: true,
        inputFieldInfo,
        domain
      },
    }
  );
}

function parseDomain() {
  var pageHref = window.location.href;

  // Remove any queries from pageHref
  var queryIndex = pageHref.indexOf('?');
  if (queryIndex !== -1) {
    pageHref = pageHref.substring(0, queryIndex);
  }

  return pageHref.split('/')[2];;
}

// Run the checkForInputFields function when the DOM is ready
if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', checkForInputFields);
} else {
  checkForInputFields();
}

// Listen for a message from the background script indicating that we should auto fill login details
chrome.runtime.onMessage.addListener((request, sender, sendResponse) => {
  if (request.type === 'AUTOFILL_DETAILS_REQUEST') {
    // Find the fields on the page
    const usernameField = document.getElementById(request.username_field_id);
    const passwordField = document.getElementById(request.password_field_id);

    // Autofill the fields if found
    if (usernameField && passwordField) {
      usernameField.value = request.username;
      passwordField.value = request.password;
    } else {
      // Username and/or password fields not found on the page. // TODO: Add error handling, we probably should've gone this far if we can't find the fields.
    }
  }

  // Send an empty response
  // See https://github.com/mozilla/webextension-polyfill/issues/130#issuecomment-531531890
  sendResponse({});
  return true;
});

// Listen for a message from the background script indicating that we should prompt for a PIN code
chrome.runtime.onMessage.addListener((request, sender, sendResponse) => {
  if (request.type === 'PIN_CODE_REQUEST') {
    // Show a prompt for the PIN code
    const pinCode = prompt('Please enter PIN code to retrieve login details:');

    if (pinCode == null || pinCode.length == 0) {
      // User has cancelled the prompt
      return;
    }
    
    const domain = parseDomain();
    const inputFieldInfo = getAllInputFields();

    // Attempt to retrieve login details with the pin code
    chrome.runtime.sendMessage(
      {
        type: 'AUTOFILL_LOGIN_DETAILS',
        payload: {
          inputFieldInfo,
          domain,
          pinCode
        },
      }
    );
    return true;
  }
});

// Listen for a message from the background script indicating that we should prompt for a passphrase
chrome.runtime.onMessage.addListener((request, sender, sendResponse) => {
  if (request.type === 'PASSPHRASE_REQUEST') {
    // Show a prompt for the passprhase
    const passphrase = prompt('Please enter passphrase to retrieve login details:');

    if (passphrase == null || passphrase.length == 0) {
      // User has cancelled the prompt
      return;
    }

    const domain = parseDomain();
    const inputFieldInfo = getAllInputFields();

    // Attempt to retrieve login details with the passphrase
    chrome.runtime.sendMessage(
      {
        type: 'AUTOFILL_LOGIN_DETAILS',
        payload: {
          inputFieldInfo,
          domain,
          passphrase
        },
      }
    );
    return true;
  }
});

// Listen for a message from the background script indicating that we should prompt for a passkey
chrome.runtime.onMessage.addListener(async (request, sender, sendResponse) => {
  if (request.type === 'PASSKEY_REQUEST') {
    const credentialId = request.credentialId;
    const challenge = str2ab(request.challenge);

    console.log('Using challenge: ', challenge)

    const credential = await getUserPasskeyCredentials(credentialId, challenge);
    console.log('User passkey credentials', credential);

    const serializeable = {
      authenticatorAttachment: credential.authenticatorAttachment,
      id: credential.id,
      rawId: bufferToBase64url(credential.rawId),
      response: {
        attestationObject: bufferToBase64url(credential.response.attestationObject),
        clientDataJSON: bufferToBase64url(credential.response.clientDataJSON),
        signature: bufferToBase64url(credential.response.signature),
        authenticatorData: bufferToBase64url(credential.response.authenticatorData)
      },
      type: credential.type
    };
    const serialized = JSON.stringify(serializeable);
    console.log('Sending userPasskeyCredentials', serialized);

    const domain = parseDomain();
    const inputFieldInfo = getAllInputFields();

    chrome.runtime.sendMessage(
      {
        type: 'VERIFY_PASSKEY_CREDENTIALS',
        payload: {
          inputFieldInfo,
          domain,
          userPasskeyCredentialsJSON: serialized,
          loginDetailsId: request.loginDetailsId
        },
      }
    );
    return true;
  }
});

function bufferToBase64url(buffer) {

  // modified from https://github.com/github/webauthn-json/blob/main/src/webauthn-json/base64url.ts

  const byteView = new Uint8Array(buffer);
  let str = "";
  for (const charCode of byteView) {
    str += String.fromCharCode(charCode);
  }

  // Binary string to base64
  const base64String = btoa(str);

  // Base64 to base64url
  // We assume that the base64url string is well-formed.
  const base64urlString = base64String.replace(/\+/g, "-").replace(
    /\//g,
    "_",
  ).replace(/=/g, "");
  return base64urlString;
}
