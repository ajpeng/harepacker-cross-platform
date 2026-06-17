using SkiaSharp;

namespace HaCreator.MapSimulator.UI.Controls {

    public interface IHaUIRenderable {
        void AddRenderable(IHaUIRenderable renderable);
        SKBitmap Render();
        HaUISize GetSize();
        HaUIInfo GetInfo();
    }
}
