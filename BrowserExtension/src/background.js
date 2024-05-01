'use strict';

// Imports
import * as passwordUtil from './util/passwordUtil';
import * as requests from './util/requestsUtil';

const { authenticatePasskey } = require('./util/authUtil');

// onStartup listener that starts initialization
chrome.runtime.onStartup.addListener(init);
chrome.runtime.onInstalled.addListener(init);

// Context menu onClick listener
chrome.contextMenus.onClicked.addListener(contextMenuOnClick);

const sourceId = Math.floor(Math.random() * 1000000);

async function init() {
  console.log('Password Manager extension started.');

  // We need to pass the crypto object to the passwordUtil file and start handshake process
  await passwordUtil.init(sourceId, crypto);

  // Initialize the context menus
  addContextMenus();
}

function addContextMenus() {
  let contexts = ['editable'];
  let parent = chrome.contextMenus.create({
    title: 'Password Manager',
    contexts: contexts,
    id: 'parent',
  });

  chrome.contextMenus.create({
    title: 'Paste Generated Password',
    parentId: parent,
    contexts: contexts,
    id: 'pasteGeneratedPassword',
  });
  chrome.contextMenus.create({
    title: 'Paste Authenticator Code',
    parentId: parent,
    contexts: contexts,
    id: 'pasteAuthenticatorCode',
  });
}

// A context menu onClick callback function.
async function contextMenuOnClick(info, tab) {
  switch (info.menuItemId) {
    case 'pasteGeneratedPassword':
      let generatedPassword = await requests.generatePassword(sourceId);

      if (generatedPassword) {
        generatedPassword = await passwordUtil.decryptPassword(
          generatedPassword
        );
        showSuccessNotification('Password Generated', 'Copied to clipboard');
      } else {
        showFailureNotification(
          'Password Generation Failed',
          'Please try again'
        );
        break;
      }

      contextMenuPasteValue(tab, generatedPassword, true);
      break;
    case 'pasteAuthenticatorCode':
      // Parse the domain
      let domain = tab.url;
      var queryIndex = domain.indexOf('?');
      if (queryIndex !== -1) {
        domain = domain.substring(0, queryIndex);
      }
      domain = domain.split('/')[2];

      // Remove any www.
      if (domain.startsWith('www.')) {
        domain = domain.substring(4);
      }

      // Get the current timestamp
      const timestamp = new Date().toISOString();
      const timestampUri = encodeURIComponent(timestamp);

      // Get the authenticator code
      const authenticatorCode =
        await requests.sendGetAuthenticatorCodeByDomainRequest(
          domain,
          timestampUri
        );
      if (!authenticatorCode.code) {
        if (authenticatorCode.notFound) {
          showFailureNotification(
            'Authenticator not found',
            'Create one in options'
          );
        } else {
          showFailureNotification(
            'Failed to get authenticator code',
            'Please try again'
          );
        }

        break;
      }

      contextMenuPasteValue(tab, authenticatorCode.code);
      break;
  }
}

// Function to paste a value into the focused input field
function contextMenuPasteValue(tab, value, saveToClipboard = false) {
  chrome.scripting
    .executeScript({
      target: { tabId: tab.id },
      function: function (value, saveToClipboard = false) {
        if (saveToClipboard) {
          navigator.clipboard.writeText(value);
        }

        // Find the focused input field
        var inputField = document.activeElement;

        // Check if the input field exists and is editable
        if (
          inputField &&
          (inputField.tagName === 'INPUT' ||
            inputField.tagName === 'TEXTAREA') &&
          !inputField.readOnly
        ) {
          inputField.value = value;

          // Set input field value attribute
          inputField.setAttribute('value', value);
          inputField.dispatchEvent(new Event('input', { bubbles: true }));

          if (inputField.id) {
            // If the inputField has an id, return it
            return { id: inputField.id };
          } else {
            // If the inputField doesn't have an id, use tagName and index
            var parent = element.parentNode;
            var tagName = element.tagName.toLowerCase();
            var index = Array.from(parent.children).indexOf(element);
            return { tagName: tagName, index: index };
          }
        } else {
          return {
            title: 'Paste Failed',
            message: 'No input field is focused or editable.',
          };
        }
      },
      args: [value, saveToClipboard],
    })
    .then((response) => {
      const result = response[0].result;
      if (result.title && result.message) {
        // Show a notification if the paste failed
        showFailureNotification(result.title, result.message);
      } else if (result.id || result.index) {
        // Send a message to the content script to animate the input field
        chrome.tabs.query(
          { active: true, currentWindow: true },
          function (tabs) {
            chrome.tabs.sendMessage(tabs[0].id, {
              type: 'ANIMATE_INPUT_FIELD',
              inputFieldResult: result,
            });
          }
        );
      }
    });
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
        password: await passwordUtil.decryptPassword(
          domainLoginResponse.password
        ),
      };

      request.payload.loginInfo = loginInfo;
      handleInputFields(request.payload);
    }
  }
});

