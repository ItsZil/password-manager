'use strict';

document.addEventListener('DOMContentLoaded', () => {

  // Check if we need to show the initial setup options in the popup.
  chrome.storage.local.get(['setup_complete'], function (result) {
    var initialSetupElement = document.querySelector('.initial-setup');
    var setupCompleteElement = document.querySelector('.setup-complete');

    if (initialSetupElement && setupCompleteElement) {
      if (result.setup_complete) {
        // Display the initial setup options.
        initialSetupElement.style.display = 'none';
        setupCompleteElement.style.display = 'initial';
      } else {
        // Display the default initialized popup.
        initialSetupElement.style.display = 'initial';
        setupCompleteElement.style.display = 'none';
      }
    }
  });


  document.getElementById('newVaultBtn').addEventListener('click', () => {
    chrome.runtime.openOptionsPage();
  });

  document.getElementById('openVaultBtn').addEventListener('click', () => {
    chrome.runtime.openOptionsPage();
  });
});
