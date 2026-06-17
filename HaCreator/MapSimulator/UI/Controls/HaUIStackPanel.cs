using System;
using System.Collections.Generic;
using System.Linq;
using SkiaSharp;

namespace HaCreator.MapSimulator.UI.Controls {

    /// <summary>
    /// A stackpanel for bitmap images that allows for easy creation of game UIs without
    /// the low level height, width, x, y, etc. calculations.
    /// </summary>
    public class HaUIStackPanel : IHaUIRenderable {
        private List<IHaUIRenderable> childrens;
        private HaUIStackOrientation _orientation;

        private HaUIInfo _info;

        public HaUIStackPanel(HaUIStackOrientation _orientation) {
            this._orientation = _orientation;
            this.childrens = new List<IHaUIRenderable>();
            this._info = new HaUIInfo() { };
        }

        public HaUIStackPanel(HaUIStackOrientation _orientation, HaUIInfo info) {
            this._orientation = _orientation;
            this.childrens = new List<IHaUIRenderable>();
            this._info = info;
        }

        public void AddRenderable(IHaUIRenderable children) {
            this.childrens.Add(children);
        }

        public void SetOrientation(HaUIStackOrientation _orientation) {
            this._orientation = _orientation;
        }

        public SKBitmap Render() {
            HaUISize size = GetSize();
            int totalWidth = size.Width;
            int totalHeight = size.Height;

            var bitmap = new SKBitmap(totalWidth, totalHeight);
            using (var canvas = new SKCanvas(bitmap)) {
                canvas.Clear(SKColors.Transparent);

                int offset = _orientation == HaUIStackOrientation.Horizontal ? _info.Margins.Left : _info.Margins.Top;
                foreach (IHaUIRenderable child in childrens) {
                    using SKBitmap childBitmap = child.Render();
                    HaUIInfo subUIInfo = child.GetInfo();

                    if (_orientation == HaUIStackOrientation.Horizontal) {
                        int alignmentYOffset = HaUIHelper.CalculateAlignmentOffset(totalHeight, childBitmap.Height, subUIInfo.VerticalAlignment);

                        int x = offset + subUIInfo.Margins.Left;
                        int y = alignmentYOffset + subUIInfo.Margins.Top;

                        canvas.DrawBitmap(childBitmap, x, y);

                        offset += childBitmap.Width + subUIInfo.Margins.Left + subUIInfo.Margins.Right;
                    }
                    else {
                        int alignmentXOffset = HaUIHelper.CalculateAlignmentOffset(totalWidth, childBitmap.Width, subUIInfo.HorizontalAlignment);

                        int x = alignmentXOffset + subUIInfo.Margins.Left;
                        int y = offset + subUIInfo.Margins.Top;

                        canvas.DrawBitmap(childBitmap, x, y);

                        offset += childBitmap.Height + subUIInfo.Margins.Top + subUIInfo.Margins.Bottom;
                    }
                }
            }

            return bitmap;
        }

        public HaUISize GetSize() {
            if (_orientation == HaUIStackOrientation.Horizontal) {
                HaUISize allChildSizes = new HaUISize(
                    childrens.Sum(r => r.GetSize().Width + r.GetInfo().Margins.Left + r.GetInfo().Margins.Right),
                    childrens.Max(r => r.GetSize().Height + r.GetInfo().Margins.Top + r.GetInfo().Margins.Bottom));

                return new HaUISize(
                    Math.Max(_info.MinWidth, allChildSizes.Width),
                    Math.Max(_info.MinHeight, allChildSizes.Height));
            }
            else {
                HaUISize allChildSizes = new HaUISize(
                    childrens.Max(r => r.GetSize().Width + r.GetInfo().Margins.Left + r.GetInfo().Margins.Right),
                    childrens.Sum(r => r.GetSize().Height + r.GetInfo().Margins.Top + r.GetInfo().Margins.Bottom));

                return new HaUISize(
                    Math.Max(_info.MinWidth, allChildSizes.Width),
                    Math.Max(_info.MinHeight, allChildSizes.Height));
            }
        }

        public HaUIInfo GetInfo() {
            return _info;
        }
    }
}
