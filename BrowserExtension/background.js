'use strict';

chrome.runtime.onInstalled.addListener(() => {
  console.log('Password Manager extension installed.');
  // Initialize extension state and set up communication with the local server here.
});
