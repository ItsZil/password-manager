'use strict';

// Imports
import * as passwordUtil from './util/passwordUtil';
import * as requests from './util/requestsUtil';

const {
  authenticatePasskey
} = require('./util/authUtil');

// onStartup listener that starts initialization
chrome.runtime.onStartup.addListener(init);
chrome.runtime.onInstalled.addListener(init);

const sourceId = 0;

async function init() {
  console.log('Password Manager extension started.');

  // We need to pass the crypto object to the passwordUtil file and start handshake process
  passwordUtil.init(sourceId, crypto);
}

// Listener for requests from content script
chrome.runtime.onMessage.addListener(async (request, sender, sendResponse) => {
  if (request.type === 'AUTOFILL_LOGIN_DETAILS') {
    handleInputFields(request.payload);
  } else if (request.type === 'VERIFY_PASSKEY_CREDENTIALS') {
    const domainLoginResponse = await verifyPasskeyCredentials(request.payload);
    if (domainLoginResponse.username && domainLoginResponse.password) {
      // Authentication successful, send login info to content script
      const loginInfo = {
        username: domainLoginResponse.username,
        password: await passwordUtil.decryptPassword(domainLoginResponse.password),
      };

      request.payload.loginInfo = loginInfo;
      handleInputFields(request.payload);
    }
  }
});

// Function to fetch login details from server and send them to the content script
async function retrieveLoginInfo(domain, pinCode = null, passphrase = null) {
  passwordUtil.init(sourceId, crypto); // Ensure we are initialized and have completed handshake.

  await passwordUtil.initiateHandshake();

  let encryptedPassphrase = null;
  if (passphrase != null) {
    // Encrypt the passphrase
    encryptedPassphrase = await passwordUtil.encryptPassword(passphrase);
  }

  const domainLoginRequestBody = {
    sourceId: sourceId,
    domain: domain,
    pinCode: pinCode,
    passphrase: encryptedPassphrase
  };

  const response = await requests.domainLoginRequest(domainLoginRequestBody); // DomainLoginResponse

  if (!response) {
    // No login info has been found for this domain.
    return null;
  } else if (response.unauthorized) {
    // Incorrect PIN code entered.
    showIncorrectExtraAuthNotification();
    return;
  }

  if (response.needsExtraAuth) {
    // Extra authentication is needed for this domain.
    switch (response.extraAuthId) {
      case 2:
        promptForPINCode(response);
        break;
      case 3:
        promptForPasskey(response);
        break;
      case 4:
        promptForPassphrase(response);
        break;
    }
    return;
  }


  try {
    const loginInfo = {
      username: response.username,
      password: await passwordUtil.decryptPassword(response.password),
    };

    // Returning login info to handleInputFields.
    return loginInfo;
  } catch (error) {
    console.error('Error during decryption: ', error);
    return null;
  }
}

function promptForPINCode(response) {
  chrome.tabs.query({ active: true, currentWindow: true }, function (tabs) {
    // Send a message to the content script to show a PIN code request prompt
    chrome.tabs.sendMessage(tabs[0].id, {
      type: 'PIN_CODE_REQUEST',
      loginDetailsId: response.loginDetailsId,
    });
  });
}

function promptForPassphrase(response) {
  chrome.tabs.query({ active: true, currentWindow: true }, function (tabs) {
    // Send a message to the content script to show a passphrase request prompt
    chrome.tabs.sendMessage(tabs[0].id, {
      type: 'PASSPHRASE_REQUEST',
      loginDetailsId: response.loginDetailsId,
    });
  });
}

async function promptForPasskey(response) {
  const passkeyCredentials = await requests.sendGetPasskeyCredentialRequest(sourceId, response.loginDetailsId);

  if (!passkeyCredentials) {
    return false;
  }

  const credentialId = passkeyCredentials.credentialId;
  const challenge = passkeyCredentials.challenge;

  chrome.tabs.query({ active: true, currentWindow: true }, function (tabs) {
    // Send a message to the content script to show a passkey request prompt
    chrome.tabs.sendMessage(tabs[0].id, {
      type: 'PASSKEY_REQUEST',
      loginDetailsId: response.loginDetailsId,
      credentialId: credentialId,
      challenge: challenge,
      sourceId: sourceId
    });
  });
}

async function verifyPasskeyCredentials(payload) {
  const userPasskeyCredentials = JSON.parse(payload.userPasskeyCredentialsJSON);
  const domainLoginResponse = await authenticatePasskey(userPasskeyCredentials, null, payload.loginDetailsId, sourceId, crypto, true, true);

  if (domainLoginResponse.password) {
    // Authentication successful, send login info to content script
  }
  return domainLoginResponse;
}

function showIncorrectExtraAuthNotification() {
  chrome.notifications.create({
    type: 'basic',
    iconUrl: 'icons/icon_fail_196.png',
    title: 'Extra Authentication Failed',
    message: 'Refresh to try again',
  });
}

function sendAutofillDetailsMessage(usernameField, passwordField, loginInfo) {
  // Send a message to content script to autofill the input fields
  chrome.tabs.query({ active: true, currentWindow: true }, function (tabs) {
    chrome.tabs.sendMessage(tabs[0].id, {
      type: 'AUTOFILL_DETAILS_REQUEST',
      username_field_id: usernameField.id,
      password_field_id: passwordField.id,
      username: loginInfo.username,
      password: loginInfo.password,
    });
  });
}

// Function to check if there are elements on the current page that are login or registration input fields
async function handleInputFields(message) {
  if (!message.inputFieldInfo) {
    return;
  }

  const usernameField = message.inputFieldInfo.find(
    (field) =>
      field.type === 'username' ||
      field.id === 'username' ||
      field.name === 'username' ||
      field.name === 'nick'
  );
  const passwordField = message.inputFieldInfo.find(
    (field) =>
      field.type === 'password' ||
      field.id === 'password' ||
      field.name === 'password'
  );

  if (usernameField && passwordField) {
    try {
      let loginInfo = message.loginInfo;
      if (!loginInfo) {
        loginInfo = await retrieveLoginInfo(message.domain, message.pinCode, message.passphrase);
      }

      if (loginInfo) {
        // No extra authentication needed, send login info to content script
        sendAutofillDetailsMessage(usernameField, passwordField, loginInfo);
      }
    } catch (error) {
      console.error('Error while handling input fields: ', error);
    }
  }
}

