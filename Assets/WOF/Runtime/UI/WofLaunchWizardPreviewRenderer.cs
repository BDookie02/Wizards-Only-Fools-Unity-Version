using System;
using UnityEngine;
using UnityEngine.UI;

namespace WOF
{
    /// <summary>
    /// Runtime port of the canonical React LaunchCharacterPreview's static front-facing
    /// holding frame. It keeps customization interactive without substituting generic art.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public sealed class WofLaunchWizardPreviewRenderer : MonoBehaviour
    {
        private const int LaunchTextureSize = 360;
        private const float CanvasScale = 2f;
        private const float AvatarScale = 1.35f * 1.35f * CanvasScale;

        private Image _image;
        private Texture2D _texture;
        private Sprite _sprite;
        private Color32[] _pixels;
        private bool _inventoryStyle;
        private int _textureWidth = LaunchTextureSize;
        private int _textureHeight = LaunchTextureSize;
        private float _avatarOffsetX = 26f;
        private float _avatarOffsetY = 28f;

        private void Awake()
        {
            _image = GetComponent<Image>();
        }

        private void OnDestroy()
        {
            ReleaseTexture();
        }

        public void UseInventoryStyle()
        {
            if (_inventoryStyle)
            {
                return;
            }

            ReleaseTexture();
            _inventoryStyle = true;
            _textureWidth = 440;
            _textureHeight = 376;
            _avatarOffsetX = 48f;
            _avatarOffsetY = 21f;
        }

        public void Render(string topHex, string skinHex, string hairHex, string hatStyle, string hairStyle)
        {
            _image ??= GetComponent<Image>();
            EnsureTexture();
            Array.Fill(_pixels, _inventoryStyle
                ? new Color32(2, 9, 6, 255)
                : new Color32(9, 5, 16, 255));

            var canvasWidth = _inventoryStyle ? 220f : 180f;
            var canvasHeight = _inventoryStyle ? 188f : 180f;
            if (_inventoryStyle)
            {
                DrawInventoryGlow();
            }
            var grid = _inventoryStyle
                ? new Color32(3, 21, 15, 255)
                : new Color32(12, 27, 36, 255);
            for (var x = 0; x < canvasWidth; x += 12)
            {
                CanvasRect(x, 0f, 1f, canvasHeight, grid);
            }
            for (var y = 0; y < canvasHeight; y += 12)
            {
                CanvasRect(0f, y, canvasWidth, 1f, grid);
            }
            if (_inventoryStyle)
            {
                CanvasOutline(12f, 10f, 196f, 168f, 2f, new Color32(48, 75, 66, 255));
                CanvasOutline(22f, 20f, 176f, 148f, 1f, new Color32(56, 59, 24, 255));
            }
            else
            {
                CanvasOutline(12f, 10f, 156f, 160f, 2f, new Color32(89, 76, 18, 255));
            }

            var skin = ParseHex(skinHex, new Color32(214, 207, 145, 255));
            var top = ParseHex(topHex, new Color32(124, 58, 237, 255));
            var hair = ParseHex(hairHex, new Color32(63, 42, 29, 255));
            var pants = new Color32(51, 65, 85, 255);
            var shoes = new Color32(31, 41, 55, 255);

            DrawPixelEllipse(18f, 56f, 28f, 5f, new Color32(0, 0, 0, 64));
            DrawLeg(28f, 50f, 25f, 55f, Shade(pants, -0.16f), shoes);
            DrawLeg(36f, 50f, 39f, 55f, pants, Shade(shoes, -0.08f));
            DrawArm(23f, 37f, 15f, 55f, skin, top);
            DrawArm(41f, 37f, 49f, 55f, skin, top);

            Block(22f, 34f, 20f, 19f, top);
            Block(24f, 34f, 16f, 3f, Shade(top, 0.16f));
            Block(38f, 37f, 3f, 13f, Shade(top, -0.14f));

            var lightSkin = Shade(skin, 0.16f);
            var darkSkin = Shade(skin, -0.18f);
            DrawPixelEllipse(18.5f, 8f, 27f, 27f, skin);
            Block(20f, 14f, 6f, 10f, lightSkin);
            Block(42f, 16f, 5f, 12f, darkSkin);
            Block(15.5f, 21f, 4f, 7f, darkSkin);
            Block(44.5f, 21f, 4f, 7f, darkSkin);

            DrawHair(hairStyle, hair, 32f, 6f);
            DrawHat(hatStyle, top, 32f, 6f);

            var eyeWhite = Shade(skin, 0.72f);
            var face = new Color32(23, 23, 23, 255);
            DrawEyeOval(24f, 24f, 12f, 14f, eyeWhite, face);
            DrawEyeOval(40f, 24f, 12f, 14f, eyeWhite, face);
            Block(29f, 31f, 6f, 1f, face);

            _texture.SetPixels32(_pixels);
            _texture.Apply(false, false);
            _image.sprite = _sprite;
            _image.color = Color.white;
            _image.preserveAspect = true;
        }

        private void EnsureTexture()
        {
            if (_texture != null)
            {
                return;
            }

            _pixels = new Color32[_textureWidth * _textureHeight];
            _texture = new Texture2D(_textureWidth, _textureHeight, TextureFormat.RGBA32, false)
            {
                name = _inventoryStyle ? "WOF Inventory Wizard Preview" : "WOF Launch Wizard Preview",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            _sprite = Sprite.Create(_texture, new Rect(0f, 0f, _textureWidth, _textureHeight), new Vector2(0.5f, 0.5f), 100f);
            _sprite.name = _texture.name;
        }

        private void CanvasRect(float x, float y, float width, float height, Color32 color)
        {
            FillFinalRect(x * CanvasScale, y * CanvasScale, width * CanvasScale, height * CanvasScale, color);
        }

        private void CanvasOutline(float x, float y, float width, float height, float thickness, Color32 color)
        {
            CanvasRect(x, y, width, thickness, color);
            CanvasRect(x, y + height - thickness, width, thickness, color);
            CanvasRect(x, y, thickness, height, color);
            CanvasRect(x + width - thickness, y, thickness, height, color);
        }

        private void Block(float x, float y, float width, float height, Color32 color)
        {
            x = Mathf.Round(x * 8f) / 8f;
            y = Mathf.Round(y * 8f) / 8f;
            FillFinalRect(
                _avatarOffsetX * CanvasScale + x * AvatarScale,
                _avatarOffsetY * CanvasScale + y * AvatarScale,
                Mathf.Max(0.125f, width) * AvatarScale,
                Mathf.Max(0.125f, height) * AvatarScale,
                color);
        }

        private void FillFinalRect(float x, float y, float width, float height, Color32 color)
        {
            var left = Mathf.Clamp(Mathf.RoundToInt(x), 0, _textureWidth);
            var top = Mathf.Clamp(Mathf.RoundToInt(y), 0, _textureHeight);
            var right = Mathf.Clamp(Mathf.RoundToInt(x + width), left, _textureWidth);
            var bottom = Mathf.Clamp(Mathf.RoundToInt(y + height), top, _textureHeight);
            for (var py = top; py < bottom; py++)
            {
                var textureY = _textureHeight - 1 - py;
                var row = textureY * _textureWidth;
                for (var px = left; px < right; px++)
                {
                    var index = row + px;
                    _pixels[index] = Blend(_pixels[index], color);
                }
            }
        }

        private void DrawInventoryGlow()
        {
            for (var textureY = 0; textureY < _textureHeight; textureY++)
            {
                var canvasY = (_textureHeight - 1 - textureY) / CanvasScale;
                var row = textureY * _textureWidth;
                for (var textureX = 0; textureX < _textureWidth; textureX++)
                {
                    var canvasX = textureX / CanvasScale;
                    var distance = Vector2.Distance(new Vector2(canvasX, canvasY), new Vector2(112f, 118f));
                    if (distance >= 112f)
                    {
                        continue;
                    }

                    var t = Mathf.Clamp01((distance - 8f) / 104f);
                    Color32 glow;
                    if (t <= 0.45f)
                    {
                        var local = t / 0.45f;
                        glow = LerpColor32(
                            new Color32(250, 204, 21, 46),
                            new Color32(16, 185, 129, 36),
                            local);
                    }
                    else
                    {
                        var local = (t - 0.45f) / 0.55f;
                        glow = LerpColor32(
                            new Color32(16, 185, 129, 36),
                            new Color32(2, 9, 6, 0),
                            local);
                    }
                    _pixels[row + textureX] = Blend(_pixels[row + textureX], glow);
                }
            }
        }

        private void ReleaseTexture()
        {
            if (_sprite != null)
            {
                Destroy(_sprite);
                _sprite = null;
            }
            if (_texture != null)
            {
                Destroy(_texture);
                _texture = null;
            }
            _pixels = null;
        }

        private void DrawPixelEllipse(float x, float y, float width, float height, Color32 color)
        {
            var rows = Mathf.Max(2, Mathf.RoundToInt(height / 1.5f));
            for (var row = 0; row < rows; row++)
            {
                var t = rows == 1 ? 0.5f : row / (float)(rows - 1);
                var dy = (t - 0.5f) * 2f;
                var rowWidth = Mathf.Max(2f, width * Mathf.Sqrt(Mathf.Max(0f, 1f - dy * dy * 0.88f)));
                Block(x + (width - rowWidth) / 2f, y + row * height / rows, rowWidth, height / rows, color);
            }
        }

        private void DrawArm(float shoulderX, float shoulderY, float handX, float handY, Color32 skin, Color32 sleeve)
        {
            var dx = handX - shoulderX;
            var dy = handY - shoulderY;
            var steps = Mathf.Max(3, Mathf.CeilToInt(Mathf.Sqrt(dx * dx + dy * dy) / 4f));
            for (var index = 0; index <= steps; index++)
            {
                var t = index / (float)steps;
                Block(shoulderX + dx * t - 2f, shoulderY + dy * t - 2f, 4f, 4f, index < steps * 0.55f ? sleeve : skin);
            }
            DrawPixelEllipse(handX - 3f, handY - 2f, 6f, 5f, skin);
        }

        private void DrawLeg(float hipX, float hipY, float footX, float footY, Color32 pants, Color32 shoes)
        {
            var dx = footX - hipX;
            var dy = footY - hipY;
            var steps = Mathf.Max(3, Mathf.CeilToInt(Mathf.Sqrt(dx * dx + dy * dy) / 4f));
            for (var index = 0; index <= steps; index++)
            {
                var t = index / (float)steps;
                Block(hipX + dx * t - 2f, hipY + dy * t - 2f, 4f, 5f, index < steps * 0.78f ? pants : shoes);
            }
            Block(footX - 4f, footY + 1f, 8f, 3f, shoes);
        }

        private void DrawHair(string style, Color32 color, float headX, float headY)
        {
            switch ((style ?? string.Empty).ToLowerInvariant())
            {
                case "none":
                    return;
                case "short":
                    Block(headX - 9f, headY + 3f, 18f, 4f, color);
                    Block(headX - 10f, headY + 7f, 5f, 9f, Shade(color, -0.16f));
                    Block(headX + 5f, headY + 7f, 5f, 8f, Shade(color, -0.12f));
                    return;
                case "bob":
                    Block(headX - 10f, headY + 4f, 20f, 5f, color);
                    Block(headX - 12f, headY + 8f, 6f, 16f, Shade(color, -0.18f));
                    Block(headX + 6f, headY + 8f, 6f, 16f, Shade(color, -0.18f));
                    Block(headX - 7f, headY + 22f, 14f, 4f, Shade(color, -0.10f));
                    return;
                case "spikes":
                    Block(headX - 10f, headY + 7f, 20f, 5f, color);
                    Block(headX - 8f, headY + 1f, 4f, 8f, color);
                    Block(headX - 2f, headY - 1f, 4f, 9f, Shade(color, 0.06f));
                    Block(headX + 5f, headY + 1f, 4f, 8f, Shade(color, -0.08f));
                    return;
                default:
                    Block(headX - 10f, headY + 4f, 20f, 5f, color);
                    Block(headX - 12f, headY + 9f, 5f, 23f, Shade(color, -0.16f));
                    Block(headX + 7f, headY + 9f, 5f, 23f, Shade(color, -0.18f));
                    return;
            }
        }

        private void DrawHat(string style, Color32 color, float headX, float headY)
        {
            switch ((style ?? string.Empty).ToLowerInvariant())
            {
                case "none":
                    return;
                case "cap":
                    Block(headX - 11f, headY + 2f, 22f, 5f, color);
                    Block(headX - 6f, headY - 2f, 14f, 5f, Shade(color, 0.05f));
                    Block(headX + 8f, headY + 5f, 7f, 3f, Shade(color, -0.10f));
                    return;
                case "hood":
                    DrawPixelEllipse(headX - 13f, headY, 26f, 25f, Shade(color, -0.06f));
                    Block(headX - 8f, headY + 9f, 16f, 13f, new Color32(9, 9, 11, 255));
                    return;
                case "pharaoh":
                    DrawPharaohHat(color, headX, headY);
                    return;
                case "floppy-wizard":
                    DrawFloppyHat(color, headX, headY);
                    return;
                default:
                    Block(headX - 15f, headY + 7f, 30f, 4f, Shade(color, -0.18f));
                    Block(headX - 9f, headY + 2f, 19f, 5f, color);
                    Block(headX - 5f, headY - 5f, 12f, 7f, Shade(color, 0.04f));
                    Block(headX - 1f, headY - 13f, 8f, 8f, Shade(color, 0.12f));
                    Block(headX + 3f, headY - 18f, 5f, 6f, Shade(color, 0.18f));
                    return;
            }
        }

        private void DrawFloppyHat(Color32 color, float x, float y)
        {
            var shade = Shade(color, -0.16f);
            var deep = Shade(color, -0.34f);
            var light = Shade(color, 0.16f);
            var fold = Shade(color, -0.28f);
            var brimY = y + 5f;
            Block(x - 24f, brimY + 4f, 48f, 5f, deep);
            Block(x - 21f, brimY + 2f, 42f, 4f, shade);
            Block(x - 16f, brimY, 34f, 4f, color);
            Block(x - 25f, brimY + 1f, 11f, 3f, Shade(color, -0.10f));
            Block(x + 13f, brimY - 1f, 18f, 3f, light);
            Block(x + 27f, brimY - 1f, 9f, 2f, Shade(color, 0.08f));
            Block(x - 10f, y, 21f, 8f, color);
            Block(x - 7f, y - 4f, 15f, 8f, Shade(color, 0.06f));
            Block(x + 5f, y - 6f, 14f, 6f, shade);
            Block(x + 17f, y - 6f, 14f, 4f, Shade(color, -0.08f));
            Block(x + 29f, y - 5f, 11f, 3f, shade);
            Block(x + 38f, y - 4f, 7f, 2f, deep);
            Block(x - 5f, y + 2f, 3f, 7f, fold);
            Block(x + 11f, y - 3f, 3f, 4f, deep);
            Block(x + 25f, y - 4f, 2f, 3f, deep);
        }

        private void DrawPharaohHat(Color32 color, float x, float y)
        {
            var gold = new Color32(250, 204, 21, 255);
            var lightGold = new Color32(253, 230, 138, 255);
            Block(x - 16f, y + 1f, 32f, 5f, gold);
            Block(x - 11f, y - 4f, 22f, 6f, lightGold);
            Block(x - 15f, y + 7f, 8f, 23f, Shade(color, -0.22f));
            Block(x + 7f, y + 7f, 8f, 23f, Shade(color, -0.38f));
            Block(x - 5f, y + 7f, 5f, 22f, color);
            Block(x + 1f, y + 7f, 5f, 22f, Shade(color, -0.22f));
            Block(x - 13f, y + 13f, 6f, 3f, gold);
            Block(x + 7f, y + 13f, 6f, 3f, gold);
            Block(x - 1f, y - 8f, 6f, 5f, color);
            Block(x + 3f, y - 11f, 3f, 4f, gold);
            Block(x - 2f, y + 5f, 5f, 4f, gold);
        }

        private void DrawEyeOval(float x, float y, float width, float height, Color32 fill, Color32 outline)
        {
            DrawPixelEllipse(x - width / 2f, y - height / 2f, width, height, outline);
            DrawPixelEllipse(x - width / 2f + 1f, y - height / 2f + 1f, Mathf.Max(2f, width - 2f), Mathf.Max(2f, height - 2f), fill);
        }

        private static Color32 ParseHex(string value, Color32 fallback)
        {
            return ColorUtility.TryParseHtmlString(value, out var color) ? (Color32)color : fallback;
        }

        private static Color32 Shade(Color32 color, float amount)
        {
            var mix = amount >= 0f ? 255f : 0f;
            var strength = Mathf.Abs(amount);
            return new Color32(
                (byte)Mathf.RoundToInt(color.r + (mix - color.r) * strength),
                (byte)Mathf.RoundToInt(color.g + (mix - color.g) * strength),
                (byte)Mathf.RoundToInt(color.b + (mix - color.b) * strength),
                color.a);
        }

        private static Color32 LerpColor32(Color32 from, Color32 to, float t)
        {
            t = Mathf.Clamp01(t);
            return new Color32(
                (byte)Mathf.RoundToInt(Mathf.Lerp(from.r, to.r, t)),
                (byte)Mathf.RoundToInt(Mathf.Lerp(from.g, to.g, t)),
                (byte)Mathf.RoundToInt(Mathf.Lerp(from.b, to.b, t)),
                (byte)Mathf.RoundToInt(Mathf.Lerp(from.a, to.a, t)));
        }

        private static Color32 Blend(Color32 background, Color32 foreground)
        {
            if (foreground.a == 255)
            {
                return foreground;
            }
            var alpha = foreground.a / 255f;
            return new Color32(
                (byte)Mathf.RoundToInt(foreground.r * alpha + background.r * (1f - alpha)),
                (byte)Mathf.RoundToInt(foreground.g * alpha + background.g * (1f - alpha)),
                (byte)Mathf.RoundToInt(foreground.b * alpha + background.b * (1f - alpha)),
                255);
        }
    }
}
