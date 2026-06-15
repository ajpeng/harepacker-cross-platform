using HaRepacker.Models;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using HaSharedLibrary.Util;
using MapleLib.WzLib.WzProperties;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using SkiaSharp;
using Spine;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace HaRepacker.GUI.Panels
{
    public class ImageAnimationPreviewWindow : Microsoft.Xna.Framework.Game
    {
        private GraphicsDeviceManager graphicsDeviceMgr;

        private int RENDER_WIDTH = 1366;
        private int RENDER_HEIGHT = 768;

        private float renderAnimationScaling = 1.0f;
        private float renderTextScaling = 1.0f;
        private float UserScreenScaleFactor = 1.0f;

        private RenderParameters _renderParams;

        private readonly List<WzNode> selectedAnimationNodes;
        private BaseDXDrawableItem dxDrawableItem = null;

        private SpriteFont font_DebugValues;
        private Texture2D texture_debugBoundaryRect;

        private SpriteBatch spriteBatch;
        private SpriteFont font;

        public int mapShiftX = -600;
        public int mapShiftY = -400;

        public ImageAnimationPreviewWindow(List<WzNode> selectedAnimationNodes, string title_path)
        {
            this.selectedAnimationNodes = selectedAnimationNodes;

            IsMouseVisible = true;
            Window.Title = title_path;
            IsFixedTimeStep = false;
            Content.RootDirectory = "Content";

            // DPI scaling is Windows-only; default to 1.0 on cross-platform
            this.UserScreenScaleFactor = 1.0f;
            this.renderAnimationScaling *= this.UserScreenScaleFactor;
            this.renderTextScaling *= this.UserScreenScaleFactor;

            this._renderParams = new RenderParameters(RENDER_WIDTH, RENDER_HEIGHT, renderAnimationScaling, RenderResolution.Res_All);

            graphicsDeviceMgr = new GraphicsDeviceManager(this)
            {
                SynchronizeWithVerticalRetrace = true,
                HardwareModeSwitch = true,
                GraphicsProfile = GraphicsProfile.HiDef,
                IsFullScreen = false,
                PreferMultiSampling = true,
                SupportedOrientations = DisplayOrientation.Default,
                PreferredBackBufferWidth = (int)(RENDER_WIDTH * UserScreenScaleFactor),
                PreferredBackBufferHeight = (int)(RENDER_HEIGHT * UserScreenScaleFactor),
                PreferredBackBufferFormat = SurfaceFormat.Color,
                PreferredDepthStencilFormat = DepthFormat.Depth24Stencil8,
            };
            graphicsDeviceMgr.ApplyChanges();
        }

        protected override void Initialize()
        {
            font = Content.Load<SpriteFont>("XnaDefaultFont");
            font_DebugValues = Content.Load<SpriteFont>("XnaFont_Debug");
            base.Initialize();
        }

        protected override void LoadContent()
        {
            spriteBatch = new SpriteBatch(GraphicsDevice);

            var animationFrames = new List<IDXObject>();
            foreach (WzNode selNode in selectedAnimationNodes)
            {
                var obj = selNode.WzObject;
                if (obj == null) continue;
                bool isUOLProperty = obj is WzUOLProperty;

                if (obj is WzCanvasProperty || isUOLProperty)
                {
                    WzCanvasProperty canvasProperty;
                    SKBitmap image;

                    if (!isUOLProperty)
                    {
                        canvasProperty = (WzCanvasProperty)obj;
                        image = canvasProperty.GetLinkedWzCanvasBitmap();
                    }
                    else
                    {
                        var linkVal = ((WzUOLProperty)obj).LinkValue;
                        if (linkVal is WzCanvasProperty property)
                        {
                            canvasProperty = property;
                            image = canvasProperty.GetLinkedWzCanvasBitmap();
                        }
                        else break;
                    }

                    if (image == null) continue;

                    int delay = canvasProperty[WzCanvasProperty.AnimationDelayPropertyName]?.GetInt() ?? 0;
                    System.Drawing.PointF origin = canvasProperty.GetCanvasOriginPosition();

                    var dxObject = new DXObject(
                        (int)-origin.X,
                        (int)-origin.Y,
                        image.ToTexture2D(graphicsDeviceMgr.GraphicsDevice),
                        delay)
                    {
                        Tag = obj.FullPath
                    };
                    animationFrames.Add(dxObject);
                }
            }

            dxDrawableItem = new BaseDXDrawableItem(animationFrames, false);

            // 1×1 white texture for border drawing
            var bitmap_debug = new SKBitmap(1, 1);
            bitmap_debug.SetPixel(0, 0, new SKColor(255, 255, 255, 255));
            texture_debugBoundaryRect = bitmap_debug.ToTexture2D(graphicsDeviceMgr.GraphicsDevice);
            bitmap_debug.Dispose();
        }

        protected override void UnloadContent()
        {
            graphicsDeviceMgr.EndDraw();
            graphicsDeviceMgr.Dispose();
            graphicsDeviceMgr = null;
            dxDrawableItem = null;
        }

        private KeyboardState oldKeyboardState = Keyboard.GetState();

        protected override void Update(GameTime gameTime)
        {
            float frameRate = 1 / (float)gameTime.ElapsedGameTime.TotalSeconds;

            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed
                || Keyboard.GetState().IsKeyDown(Keys.Escape))
                this.Exit();

            bool bIsAltEnterPressed = Keyboard.GetState().IsKeyDown(Keys.LeftAlt) && Keyboard.GetState().IsKeyDown(Keys.Enter);
            if (bIsAltEnterPressed)
            {
                graphicsDeviceMgr.IsFullScreen = !graphicsDeviceMgr.IsFullScreen;
                graphicsDeviceMgr.ApplyChanges();
            }

            bool bIsPlusKeyPressed = Keyboard.GetState().IsKeyDown(Keys.OemPlus);
            bool bIsMinusKeyPressed = Keyboard.GetState().IsKeyDown(Keys.OemMinus);
            float zoomOffset = 1.5f / frameRate;
            if (bIsPlusKeyPressed) renderAnimationScaling += zoomOffset;
            if (bIsMinusKeyPressed) renderAnimationScaling -= zoomOffset;

            int moveOffset = (int)(500f / frameRate);
            if (Keyboard.GetState().IsKeyDown(Keys.Left))  mapShiftX += (int)(moveOffset / renderAnimationScaling);
            if (Keyboard.GetState().IsKeyDown(Keys.Right)) mapShiftX -= (int)(moveOffset / renderAnimationScaling);
            if (Keyboard.GetState().IsKeyDown(Keys.Up))    mapShiftY += (int)(moveOffset / renderAnimationScaling);
            if (Keyboard.GetState().IsKeyDown(Keys.Down))  mapShiftY -= (int)(moveOffset / renderAnimationScaling);

            oldKeyboardState = Keyboard.GetState();
            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            float frameRate = 1 / (float)gameTime.ElapsedGameTime.TotalSeconds;
            int TickCount = Environment.TickCount;

            MouseState mouseState = Mouse.GetState();
            int mouseXRelativeToMap = mouseState.X - mapShiftX;
            int mouseYRelativeToMap = mouseState.Y - mapShiftY;

            GraphicsDevice.Clear(Color.Black);

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, null, null, null, null,
                Matrix.CreateScale(renderAnimationScaling));

            dxDrawableItem.Draw(spriteBatch, null, gameTime, mapShiftX, mapShiftY, 0, 0, null, _renderParams, TickCount);

            if (dxDrawableItem.LastFrameDrawn != null)
            {
                IDXObject last = dxDrawableItem.LastFrameDrawn;
                DrawBorder(spriteBatch,
                    new Rectangle(last.X - mapShiftX, last.Y - mapShiftY, last.Width, last.Height),
                    1, Color.White);
            }

            spriteBatch.End();

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, null, null, null, null,
                Matrix.CreateScale(renderTextScaling));

            var sb = new StringBuilder();
            sb.Append("FPS: ").Append(frameRate).Append(Environment.NewLine);
            sb.Append("Mouse : X ").Append(mouseXRelativeToMap).Append(", Y ").Append(mouseYRelativeToMap).Append(Environment.NewLine);
            sb.Append("RMouse: X ").Append(mouseState.X).Append(", Y ").Append(mouseState.Y);
            spriteBatch.DrawString(font_DebugValues, sb.ToString(), new Vector2(RENDER_WIDTH - 170, 10), Color.White);

            if (dxDrawableItem.LastFrameDrawn != null)
            {
                IDXObject last = dxDrawableItem.LastFrameDrawn;
                string info = string.Format(
                    "[Path: {0}]{7}[Origin: x = {1}, y = {2}]{8}[Dimension: W = {3}, H = {4}]{9}[Delay: {5}]{10}[Scale: {6}x]",
                    last.Tag as string,
                    last.X, last.Y, last.Width, last.Height, last.Delay,
                    Math.Round(renderAnimationScaling, 2),
                    Environment.NewLine, Environment.NewLine, Environment.NewLine, Environment.NewLine);
                spriteBatch.DrawString(font_DebugValues, info, new Vector2(RENDER_WIDTH / 2 - 100, RENDER_HEIGHT - 100), Color.White);
            }

            if (gameTime.TotalGameTime.TotalSeconds < 3)
                spriteBatch.DrawString(font,
                    string.Format("Press [Left] [Right] [Up] [Down] for navigation.{0}   [+ -] for zoom", Environment.NewLine),
                    new Vector2(20, 10), Color.White);

            spriteBatch.End();

            base.Draw(gameTime);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DrawBorder(SpriteBatch sprite, Rectangle rect, int thickness, Color color)
        {
            sprite.Draw(texture_debugBoundaryRect, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
            sprite.Draw(texture_debugBoundaryRect, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
            sprite.Draw(texture_debugBoundaryRect, new Rectangle(rect.X + rect.Width - thickness, rect.Y, thickness, rect.Height), color);
            sprite.Draw(texture_debugBoundaryRect, new Rectangle(rect.X, rect.Y + rect.Height - thickness, rect.Width, thickness), color);
        }
    }
}
