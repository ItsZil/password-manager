'use strict';

const { jwtDecode } = require("jwt-decode");

// Function to check if the user is authenticated
export async function isAuthenticated() {
  return new Promise((resolve, reject) => {
    chrome.storage.local.get(['token'], function (result) {
      var token = result.token;
      if (token) {
        var decodedToken = jwtDecode(token);
        var isTokenExpired = decodedToken.exp < Date.now() / 1000;
        resolve(!isTokenExpired);
      } else {
        resolve(false);
      }
    });
  });
}
