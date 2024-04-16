'use strict';

const { sendRefreshTokenRequest } = require('./requestsUtil.js');
const { jwtDecode } = require("jwt-decode");

// Function to check if the user is authenticated
export async function isAuthenticated() {
  const accessTokenCookie = await getCookie("accessToken");

  console.log('checking auth');

  // Check if the access token cookie exists
  if (accessTokenCookie) {
    console.log('has access token.. checking if expired')
    const accessToken = accessTokenCookie.value;

    // Check if decoded access token is expired
    const decodedToken = jwtDecode(accessToken);
    const isExpired = decodedToken.exp < Date.now() / 1000;

    if (isExpired) {
      console.log('access token is expired');
      // Check if user has a refresh token
      const refreshTokenCookie = await getCookie("refreshToken");
      if (refreshTokenCookie) {
        console.log('has refresh token cookie')
        const refreshToken = refreshTokenCookie.value;

        // Send a refresh token request to the server
        const refreshTokenResponse = await sendRefreshTokenRequest(refreshToken);
        if (refreshTokenResponse != false) {
          // Refresh token succeeded
          const newAccessToken = refreshTokenResponse.accessToken;
          const newRefreshToken = refreshTokenResponse.refreshToken;

          console.log('refresh succeeded! new token: ', newAccessToken);

          // Store the new access token and refresh token in cookies
          setCookie("accessToken", newAccessToken);
          setCookie("refreshToken", newRefreshToken);

          return true;
        }
        else {
          console.log('refresh failed, got ' + refreshTokenResponse);
        }
      }
    } else {
      console.log('access token is valid');
      // Access token is valid
      return true;
    }
  }

  console.log('no tokens found');
  // No valid access or refresh tokens found
  return false;
}

// Function to retrieve a cookie by name
export function getCookie(name) {
  return new Promise((resolve) => {
    chrome.cookies.get({ url: 'https://localhost:54782', name: name }, (cookie) => {
      resolve(cookie);
    });
  });
}

// Function to set a cookie by name and value
export function setCookie(name, value) {
  chrome.cookies.set({
    url: 'https://localhost:54782',
    name: name,
    value: value,
    httpOnly: true,
    secure: true
  });
}
