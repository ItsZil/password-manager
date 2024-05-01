'use strict';

const { getUserPasskeyCredentials } = require('./util/authUtil.js');
const { str2ab } = require('./util/passwordUtil.js');

const autofillAnimLength = 300;

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
      placeholder: inputField.placeholder,
    }));
    return inputFieldInfo;
  }
  return null;
}

// Function to check if there are elements on the current page that are login or registration input fields
async function checkInputFields(inputFieldInfo) {
  const usernameField = inputFieldInfo.find((field) =>
    isUsernameField(field)
  );

  const passwordField = inputFieldInfo.find((field) =>
    isPasswordField(field)
  );

  if (usernameField && passwordField) {
    return true;
  }
  return false;
}

function isUsernameField(field) {
  const usernameKeywords = ['username', 'id_username', 'email', 'user', 'login', 'nickname'];

  // Check if any of the common keywords appear in id, name, or placeholder
  return (
    (field.type === 'text' || field.type === 'email') &&
    (usernameKeywords.includes(field.id) ||
      usernameKeywords.includes(field.name) ||
      usernameKeywords.some((keyword) =>
        field.placeholder.toLowerCase().includes(keyword)
      ))
  );
}

function isPasswordField(field) {
  const passwordKeywords = ['password', 'id_password', 'passcode', 'pass', 'pwd'];

  // Check if any of the common keywords appear in id, name, or placeholder
  return (
    field.type === 'password' &&
    (passwordKeywords.includes(field.id) ||
      passwordKeywords.includes(field.name) ||
      passwordKeywords.some((keyword) =>
        field.placeholder.toLowerCase().includes(keyword)
      ))
  );
}


// Function to parse the page and check for input fields
function checkForInputFields() {
  const inputFieldInfo = getAllInputFields();

  // Grab the page domain
  var domain = parseDomain();

  let check = checkInputFields(inputFieldInfo);

  if (check) {
    // Notify the background script that input fields are found
    chrome.runtime.sendMessage({
      type: 'AUTOFILL_LOGIN_DETAILS',
      payload: {
        hasInputFields: true,
        inputFieldInfo,
        domain,
      },
    });
    return true;
  }
  return false;
}

export function parseDomain() {
  var pageHref = window.location.href;

  // Remove any queries from pageHref
  var queryIndex = pageHref.indexOf('?');
  if (queryIndex !== -1) {
    pageHref = pageHref.substring(0, queryIndex);
  }
  let domain = pageHref.split('/')[2];
  
  if (domain.startsWith('www.')) {
    domain = domain.substring(4);
  }

  return domain;
}

// Run the checkForInputFields function when the DOM is ready
if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', function() {
    checkForInputFields();
    // Create a new MutationObserver to observe changes in the DOM
    const observer = new MutationObserver(function(mutationsList, observer) {
      for(let mutation of mutationsList) {
        if (mutation.type === 'childList') {
          // Check for changes in the DOM
          checkForInputFields();
        }
      }
    });

    // Start observing the entire document for changes
    observer.observe(document.documentElement, { childList: true, subtree: true });
  });
} else {
  checkForInputFields();
  // Create a new MutationObserver to observe changes in the DOM
  const observer = new MutationObserver(function(mutationsList, observer) {
    for(let mutation of mutationsList) {
      if (mutation.type === 'childList') {
        // Check for changes in the DOM
        checkForInputFields();
      }
    }
  });

  // Start observing the entire document for changes
  observer.observe(document.documentElement, { childList: true, subtree: true });
}

export function addStylesheet() {
  const styleElement = document.createElement('style');

  styleElement.textContent = `
  .autofilled {
    animation: autofillAnimation 0.3s ease-in-out;
  }

  @keyframes autofillAnimation {
      0% {
        transform: scale(1);
      }
      50% {
        transform: scale(1.07);
      }
      100% {
        transform: scale(1);
      }
    }
  }
`;

  document.head.appendChild(styleElement);
}

