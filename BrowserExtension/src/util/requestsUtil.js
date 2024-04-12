'use strict';

// Constants
const ServerUrl = 'https://localhost:5271'; // TODO: Do not hardcode like this?

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

// Function to generate a new pragma key
// Returns: A string containing the base64 encoded, encrypted pragma key, false if unsuccessful
export async function generatePragmaKey() {
  const apiEndpoint = '/api/generatepragmakey';

  try {
    const response = await fetch(`${ServerUrl}${apiEndpoint}`, {
      method: 'GET',
    });

    if (response.status === 200) {
      const json = await response.json();
      return json.key;
    } else {
      console.error(
        `Failed to retrieve generated pragma key: ${response.status} ${response.statusText}`
      );
      return false;
    }
  } catch (error) {
    console.error('Error retrieving response: ', error);
    return false;
  }
}

// Function to create a new vault during the setup process
// Returns: A boolean indicating if the vault was successfully created
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
