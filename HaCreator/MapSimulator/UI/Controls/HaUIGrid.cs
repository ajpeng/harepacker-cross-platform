using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using SkiaSharp;

namespace HaCreator.MapSimulator.UI.Controls {

    /// <summary>
    /// An image container grid that allows for easy creation of game UIs without
    /// the low level height, width, x, y, etc. calculations.
    /// </summary>
    public class HaUIGrid : IHaUIRenderable {

        private readonly int rows;
        private readonly int columns;

        private readonly HaUIInfo _info;

        private List<KeyValuePair<Point, IHaUIRenderable>> gridContent;

        public HaUIGrid(int rows, int columns) {
            this.rows = rows;
            this.columns = columns;
            this._info = new HaUIInfo();
            this.gridContent = new List<KeyValuePair<Point, IHaUIRenderable>>();
        }

        public HaUIGrid(int rows, int columns, HaUIInfo info) {
            this.rows = rows;
            this.columns = columns;
            this._info = info;
            this.gridContent = new List<KeyValuePair<Point, IHaUIRenderable>>();
        }

        public void AddRenderable(IHaUIRenderable renderable) {
            AddRenderable(0, 0, renderable);
        }

        public void AddRenderable(int row, int column, IHaUIRenderable renderable) {
            if (row < 0 || row >= rows)
                throw new ArgumentException("Invalid row index", nameof(row));

            if (column < 0 || column >= columns)
                throw new ArgumentException("Invalid column index", nameof(column));

            gridContent.Add(new KeyValuePair<Point, IHaUIRenderable>(new Point(column, row), renderable));
        }

        public SKBitmap Render() {
            HaUISize thisGridSize = GetSize();
            int cellWidth = thisGridSize.Width;
            int cellHeight = thisGridSize.Height;

            var gridBitmap = new SKBitmap(cellWidth * columns, cellHeight * rows);
            using (var canvas = new SKCanvas(gridBitmap)) {
                canvas.Clear(SKColors.Transparent);

                foreach (var pair in gridContent) {
                    using SKBitmap drawImage = pair.Value.Render();
                    HaUISize contentSize = pair.Value.GetSize();
                    HaUIInfo subUIInfo = pair.Value.GetInfo();
                    Point subUIKey = pair.Key;

                    int alignmentXOffset = HaUIHelper.CalculateAlignmentOffset(contentSize.Width, cellWidth, subUIInfo.HorizontalAlignment);
                    int alignmentYOffset = HaUIHelper.CalculateAlignmentOffset(contentSize.Height, cellHeight, subUIInfo.VerticalAlignment);

                    int x = subUIKey.X * cellWidth - alignmentXOffset;
                    int y = subUIKey.Y * cellHeight - alignmentYOffset;

                    canvas.DrawBitmap(drawImage, x, y);
                }
            }

            return gridBitmap;
        }

        public HaUISize GetSize() {
            int width = gridContent.Select(pair => pair.Value).ToList().Max(r => r.GetSize().Width + r.GetInfo().Margins.Left + r.GetInfo().Margins.Right) * columns;
            int height = gridContent.Select(pair => pair.Value).ToList().Max(r => r.GetSize().Height + r.GetInfo().Margins.Top + r.GetInfo().Margins.Bottom) * rows;

            return new HaUISize(
                Math.Max(_info.MinWidth, width),
                Math.Max(_info.MinHeight, height));
        }

        public HaUIInfo GetInfo() {
            return _info;
        }
    }
}
