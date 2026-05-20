using System;

namespace FlipKit.Core.Tests
{
    internal static class NSubstituteExtensions
    {
        /// <summary>
        /// Tiny helper for one-line setup-and-return when configuring substitutes.
        /// Allows: <c>Substitute.For&lt;IFoo&gt;().Tap(f => f.Bar().Returns(42))</c>.
        /// </summary>
        public static T Tap<T>(this T target, Action<T> configure)
        {
            configure(target);
            return target;
        }
    }
}
