'use strict';

// Imports
import * as passwordUtil from './util/passwordUtil';
import * as requests from './util/requestsUtil';

// onStartup listener that starts initialization
chrome.runtime.onStartup.addListener(init);
chrome.runtime.onInstalled.addListener(init);

async function init() {
  console.log('Password Manager extension started.');

  // We need to pass the crypto object to the passwordUtil file and start handshake process
  passwordUtil.init(0, crypto);

  /*
  let password = 'Password123';
  const encryptedPassword = await passwordUtil.encryptPassword(password);
  const domainRegisterRequestBody = {
    domain: 'practicetestautomation.com',
    username: 'student',
    password: encryptedPassword
  }
  await requests.domainRegisterRequest(domainRegisterRequestBody);*/
}

// Listener for requests from content script
chrome.runtime.onMessage.addListener((request, sender, sendResponse) => {
  if (request.type === 'LOGIN_INPUT_FIELDS_FOUND') {
    handleInputFields(request.payload);
  } else if (request.type == 'RETRIEVE_TEST_RESPONSE') {
    testCommunication()
      .then((response) => {
        console.log('Sending response: ', response);
        sendResponse({ data: response });
      })
      .catch((error) => {
        console.error('Error during communication: ', error);
        sendResponse({ error: error.message });
      });
    return true;
  }
});

// Function to fetch login details from server and send them to the content script
async function retrieveLoginInfo(domain) {
  passwordUtil.init(0, crypto); // Ensure we are initialized and have completed handshake. TODO: alternatives?
  const domainLoginRequestBody = {
    domain: domain
  };

  const response = await requests.domainLoginRequest(domainLoginRequestBody); // DomainLoginResponse

  if (response.hasPermission && response.hasCredentials) {
    try {
      const loginInfo = {
        username: response.username,
        password: await passwordUtil.decryptPassword(response.password),
      };

      // Returning login info to handleInputFields.
      return loginInfo;
    } catch (error) {
      console.error('Error during decryption: ', error);
    }
  }
  // No login info has been found in the vault by the server.
  return null;
}

// Function to check if there are elements on the current page that are login or registration input fields
async function handleInputFields(message) {
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
      const loginInfo = await retrieveLoginInfo(message.domain);

      // Send a message to content script to autofill the input fields
      chrome.tabs.query({ active: true, currentWindow: true }, function (tabs) {
        chrome.tabs.sendMessage(tabs[0].id, {
          type: 'AUTOFILL_DETAILS',
          username_field_id: usernameField.id,
          password_field_id: passwordField.id,
          username: loginInfo.username,
          password: loginInfo.password,
        });
      });
    } catch (error) {
      console.error('Error while handling input fields: ', error);
    }
  }
}

// Function to test communication with server
function testCommunication() {
  const apiEndpoint = '/api/test';
  const enableTest = true;

  if (enableTest) {
    return fetch(`${ServerUrl}${apiEndpoint}`)
      .then((response) => response.json())
      .catch((error) => {
        console.error('Error retrieving response: ', error);
        throw error;
      });
  } else {
    return Promise.resolve('');
  }
}

/*chrome.runtime.onMessage.addListener((request, sender, sendResponse) => {
  if (request.type === 'GREETINGS') {
    const message = `Hi ${
      sender.tab ? 'Con' : 'Pop'
    }, my name is Bac. I am from Background. It's great to hear from you.`;

    // Log message coming from the `request` parameter
    console.log(request.payload.message);
    // Send a response message
    sendResponse({
      message,
    });
  }
});*/
