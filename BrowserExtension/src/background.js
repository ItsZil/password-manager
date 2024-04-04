'use strict';

/*const importMap = {
    "imports": {
        "@popperjs/core": chrome.runtime.getURL("ext/popper.min.js"),
        "bootstrap": chrome.runtime.getURL("ext/bootstrap.esm.min.js")
    }
};
chrome.storage.local.set({ importMap });*/

const serverUrl = 'https://localhost:5271';

// Placeholder for Diffie-Hellman key pair generation and handshake initiationimport * as forge from 'node-forge';

function initiateHandshake() {
    const ecdh = forge.pki.ed25519.createKeyPair();
    const clientPublicKey = forge.util.encode64(ecdh.publicKey);

    fetch(`${serverUrl}/api/handshake`, {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json'
        },
        body: JSON.stringify({ ClientPublicKey: clientPublicKey })
    })
    .then(response => response.json())
    .then(data => {
        const serverPublicKey = forge.util.decode64(data.ServerPublicKey);
        // Here you would typically use the server's public key along with the client's private key to compute the shared secret
    })
    .catch(error => console.error('Error during handshake: ', error));
}

chrome.runtime.onInstalled.addListener(() => {
    console.log('Password Manager extension installed.');

    // Start handshake process with server, /api/handshake
    const apiEndpoint = '/api/handshake';
    initiateHandshake();
});

chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
    if (message.hasInputFields) {
        handleInputFields(message);
    } else if (message == 'retrieveTestResponse') {
        testCommunication()
            .then(response => {
                console.log("Sending response: ", response);
                sendResponse({ data: response });
            })
            .catch(error => {
                console.error("Error during communication: ", error);
                sendResponse({ error: error.message });
            });
        return true;
    }
});

function testCommunication() {
    const apiEndpoint = '/api/test';
    const enableTest = true;

    if (enableTest) {
        return fetch(`${serverUrl}${apiEndpoint}`)
            .then(response => response.json())
            .catch(error => {
                console.error('Error retrieving response: ', error);
                throw error;
            });
    } else {
        return Promise.resolve('');
    }
}

function domainLoginRequest(domainLoginRequestBody) {
    const apiEndpoint = '/api/domainloginrequest';

    return fetch(`${serverUrl}${apiEndpoint}`, {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json'
        },
        body: JSON.stringify(domainLoginRequestBody)
        })
        .then(response => response.json())
        .catch(error => {
            console.error('Error retrieving response: ', error);
            throw error;
        });

}

async function handleInputFields(message) {
    const usernameField = message.inputFieldInfo.find(field => field.type === 'username' || field.id === 'username' || field.name === 'username' || field.name === 'nick');
    const passwordField = message.inputFieldInfo.find(field => field.type === 'password' || field.id === 'password' || field.name === 'password');

    if (usernameField && passwordField) {
        try {
            const loginInfo = await retrieveLoginInfo(message.domain);
            console.log("Login info: ", loginInfo);

            // Send a message to content script to autofill the input fields
            chrome.tabs.query({ active: true, currentWindow: true }, function (tabs) {
                chrome.tabs.sendMessage(tabs[0].id, { action: 'autofillDetails', username_field_id: usernameField.id, password_field_id: passwordField.id, username: loginInfo.username, password: loginInfo.password });
            });
        } catch (error) {
            console.error("Error while handling input fields: ", error);
        }
    }
}

async function retrieveLoginInfo(domain) {
    const domainLoginRequestBody = {
        domain: domain,
        userAgent: navigator.userAgent
    };

    try {
        const response = await domainLoginRequest(domainLoginRequestBody); // DomainLoginResponse

        if (response.hasPermission && response.hasCredentials) {
            const loginInfo = {
                username: response.username,
                password: response.password
            };
            console.log("Returning login info: ", loginInfo);
            return loginInfo;
        }
        console.log("No login info found.");
        return null;
    } catch (error) {
        console.error("Error during communication: ", error);
        throw error;
    }
}