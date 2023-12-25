'use strict';

chrome.runtime.onInstalled.addListener(() => {
    console.log('Password Manager extension installed.');

    const serverUrl = 'http://localhost:5000'; // Replace with your server URL
    const apiEndpoint = '/api/passwords'; // Replace with your API endpoint
    const enableTest = false;

    // Example function to communicate with the server
    function fetchPasswords() {
        if (enableTest) {
            fetch(`${serverUrl}${apiEndpoint}`)
                .then(response => response.json())
                .then(data => {
                    console.log('Passwords fetched from server:', data);
                })
                .catch(error => {
                    console.error('Error fetching passwords:', error);
                });
        }
    }

    // Call the function to fetch passwords after installation
    fetchPasswords();
});
