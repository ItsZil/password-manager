'use strict';

/*const importMap = {
    "imports": {
        "@popperjs/core": chrome.runtime.getURL("ext/popper.min.js"),
        "bootstrap": chrome.runtime.getURL("ext/bootstrap.esm.min.js")
    }
};
chrome.storage.local.set({ importMap });*/

var response = '';

function testCommunication() {
    const serverUrl = 'https://localhost:5271';
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

chrome.runtime.onInstalled.addListener(() => {
    console.log('Password Manager extension installed.');
});

chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
    if (message.hasInputFields) {
        handleInputFields(message);
    } else if (message === "retrieveResponse") {
        return handleResponse(message);
    }
});

function handleInputFields(message) {
    const usernameField = message.inputFieldInfo.find(field => field.type === 'username' || field.id === 'username' || field.name === 'username' || field.name === 'nick');
    const passwordField = message.inputFieldInfo.find(field => field.type === 'password' || field.id === 'password' || field.name === 'password');

    if (usernameField && passwordField) {
        const loginInfo = retrieveLoginInfo(message.domain);

        // Send a message to content script to autofill the input fields
        chrome.tabs.query({ active: true, currentWindow: true }, function (tabs) {
            chrome.tabs.sendMessage(tabs[0].id, { action: 'autofillDetails', username_field_id: usernameField.id, password_field_id: passwordField.id, username: loginInfo.username, password: loginInfo.password });
        });
    }
}

function retrieveLoginInfo(domain) {
    // TODO, placeholder object.
    const loginInfo = {
        username: 'testuser',
        password: 'testpassword'
    };
    return loginInfo;
}

function handleResponse(message) {
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