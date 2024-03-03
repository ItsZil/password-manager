const puppeteer = require('puppeteer');
const path = require('path');

const EXTENSION_PATH = path.resolve(__dirname, '../browserextension');
const EXTENSION_ID = 'cbloejcjdeplkphdlnkiehcfligdheje';

let browser;

beforeEach(async () => {
    browser = await puppeteer.launch({
        headless: true,
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

test('popup renders correctly', async () => {
    const page = await browser.newPage();
    await page.goto(`chrome-extension://${EXTENSION_ID}/popup.html`);
});

test('popup contains text', async () => {
    const page = await browser.newPage();
    await page.goto(`chrome-extension://${EXTENSION_ID}/popup.html`);

    const text = await page.evaluate(() => document.body.textContent);

    expect(text).toContain('Password Manager');
});