'use strict';

$('#newVaultBtn').on("click", function () {
  open('setup.html');
});

$('#importVaultBtn').on("click", function () {
  open('import.html');
});

$(() => {
  chrome.storage.local.get(['setup_complete'], function (result) {
    const initialSetupElement = $('#initial-setup');
    const setupCompleteElement = $('#setup-complete');

    if (initialSetupElement && setupCompleteElement) {
      if (result.setup_complete) {
        // Display the initial setup options.
        initialSetupElement.hide();
        setupCompleteElement.show();
      } else {
        // Display the default initialized popup.
        initialSetupElement.show();
        setupCompleteElement.hide();
      }
    }
  });
});
