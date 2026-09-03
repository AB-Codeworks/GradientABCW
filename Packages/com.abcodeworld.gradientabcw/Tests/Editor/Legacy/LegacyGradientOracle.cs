// Frozen reference implementation. This is a verbatim copy of the original GradientABCW.cs
// (from the VertexColourAnims project, Assets/Plugins/GradientABCW/Scripts/GradientABCW.cs),
// renamed to LegacyGradient and namespaced for test-only use. Golden tests compare the new
// evaluation core against this oracle bit-for-bit (within tolerance), so it must never be
// "fixed" or refactored — only ever replaced wholesale if a genuine transcription error is found.
#pragma warning disable

using System;
using UnityEngine;

namespace ABCodeworld.Gradients.Tests.Legacy
{
    /// <summary>
    /// Serializable gradient supporting up to 32 color keys and 32 alpha keys.
    /// Provides evaluation & LUT generation independent of UnityEngine.Gradient.
    /// Modulation parameters (reverseEval, repeats, repeatMode, evalOffset, hueShift, saturationAdj, brightnessAdj, alphaAdj) are applied
    /// ONLY at evaluation time. They never mutate key data nor invalidate the base LUT cache.
    /// Use <see cref="FlipKeysAuthoring"/> (via Reverse()/Flip Keys button) to permanently flip key times.
    /// </summary>
    [Serializable]
    internal struct LegacyGradient
    {
        private static bool ProjectIsLinear => QualitySettings.activeColorSpace == ColorSpace.Linear;

        public const int MaxColorKeys = 32;
        public const int MaxAlphaKeys = 32;

        /// <summary>Repeat domain mapping style.</summary>
        public enum RepeatMode
        {
            Mirror = 0, // Ping-pong / reflected
            Wrap = 1,   // Wrap (frac)
            Clamp = 2,  // Clamp beyond (repeats * domain)
        }

        #region Key Types
        [Serializable]
        public struct ColorKey
        {
            public Color color;               // Alpha ignored; alpha comes from alpha keys
            [Range(0f, 1f)] public float time;
            public ColorKey(Color c, float t)
            {
                c.a = 1f;
                color = c;
                time = Mathf.Clamp01(t);
            }
        }

        [Serializable]
        public struct AlphaKey
        {
            [Range(0f, 1f)] public float alpha;
            [Range(0f, 1f)] public float time;
            public AlphaKey(float a, float t)
            {
                alpha = Mathf.Clamp01(a);
                time = Mathf.Clamp01(t);
            }
        }
        #endregion

        #region Serialized Data
        [SerializeField] public ColorKey[] colorKeys;
        [SerializeField] public AlphaKey[] alphaKeys;

        [SerializeField] public bool fixedBlendMode;
        [SerializeField] public bool reverse;
        [SerializeField, Min(1e-5f)] public float repeats;
        [SerializeField] public RepeatMode repeatMode;
        [SerializeField, Range(0f, 1f)] public float evalOffset;

        // color-space modulations (applied only to modulated output)
        [SerializeField, Range(-1f, 1f)] public float hueShift;        // -1..1 turns (full spectrum both ways)
        [SerializeField, Range(-1f, 1f)] public float saturationAdj;   // -1 greyscale .. 0 none .. 1 over-saturate
        [SerializeField, Range(-1f, 1f)] public float brightnessAdj;   // -1 black .. 0 none .. 1 white
        [SerializeField, Range(-1f, 1f)] public float alphaAdj;        // -1 transparent .. 0 none .. 1 opaque
        #endregion

        #region Caching / Structural Tracking
        [NonSerialized] private Color[] _cachedBaseLut256;
        [NonSerialized] private bool _baseLutDirty;
        [NonSerialized] private int _lastStructuralHash;
        [NonSerialized] private bool _lastFixedBlendMode;
        #endregion

        #region Factory / Copy
        public LegacyGradient DeepCopy()
        {
            var copy = this;
            if (colorKeys != null)
            {
                var ck = new ColorKey[colorKeys.Length];
                Array.Copy(colorKeys, ck, colorKeys.Length);
                copy.colorKeys = ck;
            }
            if (alphaKeys != null)
            {
                var ak = new AlphaKey[alphaKeys.Length];
                Array.Copy(alphaKeys, ak, alphaKeys.Length);
                copy.alphaKeys = ak;
            }
            copy._cachedBaseLut256 = null;
            copy._baseLutDirty = true;
            copy._lastStructuralHash = 0;
            return copy;
        }

