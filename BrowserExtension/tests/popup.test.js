const puppeteer = require('puppeteer');
const path = require('path');

const EXTENSION_PATH = path.resolve(__dirname, '../../BrowserExtension');
const EXTENSION_ID = 'icbeakhigcgladpiblnolcogihmcdoif';

let browser;

beforeEach(async () => {
    browser = await puppeteer.launch({
        headless: "new",
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
    test('Popup renders correctly', async () => {
        const page = await browser.newPage();
        await page.goto(`chrome-extension://${EXTENSION_ID}/popup.html`);
    });

    test('Popup contains text', async () => {
        const page = await browser.newPage();
        await page.goto(`chrome-extension://${EXTENSION_ID}/popup.html`);

        const text = await page.evaluate(() => document.body.textContent);

        expect(text).toContain('Password Manager');
    });
});