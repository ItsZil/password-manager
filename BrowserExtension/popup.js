'use strict';

document.addEventListener('DOMContentLoaded', () => {
    const fetchButton = document.getElementById('fetch-passwords');
    const serverUrl = 'https://localhost:5271';
    const apiEndpoint = '/api/test';
    const enableTest = true;

    fetchButton.addEventListener('click', () => {
        console.log('Fetch passwords button clicked');
        testCommunication();
    });

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
});
