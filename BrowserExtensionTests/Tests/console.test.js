const puppeteer = require('puppeteer');
const path = require('path');

const EXTENSION_PATH = path.resolve(__dirname, '../BrowserExtension');
const EXTENSION_ID = 'cbloejcjdeplkphdlnkiehcfligdheje';

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

/*
const background = require('../../BrowserExtension/background.js');

describe('Console Tests', () => {
    test('installed text is logged', () => {
        // Mock console.log
        const consoleLogSpy = jest.spyOn(console, 'log').mockImplementation(() => { });

        // Trigger the listener function
        background();

        // Check if the expected text is logged
        expect(consoleLogSpy).toHaveBeenCalledWith('Password Manager extension installed.');

        // Restore console.log
        consoleLogSpy.mockRestore();
    });
});
*/