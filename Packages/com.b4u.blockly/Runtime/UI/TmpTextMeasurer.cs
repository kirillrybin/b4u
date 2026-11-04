using System.Collections.Generic;
using Blockly.Layout;
using TMPro;
using UnityEngine;

namespace Blockly.UI
{
    /// <summary>Measures text with the same TextMeshPro font and size the fields are drawn with.</summary>
    public sealed class TmpTextMeasurer : ITextMeasurer
    {
        readonly TMP_Text template;
        readonly Dictionary<string, float> cache = new Dictionary<string, float>();

        public TmpTextMeasurer(TMP_Text template) { this.template = template; }

        public float Width(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0f;
            if (cache.TryGetValue(text, out var w)) return w;
            w = Mathf.Ceil(template.GetPreferredValues(text).x);
            cache[text] = w;
            return w;
        }

        public void Clear() => cache.Clear();
    }
}
