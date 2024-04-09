'use strict';

// Content script file will run in the context of web page.
// With content script you can manipulate the web pages using
// Document Object Model (DOM).
// You can also pass information to the parent extension.

// We execute this script by making an entry in manifest.json file
// under `content_scripts` property

// For more information on Content Scripts,
// See https://developer.chrome.com/extensions/content_scripts

require('bootstrap');

/* Communicate with background file by sending a message
chrome.runtime.sendMessage(
  {
    type: 'GREETINGS',
    payload: {
      message: 'Hello, my name is Con. I am from ContentScript.',
    },
  },
  (response) => {
    console.log(response.message);
  }
);*/

// Function to check if an element is an input field
function isInputField(element) {
  return element.tagName === 'INPUT' || element.tagName === 'TEXTAREA';
}

// Function to parse the page and check for input fields
function checkForInputFields() {
  const inputFields = document.querySelectorAll(
    'input[type="email"], input[type="password"], input[type="text"], input[type="tel"], textarea'
  );

  if (inputFields.length > 0) {
    // Extract information about input fields
    const inputFieldInfo = Array.from(inputFields).map((inputField) => ({
      type: inputField.type,
      id: inputField.id,
      name: inputField.name,
      value: inputField.value,
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
    chrome.runtime.sendMessage(
      {
        type: 'LOGIN_INPUT_FIELDS_FOUND',
        payload: {
          hasInputFields: true,
          inputFieldInfo,
          pageHref,
          domain,
        },
      },
      (response) => {
        console.log(response.message);
      }
    );
  }
}

// Run the checkForInputFields function when the DOM is ready
if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', checkForInputFields);
} else {
  checkForInputFields();
}

// Listen for a message from the background script indicating that we should auto fill login details
chrome.runtime.onMessage.addListener((request, sender, sendResponse) => {
  if (request.type === 'AUTOFILL_DETAILS') {
    // Find the fields on the page
    const usernameField = document.getElementById(request.username_field_id);
    const passwordField = document.getElementById(request.password_field_id);

    // Autofill the fields if found
    if (usernameField && passwordField) {
      usernameField.value = request.username;
      passwordField.value = request.password;
    } else {
      // Username and/or password fields not found on the page. // TODO: Add error handling, we probably should've gone this far if we can't find the fields.
    }
  }

  // Send an empty response
  // See https://github.com/mozilla/webextension-polyfill/issues/130#issuecomment-531531890
  sendResponse({});
  return true;
});
