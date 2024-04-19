'use strict';

const { sendRefreshTokenRequest, sendCheckAuthRequest } = require('./requestsUtil.js');
const { jwtDecode } = require("jwt-decode");

// Function to check if the user is authenticated
export async function isAuthenticated() {
  const accessTokenCookie = await getCookie('accessToken');

  // Check if the access token cookie exists
  if (accessTokenCookie) {
    const accessToken = accessTokenCookie.value;

    // Check if decoded access token is expired
    let isExpired = true;
    try {
      const decodedToken = jwtDecode(accessToken);
      isExpired = decodedToken.exp < Date.now() / 1000;
    }
    catch (error) {
      console.error('Error decoding access token: ', error);
    }

    if (isExpired) {
      // Check if user has a refresh token
      const refreshTokenCookie = await getCookie("refreshToken");

      if (refreshTokenCookie) {
        const refreshToken = refreshTokenCookie.value;

        // Send a refresh token request to the server
        const refreshTokenResponse = await sendRefreshTokenRequest(refreshToken);

        if (refreshTokenResponse != false) {
          // Refresh token succeeded
          const newAccessToken = refreshTokenResponse.accessToken;
          const newRefreshToken = refreshTokenResponse.refreshToken;

          // Store the new access token and refresh token in cookies
          setTokens(newAccessToken, newRefreshToken);

          return true;
        }
      }
    } else {
      // Access token is valid, confirm that the vault is unlocked
      const authConfirmed = await sendCheckAuthRequest(accessToken);
      return authConfirmed;
    }
  }
  // No valid access or refresh tokens found
  return false;
}

// Function to make a request to the server to confirm the user is authenticated
// Needed in cases where the JWT secret key has been changed
async function confirmAuth() {
  console.log('attempting to confirm auth');
  const isAuthed = await sendCheckAuthRequest(getCookie('accessToken'));
  return isAuthed;
}

// Function to get the access token
export async function getAccessToken() {
  const isAuthenticatedResult = await isAuthenticated();
  if (isAuthenticatedResult) {
    const accessTokenCookie = await getCookie("accessToken");
    return accessTokenCookie.value;
  } else {
    return null;
  }
}

export function setTokens(accessToken, refreshToken) {
  setCookie('accessToken', accessToken);
  setCookie('refreshToken', refreshToken);
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
