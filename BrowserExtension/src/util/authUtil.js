'use strict';

const { str2ab } = require('./passwordUtil.js');

const {
  sendRefreshTokenRequest,
  sendCheckAuthRequest,
  sendGetPasskeyCredentialRequest,
  sendVerifyPasskeyCredentialRequest,
} = require('./requestsUtil.js');

const { jwtDecode } = require('jwt-decode');

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
    } catch (error) {
      console.error('Error decoding access token: ', error);
    }

    if (isExpired) {
      // Check if user has a refresh token
      const refreshTokenCookie = await getCookie('refreshToken');

      if (refreshTokenCookie) {
        const refreshToken = refreshTokenCookie.value;

        // Send a refresh token request to the server
        const refreshTokenResponse = await sendRefreshTokenRequest(
          refreshToken
        );

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

      // If the vault is not unlocked, then the users' tokens are invalid.
      if (!authConfirmed) {
        setTokens(null, null);
        return false;
      }
      return true;
    }
  }
  // No valid access or refresh tokens found
  return false;
}

// Function to get the access token
export async function getAccessToken() {
  const isAuthenticatedResult = await isAuthenticated();
  if (isAuthenticatedResult) {
    const accessTokenCookie = await getCookie('accessToken');
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
    chrome.cookies.get(
      { url: 'https://localhost:54782', name: name },
      (cookie) => {
        resolve(cookie);
      }
    );
  });
}

// Function to set a cookie by name and value
export function setCookie(name, value) {
  chrome.cookies.set({
    url: 'https://localhost:54782',
    name: name,
    value: value,
    httpOnly: true,
    secure: true,
  });
}

export async function authenticatePasskey(
  passkeyCredential,
  challenge,
  loginDetailsId
) {
  // Set up the public key credential request options
  const credentialId = Uint8Array.from(
    atob(passkeyCredential.credentialId),
    (c) => c.charCodeAt(0)
  );
  const challengeArrayBuffer = str2ab(challenge);

  const publicKeyCredentialRequestOptions = {
    challenge: challengeArrayBuffer,
    userVerifiation: 'required',
    allowCredentials: [
      {
        type: 'public-key',
        id: credentialId,
      },
    ],
  };

  // Get the credential from the authenticator
  const credential = await navigator.credentials.get({
    publicKey: publicKeyCredentialRequestOptions,
  });

  // Prepare the data to send to the server for verification
  const credentialIdBase64 = credential.id;
  const authenticatorDataBase64 = btoa(
    String.fromCharCode.apply(
      null,
      new Uint8Array(credential.response.authenticatorData)
    )
  );
  const signatureBase64 = btoa(
    String.fromCharCode.apply(
      null,
      new Uint8Array(credential.response.signature)
    )
  );
  const clientDataJsonBase64 = btoa(
    String.fromCharCode.apply(
      null,
      new Uint8Array(credential.response.clientDataJSON)
    )
  );

  // Encode the client data hash
  const clientDataHash = await window.crypto.subtle.digest(
    'SHA-256',
    credential.response.clientDataJSON
  );
  const clientDataHashBase64 = btoa(
    String.fromCharCode.apply(null, new Uint8Array(clientDataHash))
  );

  // Send data to server for verification
  const passkeyVerificationRequestBody = {
    loginDetailsId: loginDetailsId,
    credentialId: credentialIdBase64,
    signature: signatureBase64,
    authenticatorData: authenticatorDataBase64,
    clientDataJson: clientDataJsonBase64,
    clientDataHash: clientDataHashBase64,
  };

  const verified = await sendVerifyPasskeyCredentialRequest(
    passkeyVerificationRequestBody
  );
  console.log(verified);
  return verified;
}
