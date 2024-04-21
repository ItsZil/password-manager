'use strict';

const { getAccessToken } = require('./authUtil.js');

// Constants
const ServerUrl = 'https://localhost:54782';

// Function to handle input fields found in the page by sending a domain login details request
// Return: A DomainLoginResponse JSON object
export async function domainLoginRequest(domainLoginRequestBody) {
  const apiEndpoint = '/api/domainloginrequest';

  try {
    const accessToken = await getAccessToken();

    const response = await fetch(`${ServerUrl}${apiEndpoint}`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        Authorization: `Bearer ${accessToken}`,
      },
      body: JSON.stringify(domainLoginRequestBody),
    });

    if (response.status === 200) {
      const responseJson = await response.json();
      return responseJson;
    } else if (response.status == 401) {
      // TODO: not logged in
    } else {
      console.error(
        `Failed to login to ${domainLoginRequestBody.domain}: ${response.status} ${response.statusText}`
      );
    }
  } catch (error) {
    console.error('Error retrieving response: ', error);
    throw error;
  }
}

// Function to handle input fields found in the page by sending a domain register details request
// Return: A DomainRegisterResponse JSON object
export async function domainRegisterRequest(domainRegisterRequestBody) {
  const apiEndpoint = '/api/domainregisterrequest';

  try {
    const accessToken = await getAccessToken();

    const response = await fetch(`${ServerUrl}${apiEndpoint}`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        Authorization: `Bearer ${accessToken}`,
      },
      body: JSON.stringify(domainRegisterRequestBody),
    });

    if (response.status === 200) {
      return response.json();
    } else if (response.status == 401) {
      // TODO: not logged in
    } else {
      console.error(
        `Failed to register for ${domainRegisterRequestBody.domain}: ${response.status} ${response.statusText}`
      );
      return response.status;
    }
  } catch (error) {
    console.error('Error retrieving response: ', error);
    throw error;
  }
}

// Function to check if an absolute path is valid
// Returns: A boolean indicating if the path is valid
export async function isAbsolutePathValid(absolutePathUri) {
  const apiEndpoint = '/api/isabsolutepathvalid';

  try {
    const response = await fetch(`${ServerUrl}${apiEndpoint}`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({ absolutePathUri: absolutePathUri }),
    });

    if (response.status === 200) {
      const json = await response.json();
      return json.pathValid;
    } else {
      console.error(
        `Failed to check if path is valid for ${absolutePathUri}: ${response.status} ${response.statusText}`
      );
      return false;
    }
  } catch (error) {
    console.error('Error retrieving response: ', error);
    return false;
  }
}

// Function to generate a new secure password
// Returns: A string containing the base64 encoded, encrypted password. False if unsuccessful
export async function generatePassword(sourceId) {
  const apiEndpoint = `/api/generatepassword?sourceId=${sourceId}`;

  try {
    const response = await fetch(`${ServerUrl}${apiEndpoint}`, {
      method: 'GET',
    });

    if (response.status === 200) {
      const json = await response.json();
      return json.password;
    } else {
      console.error(
        `Failed to retrieve generated password: ${response.status} ${response.statusText}`
      );
      return false;
    }
  } catch (error) {
    console.error('Error retrieving response: ', error);
    return false;
  }
}

// Function to create or import a vault during the setup process
// Returns: A boolean indicating if a vault connection was successfully opened
export async function sendSetupVaultRequest(setupVaultRequestBody) {
  const apiEndpoint = '/api/setupvault';

  try {
    const response = await fetch(`${ServerUrl}${apiEndpoint}`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify(setupVaultRequestBody),
    });

    if (response.status === 201) {
      const json = await response.json();
      return json;
    } else {
      console.error(
        `Failed to setup new vault: ${response.status} ${response.statusText}`
      );
      return false;
    }
  } catch (error) {
    console.error('Error retrieving response: ', error);
    return false;
  }
}

