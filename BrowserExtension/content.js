'use strict';

// Content script logic to interact with web pages goes here.
console.log('Content script loaded.');

// Function to check if an element is an input field
function isInputField(element) {
    return element.tagName === 'INPUT' || element.tagName === 'TEXTAREA';
}

// Function to parse the page and check for input fields
function checkForInputFields() {
    const inputFields = document.querySelectorAll('input[type="email"], input[type="password"], input[type="text"], input[type="tel"], textarea');

    if (inputFields.length > 0) {
        // Extract information about input fields
        const inputFieldInfo = Array.from(inputFields).map(inputField => ({
            type: inputField.type,
            id: inputField.id,
            name: inputField.name,
            value: inputField.value
            // Add more attributes as needed
        }));

        var pageHref = window.location.href;

        // Remove any queries from pageHref
        var queryIndex = pageHref.indexOf('?');
        if (queryIndex !== -1) {
            pageHref = pageHref.substring(0, queryIndex);
        }

        // Grab the page domain
        var domain = pageHref.split('/')[2];

        // Notify the background script that input fields are found
        chrome.runtime.sendMessage({ hasInputFields: true, inputFieldInfo, pageHref, domain });
    }
}

// Run the function when the DOM is ready
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', checkForInputFields);
} else {
    checkForInputFields();
}

chrome.runtime.onMessage.addListener(function (message, sender, sendResponse) {
    if (message.action === 'autofillDetails') {
        // Find the fields on the page
        const usernameField = document.getElementById(message.username_field_id);
        const passwordField = document.getElementById(message.password_field_id);

        // Autofill the fields if found
        if (usernameField && passwordField) {
            usernameField.value = message.username;
            passwordField.value = message.password;
        } else {
            console.log('Username and/or password fields not found on the page.'); // TODO: Add error handling
        }
    }
});