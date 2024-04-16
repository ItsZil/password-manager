const puppeteer = require('puppeteer');
const path = require('path');

const EXTENSION_PATH = path.resolve(__dirname, '../../BrowserExtension/build')
const EXTENSION_ID = 'icbeakhigcgladpiblnolcogihmcdoif';

let browser;

beforeEach(async () => {
    browser = await puppeteer.launch({
        headless: 'new',
        args: [
            `--disable-extensions-except=${EXTENSION_PATH}`,
            `--load-extension=${EXTENSION_PATH}`
        ]
    });
});

afterEach(async () => {
    await browser.close();
    browser = undefined;
});

describe('Popup Tests', () => {
    test('popup renders correctly', async () => {
        const page = await browser.newPage();
        await page.goto(`chrome-extension://${EXTENSION_ID}/popup.html`);
    });

    test('popup shows setup', async () => {
      const page = await browser.newPage();
      await page.goto(`chrome-extension://${EXTENSION_ID}/popup.html`);

      const text = await page.evaluate(() => document.body.textContent);

      expect(text).toContain('Create New Vault');
      expect(text).toContain('Import Existing Vault');

      // Check if the initial-setup div is visible
      const initialSetupVisible = await page.evaluate(() => {
        const initialSetupElement = document.getElementById('initial-setup');
        return initialSetupElement.style.display !== 'none';
      });

      expect(initialSetupVisible).toBe(true);
    });

  /*test('popup shows setup complete elements', async () => {
    const page = await browser.newPage();

    // Mock chrome.storage.local.get to return setup_complete as true
    await page.evaluateOnNewDocument(() => {
      window.chrome = {
        storage: {
          local: {
            get: (keys, callback) => {
              callback({ setup_complete: true });
            }
          }
        }
      };
    });

    await page.goto(`chrome-extension://${EXTENSION_ID}/popup.html`);

    // Check if the initial setup element is hidden
    const initialSetupHidden = await page.evaluate(() => {
      const initialSetupElement = document.getElementById('initial-setup');
      return initialSetupElement.style.display === 'none';
    });

    // Check if the setup-complete element is visible
    const setupCompleteVisible = await page.evaluate(() => {
      const setupCompleteElement = document.getElementById('setup-complete');
      return setupCompleteElement.style.display !== 'none';
    });

    // Assert that the initial setup element is hidden and setup-complete element is visible
    expect(initialSetupHidden).toBe(true);
    expect(setupCompleteVisible).toBe(true);
  });*/
});