// Function to fetch login details from server and send them to the content script
async function retrieveLoginInfo(
  domain,
  pinCode = null,
  passphrase = null,
  attempt = 0
) {
  await passwordUtil.init(sourceId, crypto); // Ensure we are initialized and have completed handshake.

  await passwordUtil.initiateHandshake(sourceId);

  let encryptedPassphrase = null;
  if (passphrase != null) {
    // Encrypt the passphrase
    encryptedPassphrase = await passwordUtil.encryptPassword(passphrase);
  }

  const domainLoginRequestBody = {
    sourceId: sourceId,
    domain: domain,
    pinCode: pinCode,
    passphrase: encryptedPassphrase,
  };

  const response = await requests.domainLoginRequest(domainLoginRequestBody); // DomainLoginResponse

  if (!response) {
    // No login info has been found for this domain.
    return null;
  } else if (response.unauthorized) {
    // Incorrect PIN code entered.
    showFailureNotification(
      'Extra Authentication Failed',
      'Refresh to try again'
    );
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
    if (attempt < 3) {
      // Retry
      return await retrieveLoginInfo(domain, pinCode, passphrase, attempt + 1);
    } else {
      console.error('Error during decryption: ', error);

      return null;
    }
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
  const passkeyCredentials = await requests.sendGetPasskeyCredentialRequest(
    sourceId,
    response.loginDetailsId
  );

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
      sourceId: sourceId,
    });
  });
}

async function verifyPasskeyCredentials(payload) {
  const userPasskeyCredentials = JSON.parse(payload.userPasskeyCredentialsJSON);
  const domainLoginResponse = await authenticatePasskey(
    userPasskeyCredentials,
    null,
    payload.loginDetailsId,
    sourceId,
    crypto,
    true,
    true
  );

  if (domainLoginResponse.password) {
    // Authentication successful, send login info to content script
  }
  return domainLoginResponse;
}

function showFailureNotification(title, message) {
  chrome.notifications.create({
    type: 'basic',
    iconUrl: 'icons/icon_fail_196.png',
    title: title,
    message: message,
  });
}

function showSuccessNotification(title, message) {
  chrome.notifications.create({
    type: 'basic',
    iconUrl: 'icons/icon_128.png',
    title: title,
    message: message,
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

let handledTradingView = false;

// Function to check if there are elements on the current page that are login or registration input fields
async function handleInputFields(message) {
  if (!message.inputFieldInfo) {
    return;
  }

  const usernameField = message.inputFieldInfo.find((field) =>
    isUsernameField(field)
  );

  const passwordField = message.inputFieldInfo.find((field) =>
    isPasswordField(field)
  );

  if (usernameField && passwordField) {
    try {
      let loginInfo = message.loginInfo;
      if (!loginInfo) {
        // If the page is TradingView, we only autofill the login form once
        if (message.domain.includes('tradingview') && handledTradingView) {
          return;
        } else if (message.domain.includes('tradingview')) {
          handledTradingView = true;
        }

        loginInfo = await retrieveLoginInfo(
          message.domain,
          message.pinCode,
          message.passphrase
        );
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
