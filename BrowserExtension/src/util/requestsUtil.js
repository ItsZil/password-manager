'use strict';

// Constants
const ServerUrl = 'https://localhost:54782';

// Function to handle input fields found in the page by sending a domain login details request
// Return: A DomainLoginResponse JSON object
export async function domainLoginRequest(domainLoginRequestBody) {
  const apiEndpoint = '/api/domainloginrequest';

  try {
    const response = await fetch(`${ServerUrl}${apiEndpoint}`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify(domainLoginRequestBody),
    });

    if (response.status === 200) {
      const responseJson = await response.json();
      return responseJson;
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
    const response = await fetch(`${ServerUrl}${apiEndpoint}`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify(domainRegisterRequestBody),
    });

    if (response.status === 200) {
      return response.json();
    } else {
      console.error(
        `Failed to register for ${domainRegisterRequestBody.domain}: ${response.status} ${response.statusText}`
      );
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
export async function generatePassword() {
  const apiEndpoint = '/api/generatepassword';

  try {
    const response = await fetch(`${ServerUrl}${apiEndpoint}`, {
      method: 'GET',
    });

    if (response.status === 200) {
      const json = await response.json();
      return json.key;
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

    if (response.status === 200) {
      return true;
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
      method: 'GET'
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
      .then(response => {
        if (response.ok) {
          return true;
        } else {
          return false;
        }
      })
      .catch(error => {
        return false;
      });
  }
  catch (error) {
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
