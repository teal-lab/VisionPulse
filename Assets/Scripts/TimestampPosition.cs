using UnityEngine;

public struct TimestampPosition
{
    public TimestampPosition(float time, Vector3 position)
    {
        Time = time;
        X = position.x;
        Y = position.y;
        Z = position.z;
    }

    public float Time { get; private set; }

    public float X { get; private set; }

    public float Y { get; private set; }

    public float Z { get; private set; }
}
