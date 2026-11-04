const Blockly = require('blockly/core');
Blockly.setLocale(require('blockly/msg/en'));
require('blockly/blocks');
const { javascriptGenerator } = require('blockly/javascript');
module.exports = { Blockly, javascriptGenerator };
