'use strict';

// Constants
const ServerUrl = 'https://localhost:5271'; // TODO: Do not hardcode like this?

// Variables
let crypto = null;
let SharedSecret = null;

// Function to initialize the crypto object from the background script
export function init(chromeCrypto) {
  crypto = chromeCrypto;
}

// Function to initiate handshake with server in order to generate a shared secret
export async function initiateHandshake() {
  try {
    // Generate client key pair
    const clientKeyPair = await crypto.subtle.generateKey(
      { name: 'ECDH', namedCurve: 'P-256' },
      true,
      ['deriveKey', 'deriveBits']
    );

    // Export client public key
    const clientPublicKey = await crypto.subtle.exportKey(
      'spki',
      clientKeyPair.publicKey
    );
    const publicKeyBytes = new Uint8Array(clientPublicKey);
    const clientPublicKeyBase64 = btoa(String.fromCharCode.apply(null, publicKeyBytes));

    // Send client public key to server
    let response;
    try {
      response = await fetch(`${ServerUrl}/api/handshake`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({ clientPublicKey: clientPublicKeyBase64 }),
      });
    } catch (error) {
      console.error('Error sending client public key:', error);
      return;
    }

    if (!response.ok) {
      throw new Error(
        `Failed to send client public key. Status code: ${response.status}`
      );
    }

    // Read server public key
    const data = await response.json();
    const serverPublicKeyBase64 = data.serverPublicKey;
    const serverPublicKeyArrayBuffer = str2ab(atob(serverPublicKeyBase64));

    // Import server public key
    const serverPublicKey = await crypto.subtle.importKey(
      'spki',
      serverPublicKeyArrayBuffer,
      { name: 'ECDH', namedCurve: 'P-256' },
      true,
      []
    );

    // Generate raw (unhashed) shared secret
    const rawSecret = await crypto.subtle.deriveKey(
      { name: 'ECDH', public: serverPublicKey },
      clientKeyPair.privateKey,
      { name: 'AES-CBC', length: 256 },
      true,
      ['encrypt', 'decrypt']
    );

    // Export the raw shared secret key, hash it with SHA-256 and import it back
    const rawSharedSecret = await crypto.subtle.exportKey('raw', rawSecret);
    const hashedSharedSecret = await crypto.subtle.digest(
      'SHA-256',
      rawSharedSecret
    );

    SharedSecret = await crypto.subtle.importKey(
      'raw',
      hashedSharedSecret,
      { name: 'AES-CBC', length: 256 },
      true,
      ['encrypt', 'decrypt']
    );
  } catch (error) {
    console.error('Error during handshake:', error);
  }
}

// Function to encrypt a password using the shared secret
// Returns: Base64 encoded encrypted byte[] password
export async function encryptPassword(rawPassword) {
  const iv = crypto.getRandomValues(new Uint8Array(16));
  const passwordArray = str2ab(rawPassword);

  const passwordEncryptedArray = await crypto.subtle.encrypt(
    { name: 'AES-CBC', iv: iv },
    SharedSecret,
    passwordArray
  );

  // Concatenate the IV and the encrypted password
  const passwordEncryptedWithIV = new Uint8Array(iv.length + passwordEncryptedArray.byteLength);
  passwordEncryptedWithIV.set(iv);
  passwordEncryptedWithIV.set(new Uint8Array(passwordEncryptedArray), iv.length);

  // Convert passwordEncryptedWithIV into a Base64 string
  const passwordEncryptedWithIVString = String.fromCharCode.apply(
    null,
    passwordEncryptedWithIV
  );
  const passwordEncryptedWithIVBase64 = btoa(passwordEncryptedWithIVString);

  return passwordEncryptedWithIVBase64;
}

// Function to decrypt a password using the shared secret
// Returns: Plain-text password
export async function decryptPassword(rawResponsePassword) {
  // Decrypt the password using the shared secret
  const passwordEncrypted = str2ab(atob(rawResponsePassword));

  const iv = passwordEncrypted.slice(0, 16);
  const cipherText = passwordEncrypted.slice(16);
  const passwordDecrypted = await crypto.subtle.decrypt(
    { name: 'AES-CBC', iv: iv },
    SharedSecret,
    cipherText
  );

  // Convert passwordDecrypted into a plain-text string
  const passwordDecryptedArray = new Uint8Array(passwordDecrypted);
  const passwordDecryptedString = String.fromCharCode.apply(
    null,
    passwordDecryptedArray
  );

  return passwordDecryptedString;
}

// ----------------
// Helper functions
// ----------------

// Function to convert a string to an ArrayBuffer
export function str2ab(str) {
  const buf = new ArrayBuffer(str.length);
  const bufView = new Uint8Array(buf);
  for (let i = 0, strLen = str.length; i < strLen; i++) {
    bufView[i] = str.charCodeAt(i);
  }
  return buf;
}
