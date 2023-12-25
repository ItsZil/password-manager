'use strict';

document.addEventListener('DOMContentLoaded', () => {
    const fetchButton = document.getElementById('fetch-passwords');

    fetchButton.addEventListener('click', () => {
        testCommunication();
    });

    function testCommunication() {
        (async () => {
            const response = await chrome.runtime.sendMessage("retrieveResponse");

            document.getElementById('response').innerHTML = response.data.message;
        })();
    }
});