        public static LegacyGradient CreateDefault()
        {
            return new LegacyGradient
            {
                colorKeys = new[]
                {
                    new ColorKey(Color.black, 0f),
                    new ColorKey(Color.white, 1f)
                },
                alphaKeys = new[]
                {
                    new AlphaKey(1f, 0f),
                    new AlphaKey(1f, 1f)
                },
                fixedBlendMode = false,
                reverse = false,
                repeats = 1f,
                repeatMode = RepeatMode.Clamp,
                evalOffset = 0f,

                hueShift = 0f,
                saturationAdj = 0f,
                brightnessAdj = 0f,
                alphaAdj = 0f,

                _cachedBaseLut256 = null,
                _baseLutDirty = true,
                _lastStructuralHash = 0,
                _lastFixedBlendMode = false
            }.Validate();
        }
        #endregion

        #region Validation / Structural Hash
        public LegacyGradient Validate()
        {
            if (colorKeys == null || colorKeys.Length < 2)
                colorKeys = new[] { new ColorKey(Color.black, 0f), new ColorKey(Color.white, 1f) };
            if (alphaKeys == null || alphaKeys.Length < 2)
                alphaKeys = new[] { new AlphaKey(1f, 0f), new AlphaKey(1f, 1f) };

            for (int i = 0; i < colorKeys.Length; i++)
            {
                var ck = colorKeys[i];
                ck.time = Mathf.Clamp01(ck.time);
                ck.color.a = 1f;
                colorKeys[i] = ck;
            }
            for (int i = 0; i < alphaKeys.Length; i++)
            {
                var ak = alphaKeys[i];
                ak.time = Mathf.Clamp01(ak.time);
                ak.alpha = Mathf.Clamp01(ak.alpha);
                alphaKeys[i] = ak;
            }

            if (!IsSorted(colorKeys))
                Array.Sort(colorKeys, (a, b) => a.time.CompareTo(b.time));
            if (!IsSorted(alphaKeys))
                Array.Sort(alphaKeys, (a, b) => a.time.CompareTo(b.time));

            repeats = Mathf.Max(1e-5f, repeats);
            evalOffset = Mathf.Clamp01(evalOffset);
            hueShift = Mathf.Clamp(hueShift, -1f, 1f);
            saturationAdj = Mathf.Clamp(saturationAdj, -1f, 1f);
            brightnessAdj = Mathf.Clamp(brightnessAdj, -1f, 1f);
            alphaAdj = Mathf.Clamp(alphaAdj, -1f, 1f);

            if ((int)repeatMode < 0 || (int)repeatMode > 2)
                repeatMode = RepeatMode.Mirror;

            int newHash = ComputeStructuralHash();
            bool structuralChanged = newHash != _lastStructuralHash || _lastFixedBlendMode != fixedBlendMode;

            if (structuralChanged)
            {
                _lastStructuralHash = newHash;
                _lastFixedBlendMode = fixedBlendMode;
                MarkDirtyBase();
            }

            return this;
        }

        public void MarkDirtyBase() => _baseLutDirty = true;

        private static bool IsSorted(ColorKey[] arr)
        {
            for (int i = 1; i < arr.Length; i++)
                if (arr[i].time < arr[i - 1].time) return false;
            return true;
        }
        private static bool IsSorted(AlphaKey[] arr)
        {
            for (int i = 1; i < arr.Length; i++)
                if (arr[i].time < arr[i - 1].time) return false;
            return true;
        }

        private int ComputeStructuralHash()
        {
            unchecked
            {
                int h = 17;
                if (colorKeys != null)
                {
                    h = h * 31 + colorKeys.Length;
                    for (int i = 0; i < colorKeys.Length; i++)
                    {
                        var k = colorKeys[i];
                        h = h * 31 + k.time.GetHashCode();
                        h = h * 31 + k.color.r.GetHashCode();
                        h = h * 31 + k.color.g.GetHashCode();
                        h = h * 31 + k.color.b.GetHashCode();
                    }
                }
                if (alphaKeys != null)
                {
                    h = h * 31 + alphaKeys.Length;
                    for (int i = 0; i < alphaKeys.Length; i++)
                    {
                        var k = alphaKeys[i];
                        h = h * 31 + k.time.GetHashCode();
                        h = h * 31 + k.alpha.GetHashCode();
                    }
                }
                return h;
            }
        }
        #endregion

        #region Evaluation
        public Color Evaluate(float t)
        {
            t = Mathf.Clamp01(t);
            float modT = TransformT(t);
            Color c;
            if (fixedBlendMode)
            {
                c = SampleSteppedColor(modT);
                c.a = SampleSteppedAlpha(modT);
            }
            else
            {
                c = EvaluateColorSmooth(modT);
                c.a = EvaluateAlphaSmooth(modT);
            }

            c = ApplyHsbModulations(c);
            return c;
        }

