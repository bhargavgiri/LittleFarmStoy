namespace LittleFarmStory.Input
{
    /// <summary>
    /// Anything that can raise a one-shot "do the thing" request (on-screen button, key, gamepad button).
    /// </summary>
    public interface IActionInputSource
    {
        /// <summary>Returns true once per press. Implementations clear their own latch when read.</summary>
        bool ConsumeInteractPressed();
    }
}
