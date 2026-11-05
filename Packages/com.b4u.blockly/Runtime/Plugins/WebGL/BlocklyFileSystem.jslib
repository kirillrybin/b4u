// WebGL keeps files written under Application.persistentDataPath in an in-memory file system that is only copied to the
// browser's IndexedDB when asked to. Without this call a saved program is gone after the page is reloaded.
mergeInto(LibraryManager.library, {
  Blockly_SyncFileSystem: function () {
    if (typeof FS === 'undefined' || !FS.syncfs) return;
    FS.syncfs(false, function (err) {
      if (err) console.error('[Blockly] could not store the files in the browser: ' + err);
    });
  }
});
