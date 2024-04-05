'use strict';

// Imports

// Constants
const serverUrl = 'https://localhost:5271'; // TODO: Do not hardcode like this?

// onInstalled listener that starts initialization
chrome.runtime.onInstalled.addListener(() => {
  console.log('Password Manager extension installed.');

  // Start handshake process with server
  initiateHandshake();
});

// Function to initiate handshake with server in order to generate a shared secret
async function initiateHandshake() {
  try {
    // Generate client key pair
    const clientKeyPair = await crypto.subtle.generateKey({ name: 'ECDH', namedCurve: 'P-521' }, true, ['deriveKey', 'deriveBits']);

    // Export client public key
    const clientPublicKey = await crypto.subtle.exportKey('spki', clientKeyPair.publicKey);
    const clientPublicKeyBase64 = btoa(String.fromCharCode(...new Uint8Array(clientPublicKey)));

    // Send client public key to server
    let response;
    try {
      response = await fetch(`${serverUrl}/api/handshake`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({ clientPublicKey: clientPublicKeyBase64 }),
      });
    } catch (error) {
      console.error('Error sending client public key:', error);
      return;
    }

    if (!response.ok) {
      throw new Error(`Failed to send client public key. Status code: ${response.status}`);
    }

    const data = await response.json();
    const serverPublicKeyBase64 = data.serverPublicKey;
    const serverPublicKeyArrayBuffer = new Uint8Array(atob(serverPublicKeyBase64).split('').map((c) => c.charCodeAt(0))).buffer;

    // Import server public key
    const serverPublicKey = await crypto.subtle.importKey('spki', serverPublicKeyArrayBuffer, { name: 'ECDH', namedCurve: 'P-521' }, false, []);

    // Generate shared secret
    const sharedSecret = await crypto.subtle.deriveBits({ name: 'ECDH', public: serverPublicKey }, clientKeyPair.privateKey, 256);
  } catch (error) {
    console.error('Error during handshake:', error);
  }
}



// Listener for requests from content script
chrome.runtime.onMessage.addListener((request, sender, sendResponse) => {
  if (request.type === 'LOGIN_INPUT_FIELDS_FOUND') {
    handleInputFields(message);
  } else if (message == 'retrieveTestResponse') {
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

// Function to handle input fields found in the page by sending a domain login details request
function domainLoginRequest(domainLoginRequestBody) {
  const apiEndpoint = '/api/domainloginrequest';

  return fetch(`${serverUrl}${apiEndpoint}`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(domainLoginRequestBody),
  })
    .then((response) => response.json())
    .catch((error) => {
      console.error('Error retrieving response: ', error);
      throw error;
    });
}

// Function to fetch login details from server and send them to the content script
async function retrieveLoginInfo(domain) {
  const domainLoginRequestBody = {
    domain: domain,
    userAgent: navigator.userAgent,
  };

  try {
    const response = await domainLoginRequest(domainLoginRequestBody); // DomainLoginResponse

    if (response.hasPermission && response.hasCredentials) {
      const loginInfo = {
        username: response.username,
        password: response.password,
      };
      console.log('Returning login info: ', loginInfo);
      return loginInfo;
    }
    console.log('No login info found.');
    return null;
  } catch (error) {
    console.error('Error during communication: ', error);
    throw error;
  }
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
      console.log('Login info: ', loginInfo);

      // Send a message to content script to autofill the input fields
      chrome.tabs.query({ active: true, currentWindow: true }, function (tabs) {
        chrome.tabs.sendMessage(tabs[0].id, {
          action: 'autofillDetails',
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
    return fetch(`${serverUrl}${apiEndpoint}`)
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
