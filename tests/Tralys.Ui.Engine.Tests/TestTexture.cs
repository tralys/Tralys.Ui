using Tralys.Ui.Rendering;

namespace Tralys.Ui.Engine.Tests;

sealed class TestTexture(int width, int height) : RenderTexture
{
    public override int Width => width;

    public override int Height => height;

    public void Repack() => BumpGeneration();
}

sealed class TestTextureArray(int width, int height, int layers) : RenderTextureArray
{
    public override int Width => width;

    public override int Height => height;

    public override int LayerCount => layers;
}
