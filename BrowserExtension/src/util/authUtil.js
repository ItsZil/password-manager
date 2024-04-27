'use strict';

const {
  getServerAddress,
  sendRefreshTokenRequest,
  sendCheckAuthRequest,
  sendVerifyPasskeyCredentialRequest,
} = require('./requestsUtil.js');

const { jwtDecode } = require('jwt-decode');

// Function to check if the user is authenticated
export async function isAuthenticated() {
  const accessTokenCookie = await getCookie('accessToken');

  // Check if the access token cookie exists
  if (accessTokenCookie) {
    const accessToken = accessTokenCookie.value;

    if (accessToken.length < 1) {
      await setTokens(null, null);
    }

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

        if (refreshToken.length < 1) {
          await setTokens(null, null);
          return false;
        }

        // Send a refresh token request to the server
        const refreshTokenResponse = await sendRefreshTokenRequest(
          refreshToken
        );

        if (refreshTokenResponse != false) {
          // Refresh token succeeded
          const newAccessToken = refreshTokenResponse.accessToken;
          const newRefreshToken = refreshTokenResponse.refreshToken;

          // Store the new access token and refresh token in cookies
          await setTokens(newAccessToken, newRefreshToken);

          return true;
        }
      }
    } else {
      // Access token is valid, confirm that the vault is unlocked
      const authConfirmed = await sendCheckAuthRequest(accessToken);

      // If the vault is not unlocked, then the users' tokens are invalid.
      if (!authConfirmed) {
        await setTokens(null, null);
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

export async function setTokens(accessToken, refreshToken) {
  await setCookie('accessToken', accessToken);
  await setCookie('refreshToken', refreshToken);
}

// Function to retrieve a cookie by name
export async function getCookie(name) {
  const url = await getServerAddress();

  return new Promise((resolve) => {
    chrome.cookies.get(
      { url: url, name: name },
      (cookie) => {
        resolve(cookie);
      }
    );
  });
}

// Function to set a cookie by name and value
export async function setCookie(name, value) {
  const url = await getServerAddress();
  await chrome.cookies.set({
    url: url,
    name: name,
    value: value,
    httpOnly: true,
    secure: true,
  });
}

export async function authenticatePasskey(
  passkeyCredential,
  challenge,
  loginDetailsId,
  sourceId,
  crypto,
  isForLogin,
  credentialAlreadyRetrieved = false,
) {

  let credential = passkeyCredential;
  if (!credentialAlreadyRetrieved) {
    credential = await getUserPasskeyCredentials(passkeyCredential, challenge);
  }

  // Prepare the data to send to the server for verification
  const credentialIdBase64 = credential.id;
  const authenticatorDataBase64 = credential.response.authenticatorData;
  const signatureBase64 = credential.response.signature;
  const clientDataJsonBase64 = credential.response.clientDataJSON;

  // Encode the client data hash
  const clientDataAB = Uint8Array.from(
    atob(clientDataJsonBase64),
    (c) => c.charCodeAt(0)
  );
  const clientDataHash = await crypto.subtle.digest(
    'SHA-256',
    clientDataAB
  );
  const clientDataHashBase64 = btoa(
    String.fromCharCode.apply(null, new Uint8Array(clientDataHash))
  );

  // Send data to server for verification
  const passkeyVerificationRequestBody = {
    sourceId: sourceId,
    isForLogin: isForLogin,
    loginDetailsId: loginDetailsId,
    credentialId: credentialIdBase64,
    signature: signatureBase64,
    authenticatorData: authenticatorDataBase64,
    clientDataJson: clientDataJsonBase64,
    clientDataHash: clientDataHashBase64
  };

  const verified = await sendVerifyPasskeyCredentialRequest(
    passkeyVerificationRequestBody
  );
  return verified;
}

export async function getUserPasskeyCredentials(passkeyCredential, challengeArrayBuffer) {
  // Set up the public key credential request options
  const credentialId = Uint8Array.from(
    atob(passkeyCredential),
    (c) => c.charCodeAt(0)
  );

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

  return credential;
}
