'use strict';

/*const importMap = {
    "imports": {
        "@popperjs/core": chrome.runtime.getURL("ext/popper.min.js"),
        "bootstrap": chrome.runtime.getURL("ext/bootstrap.esm.min.js")
    }
};
chrome.storage.local.set({ importMap });*/

chrome.runtime.onInstalled.addListener(() => {
    console.log('Password Manager extension installed.');

    const serverUrl = 'https://localhost:5271';
    const apiEndpoint = '/api/test';
    const enableTest = false;

    function testCommunication() {
        if (enableTest) {
            fetch(`${serverUrl}${apiEndpoint}`)
                .then(response => response.text())
                .then(data => {
                    console.log('Response received from server: ', data);
                })
                .catch(error => {
                    console.error('Error retrieving response: ', error);
                });
        }
    }

    testCommunication();
});