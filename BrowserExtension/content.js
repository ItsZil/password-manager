'use strict';

// Content script logic to interact with web pages goes here.
console.log('Content script loaded.');

// Function to check if an element is an input field
function isInputField(element) {
    return element.tagName === 'INPUT' || element.tagName === 'TEXTAREA';
}

// Function to parse the page and check for input fields
function checkForInputFields() {
    const inputFields = document.querySelectorAll('input, textarea');
    const filteredInputFields = Array.from(inputFields).filter(inputField => {
        return inputField.type === 'email' || inputField.type === 'password' || inputField.type === 'text' || inputField.type === 'tel';
    });

    if (filteredInputFields.length > 0) {
        var pageHref = window.location.href;

        // Remove any queries from pageHref
        var queryIndex = pageHref.indexOf('?');
        if (queryIndex !== -1) {
            pageHref = pageHref.substring(0, queryIndex);
        }

        // Grab the page domain
        var domain = pageHref.split('/')[2];

        // Notify the background script that input fields are found
        chrome.runtime.sendMessage({ hasInputFields: true, filteredInputFields, pageHref, domain });
    }
}

// Run the function when the DOM is ready
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', checkForInputFields);
} else {
    checkForInputFields();
}