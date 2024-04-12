'use strict';

const { merge } = require('webpack-merge');

const common = require('./webpack.common.js');
const PATHS = require('./paths');

// Merge webpack configuration files
const config = (env, argv) =>
  merge(common, {
    entry: {
      main: PATHS.src + '/main.js',
      popup: PATHS.src + '/popup.js',
      options: PATHS.src + '/options.js',
      setup: PATHS.src + '/setup.js',
      import: PATHS.src + '/import.js',

      contentScript: PATHS.src + '/contentScript.js',
      background: PATHS.src + '/background.js',
    },
    devtool: argv.mode === 'production' ? false : 'source-map',
  });

module.exports = config;