// Function to check if a user has an existing vault
// Returns: A boolean indicating if the user has an existing vault
export async function sendHasExistingVaultRequest() {
  const apiEndpoint = '/api/hasexistingvault';

  try {
    const response = await fetch(`${ServerUrl}${apiEndpoint}`, {
      method: 'GET',
    });

    if (response.status === 200) {
      const result = await response.json();
      return result;
    } else {
      console.error(
        `Failed to check if user has existing vault: ${response.status} ${response.statusText}`
      );
      return false;
    }
  } catch (error) {
    console.error('Error retrieving response: ', error);
    return false;
  }
}

// Function to check if the server is reachable
// Returns: A boolean indicating if the server is reachable
export async function checkIfServerReachable() {
  try {
    return await fetch(ServerUrl, { method: 'HEAD' })
      .then((response) => {
        if (response.ok) {
          return true;
        } else {
          return false;
        }
      })
      .catch((error) => {
        return false;
      });
  } catch (error) {
    return false;
  }
}

// Function to send a request to unlock the vault
// Returns: A UnlockVaultRequestResponse
export async function sendUnlockVaultRequest(unlockVaultRequestBody) {
  const apiEndpoint = '/api/unlockvault';

  try {
    const response = await fetch(`${ServerUrl}${apiEndpoint}`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify(unlockVaultRequestBody),
    });

    if (response.status === 201) {
      return response.json();
    } else {
      console.error(
        `Failed to unlock vault: ${response.status} ${response.statusText}`
      );
      return false;
    }
  } catch (error) {
    console.error('Error retrieving response: ', error);
    return false;
  }
}

// Function to send a request to refresh the access token
// Returns a RefreshTokenResponse
export async function sendRefreshTokenRequest(refreshToken) {
  const apiEndpoint = '/api/refreshtoken';

  try {
    const response = await fetch(`${ServerUrl}${apiEndpoint}`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({ refreshToken: refreshToken }),
    });

    if (response.status === 201) {
      const json = await response.json();
      return json;
    } else {
      console.error(
        `Failed to refresh token: ${response.status} ${response.statusText}`
      );
      return false;
    }
  } catch (error) {
    console.error('Error retrieving response: ', error);
    return false;
  }
}

// Function to send a request to lock the vault
// Returns a boolean indicating if the vault was successfully locked
export async function sendLockVaultRequest() {
  const apiEndpoint = '/api/lockvault';

  try {
    const response = await fetch(`${ServerUrl}${apiEndpoint}`, {
      method: 'GET',
    });

    if (response.status === 204) {
      return true;
    } else {
      console.error(
        `Failed to lock vault: ${response.status} ${response.statusText}`
      );
      return false;
    }
  } catch (error) {
    console.error('Error retrieving response: ', error);
    return false;
  }
}

// Function to send a request to see if the user is authenticated
// Returns a boolean indicating if the user is authenticated
export async function sendCheckAuthRequest(accessToken) {
  const apiEndpoint = '/api/checkauth';

  try {
    const response = await fetch(`${ServerUrl}${apiEndpoint}`, {
      method: 'GET',
      headers: {
        Authorization: `Bearer ${accessToken}`,
      },
    });

    if (response.status === 200) {
      return true;
    } else {
      return false;
    }
  } catch (error) {
    console.error('Error retrieving response: ', error);
    return false;
  }
}

// Function to send a request to retrieve the count of login details
// Returns: The count of login details
export async function sendLoginDetailsCountRequest() {
  const apiEndpoint = '/api/logindetailscount';
  const accessToken = await getAccessToken();

  try {
    const response = await fetch(`${ServerUrl}${apiEndpoint}`, {
      method: 'GET',
      headers: {
        Authorization: `Bearer ${accessToken}`,
      },
    });

    if (response.status === 200) {
      const count = await response.json();
      return count;
    } else {
      console.error(
        `Failed to retrieve login details count: ${response.status} ${response.statusText}`
      );
      return false;
    }
  } catch (error) {
    console.error('Error retrieving response: ', error);
    return false;
  }
}

