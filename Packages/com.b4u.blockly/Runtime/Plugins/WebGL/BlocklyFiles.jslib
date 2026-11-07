// File export and import in a browser: a download of the program as a file, and a file picker to read one back.
mergeInto(LibraryManager.library, {
  Blockly_DownloadText: function (namePtr, textPtr) {
    var name = UTF8ToString(namePtr), text = UTF8ToString(textPtr);
    var url = URL.createObjectURL(new Blob([text], { type: 'application/json' }));
    var link = document.createElement('a');
    link.href = url; link.download = name; link.style.display = 'none';
    document.body.appendChild(link); link.click(); document.body.removeChild(link);
    setTimeout(function () { URL.revokeObjectURL(url); }, 10000);
  },

  // Shows a small panel with a real file input and answers with SendMessage(object, okMethod, "<file name>\n<file text>"), or
  // failMethod with a reason ("cancelled" when the panel is closed). The input has to be tapped by the user: browsers only open the
  // file picker for a touch or click they handle themselves, not for one Unity passes on a frame later.
  Blockly_PickTextFile: function (objectPtr, okPtr, failPtr) {
    var obj = UTF8ToString(objectPtr), ok = UTF8ToString(okPtr), fail = UTF8ToString(failPtr);
    var send = function (method, text) {
      try { SendMessage(obj, method, text); } catch (e) { console.error('[Blockly] file: ' + e); }
    };
    var old = document.getElementById('blockly-file-panel');
    if (old) old.parentNode.removeChild(old);

    var backdrop = document.createElement('div');
    backdrop.id = 'blockly-file-panel';
    backdrop.style.cssText = 'position:fixed;left:0;top:0;right:0;bottom:0;z-index:2147483647;background:rgba(0,0,0,0.55);display:flex;align-items:center;justify-content:center;font-family:sans-serif';
    var panel = document.createElement('div');
    panel.style.cssText = 'background:#2b2b33;color:#eee;padding:20px;border-radius:10px;max-width:88vw;box-sizing:border-box;text-align:center';
    var title = document.createElement('div');
    title.textContent = 'Choose a program file (.json or .xml)';
    title.style.cssText = 'margin-bottom:16px;font-size:16px';
    var input = document.createElement('input');
    input.type = 'file'; input.id = 'blockly-file-input';
    // Document types only. With no filter Android offers its photo and video picker; octet-stream is what Android often reports for a downloaded .json.
    input.accept = '.json,.xml,application/json,application/xml,text/xml,text/plain,application/octet-stream';
    input.style.cssText = 'display:block;margin:0 auto 16px;max-width:100%;color:#eee;font-size:16px';
    var cancel = document.createElement('button');
    cancel.textContent = 'Cancel';
    cancel.style.cssText = 'padding:10px 24px;font-size:16px;border:0;border-radius:6px;background:#555a78;color:#fff';

    var close = function () { if (backdrop.parentNode) backdrop.parentNode.removeChild(backdrop); };
    cancel.onclick = function () { close(); send(fail, 'cancelled'); };
    backdrop.onclick = function (e) { if (e.target === backdrop) { close(); send(fail, 'cancelled'); } };
    input.onchange = function () {
      var file = input.files && input.files[0];
      if (!file) return;
      close();
      var reader = new FileReader();
      reader.onload = function () { send(ok, file.name + '\n' + reader.result); };
      reader.onerror = function () { send(fail, 'The file could not be read'); };
      reader.readAsText(file);
    };
    panel.appendChild(title); panel.appendChild(input); panel.appendChild(cancel);
    backdrop.appendChild(panel);
    document.body.appendChild(backdrop);
  }
});
