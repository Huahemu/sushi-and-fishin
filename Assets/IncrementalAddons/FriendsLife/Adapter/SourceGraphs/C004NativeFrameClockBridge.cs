using System.Linq;
namespace SushiAndFish.FriendsLife.SourceGraphs
{
    public sealed partial class OriginalGraphBridge
    {
        C004NativeFrameProducer nativeFrameProducer;
        void InitializeC004NativeFrameProducer(){nativeFrameProducer=gameObject.AddComponent<C004NativeFrameProducer>();nativeFrameProducer.Initialize(()=>runtime);}
        void BindC004NativeFrameConsumer(OriginalGraphRunner runner,SourceEpisode episode)
        {runner.NativeFrameInput=()=>nativeFrameProducer.Read(runner,episode);}
        void CaptureC004NativeFrameClock()=>nativeFrameProducer?.Capture(running.Values.Select(j=>j.runner));
    }
}
