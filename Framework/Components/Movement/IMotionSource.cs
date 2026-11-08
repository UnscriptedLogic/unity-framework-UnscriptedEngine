using System;
namespace UnscriptedEngine
{
    public interface IMotionSource
    {
        /// <summary>Normalized speed, signed turn rate in degrees/second, grounded.</summary>
        event Action<float, float, bool> MotionUpdated;
    }
}