        public Color EvaluateBase(float t)
        {
            t = Mathf.Clamp01(t);
            Color c;
            if (fixedBlendMode)
            {
                c = SampleSteppedColor(t);
                c.a = SampleSteppedAlpha(t);
            }
            else
            {
                c = EvaluateColorSmooth(t);
                c.a = EvaluateAlphaSmooth(t);
            }
            return c;
        }

        private float TransformT(float t)
        {
            if (t >= 1f) t = 1f - 1e-7f;

            float td = t + evalOffset;

            float r = repeats <= 0f ? 1f : repeats;
            bool repeating = r != 1f;
            bool periodic = repeatMode == RepeatMode.Wrap || repeatMode == RepeatMode.Mirror;

            if (!repeating && !periodic && repeatMode == RepeatMode.Clamp)
                td = Mathf.Clamp01(td);
            else
                td = td - Mathf.Floor(td);

            if (repeating || periodic)
            {
                float scaled = td * r;
                switch (repeatMode)
                {
                    case RepeatMode.Clamp:
                        td = Mathf.Min(scaled, 1f);
                        break;
                    case RepeatMode.Wrap:
                        td = scaled - Mathf.Floor(scaled);
                        break;
                    case RepeatMode.Mirror:
                    {
                        int cycle = (int)Mathf.Floor(scaled);
                        float f = scaled - cycle;
                        bool odd = (cycle & 1) == 1;
                        td = odd ? (1f - f) : f;
                    }
                    break;
                }
            }

            if (reverse)
                td = 1f - td;

            return Mathf.Clamp01(td);
        }