// Listen for a message from the background script indicating that we should auto fill login details
chrome.runtime.onMessage.addListener((request, sender, sendResponse) => {
  if (request.type === 'AUTOFILL_DETAILS_REQUEST') {
    // Find the fields on the page
    const usernameField = document.getElementById(request.username_field_id);
    const passwordField = document.getElementById(request.password_field_id);

    // Autofill the fields if found
    if (usernameField && passwordField) {
      // Run an autofill animation
      addStylesheet();

      usernameField.classList.add('autofilled');
      usernameField.value = request.username;
      usernameField.dispatchEvent(new Event('input', { bubbles: true }));

      // Wait 0.1s before animating the password field
      setTimeout(() => {
        passwordField.classList.add('autofilled');
        passwordField.value = request.password;
        passwordField.dispatchEvent(new Event('input', { bubbles: true }));
      }, 100);

      // Wait for autofillAnimLength before removing the autofilled class
      setTimeout(() => {
        usernameField.classList.remove('autofilled');
        passwordField.classList.remove('autofilled');
      }, autofillAnimLength + 100);
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
    chrome.runtime.sendMessage({
      type: 'AUTOFILL_LOGIN_DETAILS',
      payload: {
        inputFieldInfo,
        domain,
        pinCode,
      },
    });
    return true;
  }
});

// Listen for a message from the background script indicating that we should prompt for a passphrase
chrome.runtime.onMessage.addListener((request, sender, sendResponse) => {
  if (request.type === 'PASSPHRASE_REQUEST') {
    // Show a prompt for the passprhase
    const passphrase = prompt(
      'Please enter passphrase to retrieve login details:'
    );

    if (passphrase == null || passphrase.length == 0) {
      // User has cancelled the prompt
      return;
    }

    const domain = parseDomain();
    const inputFieldInfo = getAllInputFields();

    // Attempt to retrieve login details with the passphrase
    chrome.runtime.sendMessage({
      type: 'AUTOFILL_LOGIN_DETAILS',
      payload: {
        inputFieldInfo,
        domain,
        passphrase,
      },
    });
    return true;
  }
});

// Listen for a message from the background script indicating that we should prompt for a passkey
chrome.runtime.onMessage.addListener(async (request, sender, sendResponse) => {
  if (request.type === 'PASSKEY_REQUEST') {
    const credentialId = request.credentialId;
    const challenge = str2ab(atob(request.challenge));

    const credential = await getUserPasskeyCredentials(credentialId, challenge);

    const serializeableCredentials = {
      authenticatorAttachment: credential.authenticatorAttachment,
      id: credential.id,
      rawId: bufferToBase64url(credential.rawId),
      response: {
        attestationObject: bufferToBase64url(
          credential.response.attestationObject
        ),
        clientDataJSON: bufferToBase64url(credential.response.clientDataJSON),
        signature: bufferToBase64url(credential.response.signature),
        authenticatorData: bufferToBase64url(
          credential.response.authenticatorData
        ),
      },
      type: credential.type,
    };
    const serializedCredentials = JSON.stringify(serializeableCredentials);

    const domain = parseDomain();
    const inputFieldInfo = getAllInputFields();

    chrome.runtime.sendMessage({
      type: 'VERIFY_PASSKEY_CREDENTIALS',
      payload: {
        inputFieldInfo,
        domain,
        userPasskeyCredentialsJSON: serializedCredentials,
        loginDetailsId: request.loginDetailsId,
      },
    });
    return true;
  }
});

// Listen for a message from the background script indicating that we should animate an input field
chrome.runtime.onMessage.addListener((request, sender, sendResponse) => {
  if (request.type === 'ANIMATE_INPUT_FIELD') {
    const inputFieldResult = request.inputFieldResult;

    let inputField;
    if (inputFieldResult.id) {
      // If the inputFieldResult has an id, find the element using getElementById
      inputField = document.getElementById(inputFieldResult.id);
    } else {
      // If the inputFieldResult doesn't have an id, find the element using tag name and index
      const parent = document.body; // Assuming the parent is the body element
      const elements = parent.getElementsByTagName(inputFieldResult.tagName);
      if (elements.length > inputFieldResult.index) {
        inputField = elements[inputFieldResult.index];
      }
    }

    if (inputField) {
      // Perform actions on the input field
      addStylesheet(); // Assuming addStylesheet is defined elsewhere
      inputField.classList.add('autofilled');

      // Wait for autofillAnimLength before removing the autofilled class
      setTimeout(() => {
        inputField.classList.remove('autofilled');
      }, autofillAnimLength);
    }
  }
});

function bufferToBase64url(buffer) {
  // modified from https://github.com/github/webauthn-json/blob/main/src/webauthn-json/base64url.ts

  const byteView = new Uint8Array(buffer);
  let str = '';
  for (const charCode of byteView) {
    str += String.fromCharCode(charCode);
  }

  // Binary string to base64
  const base64String = btoa(str);

  // Base64 to base64url
  // We assume that the base64url string is well-formed.
  const base64urlString = base64String
    .replace(/\+/g, '-')
    .replace(/\//g, '_')
    .replace(/=/g, '');
  return base64urlString;
}
