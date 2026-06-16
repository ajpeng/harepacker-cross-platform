using HaSharedLibrary.Util;
using MapleLib.WzLib;
using Microsoft.Xna.Framework.Graphics;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaCreator.MapEditor.Info
{
    public abstract class MapleDrawableInfo
    {
        private SKBitmap? image;
        private Texture2D? texture;
        private System.Drawing.Point origin;
        private WzObject parentObject;
        int width;
        int height;

        public MapleDrawableInfo(SKBitmap? image, Point origin, WzObject parentObject)
        {
            this.Image = image;
            this.origin = origin;
            this.parentObject = parentObject;
        }

        /// <summary>
        /// Create an instance of BoardItem from editor panels
        /// </summary>
        /// <param name="layer"></param>
        /// <param name="board"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="z"></param>
        /// <param name="flip"></param>
        /// <returns></returns>
        public abstract BoardItem CreateInstance(Layer layer, Board board, int x, int y, int z, bool flip);

        public virtual Texture2D? GetTexture(SpriteBatch sprite)
        {
            if (texture == null)
            {
                SKBitmap src = (image == null || (image.Width == 1 && image.Height == 1))
                    ? global::HaCreator.Properties.Resources.placeholder
                    : image;
                try { texture = src.ToTexture2D(sprite.GraphicsDevice); }
                catch { texture = global::HaCreator.Properties.Resources.placeholder.ToTexture2D(sprite.GraphicsDevice); }
            }
            return texture;
        }

        public virtual WzObject ParentObject
        {
            get
            {
                return parentObject;
            }
            set
            {
                parentObject = value;
            }
        }

        public virtual SKBitmap? Image
        {
            get => image;
            set
            {
                image = value;
                texture = null;
                if (image != null)
                {
                    width = image.Width;
                    height = image.Height;
                }
            }
        }

        public virtual int Width
        {
            get
            {
                return width;
            }
        }

        public virtual int Height
        {
            get
            {
                return height;
            }
        }

        public virtual Point Origin
        {
            get
            {
                return origin;
            }
            set
            {
                origin = value;
            }
        }
    }
}
