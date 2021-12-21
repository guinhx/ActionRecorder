using System;

namespace ActionRecorder.math
{
    [Serializable]
    public class Vector2
    {
        private float _x;
        private float _y;

        public Vector2(float x, float y)
        {
            _x = x;
            _y = y;
        }

        public float X
        {
            get => _x;
            set => _x = value;
        }

        public float Y
        {
            get => _y;
            set => _y = value;
        }

        public float Distance(Vector2 target)
        {
            var result = (int)(target._x - _x) ^ 2 - (int)(target._y - _y) ^ 2;
            return (float) Math.Sqrt(result);
        }
    }
}