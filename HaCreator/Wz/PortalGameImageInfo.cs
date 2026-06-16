using MapleLib.WzLib.WzStructure.Data;
using System;
using System.Collections;
using System.Collections.Generic;
using SkiaSharp;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaCreator.Wz
{
    public class PortalGameImageInfo
    {
        private readonly SKBitmap? defaultImage;

        private readonly Dictionary<string, List<SKBitmap?>> imageList;

        public PortalGameImageInfo(SKBitmap? defaultImage, Dictionary<string, List<SKBitmap?>> imageList)
        {
            this.defaultImage = defaultImage;
            this.imageList = imageList;
        }

        public SKBitmap? DefaultImage
        {
            get { return defaultImage; }
        }

        public List<SKBitmap?> this[string name]
        {
            get
            {
                if (!imageList.ContainsKey(name))
                    return imageList["default"];
                return imageList[name];
            }
        }

        public IDictionaryEnumerator GetEnumerator()
        {
            return imageList.GetEnumerator();
        }
    }
}
