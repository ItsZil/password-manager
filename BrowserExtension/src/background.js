'use strict';

// Imports

// Constants
const ServerUrl = 'https://localhost:5271'; // TODO: Do not hardcode like this?
let SharedSecret = null;

// onInstalled listener that starts initialization
chrome.runtime.onInstalled.addListener(() => {
  console.log('Password Manager extension installed.');

  // Start handshake process with server
  initiateHandshake();

  /*let domainRegisterRequestBody = {
    domain: 'login.ktu.lt',
    username: 'zilkra'
  };
  domainRegisterRequest(domainRegisterRequestBody)*/
});

// Function to convert a string to an ArrayBuffer
function str2ab(str) {
  const buf = new ArrayBuffer(str.length);
  const bufView = new Uint8Array(buf);
  for (let i = 0, strLen = str.length; i < strLen; i++) {
    bufView[i] = str.charCodeAt(i);
  }
  return buf;
}

// Function to initiate handshake with server in order to generate a shared secret
async function initiateHandshake() {
  try {
    // Generate client key pair
    const clientKeyPair = await crypto.subtle.generateKey({ name: 'ECDH', namedCurve: 'P-256' }, true, ['deriveKey', 'deriveBits']);

    // Export client public key
    const clientPublicKey = await crypto.subtle.exportKey('spki', clientKeyPair.publicKey);
    const publicKeyBytes = new Uint8Array(clientPublicKey);
    const clientPublicKeyBase64 = btoa(String.fromCharCode.apply(null, publicKeyBytes));

    // Send client public key to server
    let response;
    try {
      response = await fetch(`${ServerUrl}/api/handshake`, {
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

    // Read server public key
    const data = await response.json();
    const serverPublicKeyBase64 = data.serverPublicKey;
    const serverPublicKeyArrayBuffer = str2ab(atob(serverPublicKeyBase64));

    // Import server public key
    const serverPublicKey = await crypto.subtle.importKey('spki', serverPublicKeyArrayBuffer, { name: 'ECDH', namedCurve: 'P-256' }, true, []);

    // Generate raw (unhashed) shared secret
    const rawSecret = await crypto.subtle.deriveKey({ name: 'ECDH', public: serverPublicKey }, clientKeyPair.privateKey, { name: 'AES-GCM', length: 256 }, true, ['encrypt', 'decrypt']);

    // Export the raw shared secret key, hash it with SHA-256 and import it back
    const rawSharedSecret = await crypto.subtle.exportKey('raw', rawSecret);
    const hashedSharedSecret = await crypto.subtle.digest('SHA-256', rawSharedSecret);

    SharedSecret = await crypto.subtle.importKey('raw', hashedSharedSecret, { name: 'AES-GCM', length: 256 }, true, ['encrypt', 'decrypt']);

  } catch (error) {
    console.error('Error during handshake:', error);
  }
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

// Function to handle domain registration requests
function domainRegisterRequest(domainRegisterRequestBody) {
  const apiEndpoint = '/api/domainregisterrequest';

  return fetch(`${ServerUrl}${apiEndpoint}`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(domainRegisterRequestBody),
  })
    .then((response) => {
      if (response.status === 200) {
        return response.json();
      } else {
        console.error(`Failed to register for ${domainRegisterRequestBody.domain}: ${response.status} ${response.statusText}`);
      }
    })
    .catch((error) => {
      console.error('Error retrieving response: ', error);
      throw error;
    });
}


// Function to fetch login details from server and send them to the content script
async function retrieveLoginInfo(domain) {
  const domainLoginRequestBody = {
    domain: domain
  };

    const response = await domainLoginRequest(domainLoginRequestBody); // DomainLoginResponse
    console.log(response);

  if (response.hasPermission && response.hasCredentials) {
    try {
      // The password is a base64 encoded AES encrypted byte[].
      // Decrypt the password using the shared secret.
      const passwordEncrypted = str2ab(atob(response.password));

      const passwordDecrypted = await crypto.subtle.decrypt({ name: 'AES-GCM', iv: passwordEncrypted.slice(0, 16) }, SharedSecret, passwordEncrypted); // TODO: does this work?

      const loginInfo = {
        username: response.username,
        password: passwordDecrypted
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

// Function to handle input fields found in the page by sending a domain login details request
function domainLoginRequest(domainLoginRequestBody) {
  const apiEndpoint = '/api/domainloginrequest';

  return fetch(`${ServerUrl}${apiEndpoint}`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(domainLoginRequestBody),
  })
    .then((response) => {
      if (response.status === 200) {
        return response.json();
      } else {
        console.error(`Failed to login to ${domainLoginRequestBody.domain}: ${response.status} ${response.statusText}`);
      }
    })
    .catch((error) => {
      console.error('Error retrieving response: ', error);
      throw error;
    });
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