        private Color EvaluateColorSmooth(float t)
        {
            var arr = colorKeys;
            if (arr == null || arr.Length == 0) return Color.white;
            if (arr.Length == 1) return arr[0].color;

            if (t <= arr[0].time) return arr[0].color;
            if (t >= arr[arr.Length - 1].time) return arr[arr.Length - 1].color;

            int lo = 0, hi = arr.Length - 1;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) >> 1;
                if (t >= arr[mid].time) lo = mid;
                else hi = mid;
            }
            float span = arr[hi].time - arr[lo].time;
            float u = span > 1e-9f ? (t - arr[lo].time) / span : 0f;
            return Color.LerpUnclamped(arr[lo].color, arr[hi].color, u);
        }

        private float EvaluateAlphaSmooth(float t)
        {
            var arr = alphaKeys;
            if (arr == null || arr.Length == 0) return 1f;
            if (arr.Length == 1) return arr[0].alpha;

            if (t <= arr[0].time) return arr[0].alpha;
            if (t >= arr[arr.Length - 1].time) return arr[arr.Length - 1].alpha;

            int lo = 0, hi = arr.Length - 1;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) >> 1;
                if (t >= arr[mid].time) lo = mid;
                else hi = mid;
            }
            float span = arr[hi].time - arr[lo].time;
            float u = span > 1e-9f ? (t - arr[lo].time) / span : 0f;
            return Mathf.LerpUnclamped(arr[lo].alpha, arr[hi].alpha, u);
        }

        private Color ApplyHsbModulations(Color c)
        {
            // Hue + Saturation: HSV space
            Color.RGBToHSV(new Color(c.r, c.g, c.b, 1f), out float h, out float s, out float v);

            // Hue shift: -1..1 turns (full spectrum)
            h = h + hueShift;
            h = h - Mathf.Floor(h); // frac

            // Saturation: -1 -> greyscale, +1 -> oversaturate (clamped)
            if (saturationAdj >= 0f) s = Mathf.Lerp(s, 1f, saturationAdj);
            else s = Mathf.Lerp(s, 0f, -saturationAdj);
            s = Mathf.Clamp01(s);

            var rgb = Color.HSVToRGB(h, s, v, true);
            float a = c.a;

            // Brightness: -1 -> black, +1 -> white (RGB lerp to preserve intent)
            if (Mathf.Abs(brightnessAdj) > 1e-6f)
            {
                if (brightnessAdj > 0f) rgb = Color.Lerp(rgb, Color.white, Mathf.Clamp01(brightnessAdj));
                else rgb = Color.Lerp(rgb, Color.black, Mathf.Clamp01(-brightnessAdj));
            }

            // Alpha adjustment: >0 towards opaque, <0 towards transparent
            if (alphaAdj > 0f) a = Mathf.Lerp(a, 1f, Mathf.Clamp01(alphaAdj));
            else if (alphaAdj < 0f) a = Mathf.Lerp(a, 0f, Mathf.Clamp01(-alphaAdj));

            rgb.a = Mathf.Clamp01(a);
            return rgb;
        }
        #endregion

        #region LUT (Modulated / Base)
        public Color[] GetLUT256()
        {
            EnsureBaseLut();
            return GenerateModulatedCopy(_cachedBaseLut256);
        }

        public Color[] GetBaseLUT256()
        {
            EnsureBaseLut();
            var copy = new Color[_cachedBaseLut256.Length];
            Array.Copy(_cachedBaseLut256, copy, copy.Length);
            return copy;
        }

        public Color[] GenerateLUT(int size)
        {
            size = Mathf.Max(2, size);
            var arr = new Color[size];
            if (!fixedBlendMode)
            {
                for (int i = 0; i < size; i++)
                    arr[i] = Evaluate(i / (float)(size - 1));
                return arr;
            }

            for (int i = 0; i < size; i++)
            {
                float t = TransformT(i / (float)(size - 1));
                Color col = SampleSteppedColor(t);
                col.a = SampleSteppedAlpha(t);
                // Stepped path needs HSB too
                arr[i] = ApplyHsbModulations(col);
            }
            return arr;
        }

        public Color[] GenerateBaseLUT(int size)
        {
            size = Mathf.Max(2, size);
            var arr = new Color[size];
            if (!fixedBlendMode)
            {
                for (int i = 0; i < size; i++)
                    arr[i] = EvaluateBase(i / (float)(size - 1));
                return arr;
            }

            for (int i = 0; i < size; i++)
            {
                float t = i / (float)(size - 1);
                Color col = SampleSteppedColor(t);
                col.a = SampleSteppedAlpha(t);
                arr[i] = col; // base LUT intentionally ignores HSB/Alpha mods
            }
            return arr;
        }

        private void EnsureBaseLut()
        {
            if (_cachedBaseLut256 == null || _cachedBaseLut256.Length != 256 || _baseLutDirty)
            {
                _cachedBaseLut256 = GenerateBaseLUT(256);
                _baseLutDirty = false;
            }
        }

        private Color[] GenerateModulatedCopy(Color[] baseLut256)
        {
            bool noMods = !reverse &&
                          Mathf.Abs(repeats - 1f) < 1e-6f &&
                          repeatMode == RepeatMode.Clamp &&
                          evalOffset <= 1e-6f &&
                          Mathf.Abs(hueShift) <= 1e-6f &&
                          Mathf.Abs(saturationAdj) <= 1e-6f &&
                          Mathf.Abs(brightnessAdj) <= 1e-6f &&
                          Mathf.Abs(alphaAdj) <= 1e-6f;

            var outArr = new Color[baseLut256.Length];
            if (noMods)
            {
                Array.Copy(baseLut256, outArr, baseLut256.Length);
                return outArr;
            }

            int n = baseLut256.Length;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)(n - 1);
                outArr[i] = Evaluate(t); // Evaluate applies domain + HSB + Alpha mods
            }
            return outArr;
        }
        #endregion

        #region Stepped Sampling
        private Color SampleSteppedColor(float t)
        {
            if (colorKeys == null || colorKeys.Length == 0) return Color.white;
            if (colorKeys.Length == 1) return colorKeys[0].color;

            int last = colorKeys.Length - 1;
            for (int i = 0; i < last; i++)
            {
                float ti = colorKeys[i].time;
                float tj = colorKeys[i + 1].time;
                float mid = 0.5f * (ti + tj);
                if (t < mid) return colorKeys[i].color;
            }
            return colorKeys[last].color;
        }

        private float SampleSteppedAlpha(float t)
        {
            if (alphaKeys == null || alphaKeys.Length == 0) return 1f;
            if (alphaKeys.Length == 1) return alphaKeys[0].alpha;

            int last = alphaKeys.Length - 1;
            for (int i = 0; i < last; i++)
            {
                float ti = alphaKeys[i].time;
                float tj = alphaKeys[i + 1].time;
                float mid = 0.5f * (ti + tj);
                if (t < mid) return alphaKeys[i].alpha;
            }
            return alphaKeys[last].alpha;
        }
        #endregion

        #region Project-Space Helpers
        public Color EvaluateProject(float t)
        {
            var c = Evaluate(t);
            return ProjectIsLinear ? c.linear : c;
        }

        public Color[] GenerateLUTProjectSpace(int size)
        {
            var arr = GenerateLUT(size);
            if (ProjectIsLinear)
                for (int i = 0; i < arr.Length; i++)
                    arr[i] = arr[i].linear;
            return arr;
        }

        public Color[] GetLUT256ProjectSpace()
        {
            var mod = GetLUT256();
            if (ProjectIsLinear)
            {
                for (int i = 0; i < mod.Length; i++)
                    mod[i] = mod[i].linear;
            }
            return mod;
        }

        public Texture2D GenerateTexture1D(int size = 256)
        {
            var tex = new Texture2D(size, 1, TextureFormat.RGBA32, false, ProjectIsLinear)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "GradientABCWPreview"
            };
            var data = GenerateLUTProjectSpace(size);
            tex.SetPixels(data);
            tex.Apply(false);
            return tex;
        }

        public Color[] GetBaseLUT256ProjectSpace()
        {
            var baseLut = GetBaseLUT256();
            if (ProjectIsLinear)
            {
                for (int i = 0; i < baseLut.Length; i++)
                    baseLut[i] = baseLut[i].linear;
            }
            return baseLut;
        }
        #endregion

        #region Editing Helpers
        public bool CanAddColorKey => colorKeys.Length < MaxColorKeys;
        public bool CanAddAlphaKey => alphaKeys.Length < MaxAlphaKeys;

        public int AddColorKey(Color c, float t)
        {
            if (!CanAddColorKey) return -1;
            var list = new System.Collections.Generic.List<ColorKey>(colorKeys) { new ColorKey(c, t) };
            list.Sort((a, b) => a.time.CompareTo(b.time));
            colorKeys = list.ToArray();
            MarkDirtyBase();
            return IndexOfColorKeyAtTime(t);
        }

        public int AddAlphaKey(float a, float t)
        {
            if (!CanAddAlphaKey) return -1;
            var list = new System.Collections.Generic.List<AlphaKey>(alphaKeys) { new AlphaKey(a, t) };
            list.Sort((x, y) => x.time.CompareTo(y.time));
            alphaKeys = list.ToArray();
            MarkDirtyBase();
            return IndexOfAlphaKeyAtTime(t);
        }

        public void RemoveColorKey(int index)
        {
            if (colorKeys.Length <= 2 || index < 0 || index >= colorKeys.Length) return;
            var list = new System.Collections.Generic.List<ColorKey>(colorKeys);
            list.RemoveAt(index);
            colorKeys = list.ToArray();
            MarkDirtyBase();
        }

        public void RemoveAlphaKey(int index)
        {
            if (alphaKeys.Length <= 2 || index < 0 || index >= alphaKeys.Length) return;
            var list = new System.Collections.Generic.List<AlphaKey>(alphaKeys);
            list.RemoveAt(index);
            alphaKeys = list.ToArray();
            MarkDirtyBase();
        }

        public int IndexOfColorKeyAtTime(float t)
        {
            for (int i = 0; i < colorKeys.Length; i++)
                if (Mathf.Approximately(colorKeys[i].time, t)) return i;
            return -1;
        }

        public int IndexOfAlphaKeyAtTime(float t)
        {
            for (int i = 0; i < alphaKeys.Length; i++)
                if (Mathf.Approximately(alphaKeys[i].time, t)) return i;
            return -1;
        }

        public void Reverse() => FlipKeysAuthoring();

        public void FlipKeysAuthoring()
        {
            for (int i = 0; i < colorKeys.Length; i++)
            {
                var ck = colorKeys[i];
                ck.time = 1f - ck.time;
                colorKeys[i] = ck;
            }
            for (int i = 0; i < alphaKeys.Length; i++)
            {
                var ak = alphaKeys[i];
                ak.time = 1f - ak.time;
                alphaKeys[i] = ak;
            }
            Validate();
        }

        public void EvenlyDistribute()
        {
            EvenlyDistributeColors();
            EvenlyDistributeAlphas();
        }

        public void EvenlyDistributeColors()
        {
            if (colorKeys != null && colorKeys.Length > 1)
            {
                for (int i = 0; i < colorKeys.Length; i++)
                {
                    var ck = colorKeys[i];
                    ck.time = (float)i / (colorKeys.Length - 1);
                    colorKeys[i] = ck;
                }
                Validate();
            }
        }

        public void EvenlyDistributeAlphas()
        {
            if (alphaKeys != null && alphaKeys.Length > 1)
            {
                for (int i = 0; i < alphaKeys.Length; i++)
                {
                    var ak = alphaKeys[i];
                    ak.time = (float)i / (alphaKeys.Length - 1);
                    alphaKeys[i] = ak;
                }
                Validate();
            }
        }
        #endregion
    }
}
