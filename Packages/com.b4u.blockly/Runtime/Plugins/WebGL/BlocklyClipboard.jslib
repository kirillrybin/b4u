// A WebGL build cannot read the system clipboard synchronously: the browser only hands it out asynchronously, and only while the
// page is handling a user action. This asks for it and reports back with SendMessage to the receiver object. Writing needs the
// same user action, and Unity's own clipboard setter does not reach the browser's clipboard.
mergeInto(LibraryManager.library, {
  Blockly_WriteClipboard: function (textPtr) {
    var text = UTF8ToString(textPtr);
    var fallback = function () {
      try {
        var area = document.createElement('textarea');
        area.value = text; area.style.position = 'fixed'; area.style.opacity = '0';
        document.body.appendChild(area); area.focus(); area.select();
        document.execCommand('copy');
        document.body.removeChild(area);
      } catch (e) { console.error('[Blockly] could not copy: ' + e); }
    };
    if (navigator.clipboard && navigator.clipboard.writeText) navigator.clipboard.writeText(text).catch(fallback);
    else fallback();
  },

  Blockly_ReadClipboard: function (objectPtr, okPtr, failPtr) {
    var obj = UTF8ToString(objectPtr), ok = UTF8ToString(okPtr), fail = UTF8ToString(failPtr);
    var send = function (method, text) {
      try { SendMessage(obj, method, text); } catch (e) { console.error('[Blockly] clipboard: ' + e); }
    };
    if (!navigator.clipboard || !navigator.clipboard.readText) { send(fail, 'The browser cannot read the clipboard'); return; }
    navigator.clipboard.readText().then(function (text) { send(ok, text || ''); }, function (err) { send(fail, String(err)); });
  }
});
