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
      return response.json();
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
