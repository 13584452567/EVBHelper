namespace OpenixCard.Services;

internal static class ServiceFactory
{
    public static OpenixCardService Create()
    {
        var fexToCfg = new FexToCfgConverter();
        var imageUnpacker = new ImageUnpacker();
        var genImageWorkflow = new GenImageWorkflow();
        return new OpenixCardService(fexToCfg, imageUnpacker, genImageWorkflow);
    }
}
