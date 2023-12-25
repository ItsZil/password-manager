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

chrome.runtime.onMessage.addListener((request, sender, sendResponse) => {
    if (request === "retrieveResponse") {
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