// Function to send a request to retrieve a batch of login details for view
// Returns: List<LoginDetailsResponse>
export async function sendLoginDetailsViewRequest(page) {
  const apiEndpoint = `/api/logindetails?page=${page}`;
  const accessToken = await getAccessToken();

  try {
    const response = await fetch(`${ServerUrl}${apiEndpoint}`, {
      method: 'GET',
      headers: {
        Authorization: `Bearer ${accessToken}`,
      },
    });

    if (response.status === 200) {
      const json = await response.json();
      return json;
    } else {
      console.error(
        `Failed to retrieve login details: ${response.status} ${response.statusText}`
      );
      return false;
    }
  } catch (error) {
    console.error('Error retrieving response: ', error);
    return false;
  }
}

// Function to send a request to retrieve a login details' password by ID
// Returns: A base64 encoded, shared key encrypted password
export async function sendLoginDetailsPasswordRequest(
  domainLoginPasswordRequestBody
) {
  const apiEndpoint = `/api/logindetailspassword`;
  const accessToken = await getAccessToken();

  try {
    const response = await fetch(`${ServerUrl}${apiEndpoint}`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        Authorization: `Bearer ${accessToken}`,
      },
      body: JSON.stringify(domainLoginPasswordRequestBody),
    });

    if (response.status === 200) {
      const json = await response.json();
      return json.passwordB64;
    } else {
      console.error(
        `Failed to retrieve login details password: ${response.status} ${response.statusText}`
      );
      return false;
    }
  } catch (error) {
    console.error('Error retrieving response: ', error);
    return false;
  }
}

// Function to send a request to store a new passkey for a specific login detail ID
// Returns: A boolean indicating if the passkey was successfully stored
export async function sendCreatePasskeyRequest(passkeyCreationRequestBody) {
  const apiEndpoint = '/api/passkey';
  const accessToken = await getAccessToken();

  try {
    const response = await fetch(`${ServerUrl}${apiEndpoint}`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        Authorization: `Bearer ${accessToken}`,
      },
      body: JSON.stringify(passkeyCreationRequestBody),
    });

    if (response.status === 201) {
      return true;
    } else {
      console.error(
        `Failed to create passkey: ${response.status} ${response.statusText}`
      );
      return false;
    }
  } catch (error) {
    console.error('Error retrieving response: ', error);
    return false;
  }
}

// Function to send a request to retrieve a passkey for a specific login detail ID
// Returns: A PasskeyCredentialResponse object
export async function sendGetPasskeyCredentialRequest(
  sourceId,
  loginDetailsId
) {
  const apiEndpoint = `/api/passkey?sourceId=${sourceId}&loginDetailsId=${loginDetailsId}`;
  const accessToken = await getAccessToken();

  try {
    const response = await fetch(`${ServerUrl}${apiEndpoint}`, {
      method: 'GET',
      headers: {
        'Content-Type': 'application/json',
        Authorization: `Bearer ${accessToken}`,
      },
    });

    if (response.status === 200) {
      const json = await response.json();
      return json;
    } else {
      console.error(
        `Failed to retrieve passkey credential: ${response.status} ${response.statusText}`
      );
      return false;
    }
  } catch (error) {
    console.error('Error retrieving response: ', error);
    return false;
  }
}

// Function to send a request to verfy a passkey credential
// Returns: A boolean indicating if the passkey credential is valid
export async function sendVerifyPasskeyCredentialRequest(
  passkeyVerificationRequestBody
) {
  const apiEndpoint = '/api/passkey/verify';
  const accessToken = await getAccessToken();

  try {
    const response = await fetch(`${ServerUrl}${apiEndpoint}`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        Authorization: `Bearer ${accessToken}`,
      },
      body: JSON.stringify(passkeyVerificationRequestBody),
    });

    if (response.status === 200) {
      return true;
    } else {
      console.error(
        `Failed to verify passkey credential: ${response.status} ${response.statusText}`
      );
      return false;
    }
  } catch (error) {
    console.error('Error retrieving response: ', error);
    return false;
  }
}
