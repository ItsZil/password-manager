'use strict';

document.addEventListener('DOMContentLoaded', () => {
  chrome.storage.local.get(['setup_complete'], function(result) {
    if (result.setup_complete) {
      document.querySelector('.app').style.display = 'none';
    }
  });

  document.getElementById('newVaultBtn').addEventListener('click', () => {
    chrome.runtime.openOptionsPage();
  });

  document.getElementById('openVaultBtn').addEventListener('click', () => {
    chrome.runtime.openOptionsPage();
  });
});
