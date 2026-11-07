using System;
using System.Collections.Generic;
using UnityEngine;

namespace Blockly.UI
{
    /// <summary>
    /// Pictures for <c>field_image</c> blocks and toolbox category icons. A "src" is looked up in this order:
    /// a sprite registered under that name, the <see cref="Resolver"/> callback, then a Sprite in a Resources folder
    /// (the name without its file extension). Nothing is downloaded: the app decides where pictures come from.
    /// </summary>
    public static class BlockImages
    {
        static readonly Dictionary<string, Sprite> registered = new Dictionary<string, Sprite>();

        /// <summary>Optional hook for pictures that live elsewhere (an addressable, a texture atlas...).</summary>
        public static Func<string, Sprite> Resolver;

        public static void Register(string src, Sprite sprite) { registered[src] = sprite; }

        public static void Unregister(string src) { registered.Remove(src); }

        public static void Clear()
        {
            registered.Clear();
            Resolver = null;
        }

        /// <summary>The sprite for <paramref name="src"/>, or null when there is none.</summary>
        public static Sprite Get(string src)
        {
            if (string.IsNullOrEmpty(src)) return null;
            if (registered.TryGetValue(src, out var s) && s != null) return s;
            var r = Resolver?.Invoke(src);
            if (r != null) return r;
            int dot = src.LastIndexOf('.');
            string path = dot > src.LastIndexOf('/') ? src.Substring(0, dot) : src;
            return Resources.Load<Sprite>(path);
        }
    }
}
