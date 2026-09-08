namespace LittleFarmStory.Input
{
    /// <summary>
    /// Anything that can produce a 2D movement vector (joystick, keyboard, gamepad, AI, replay...).
    /// Implementations must not know about the player or the camera.
    /// </summary>
    public interface IMoveInputSource
    {
        /// <summary>Normalised-ish movement vector. X = right, Y = forward. Magnitude 0..1.</summary>
        UnityEngine.Vector2 MoveInput { get; }

        /// <summary>True while this source is actively being used, so higher priority sources can win.</summary>
        bool HasMoveInput { get; }
    }
}
