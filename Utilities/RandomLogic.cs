using System.Collections.Generic;
using UnityEngine;

namespace UnscriptedEngine
{
    public static class RandomLogic
    {
        public static float BetFloats(float start = 0f, float end = 100f)
        {
            return Random.Range(start, end);
        }

        public static float BetFloats(Vector2 range)
        {
            return Random.Range(range.x, range.y);
        }

        public static int BetInts(int start = 0, int end = 100)
        {
            return Random.Range(start, end);
        }

        public static int BetInts(Vector2Int range)
        {
            return Random.Range(range.x, range.y);
        }

        public static float FloatZeroTo(float value)
        {
            return Random.Range(0f, value);
        }

        public static int IntZeroTo(int value)
        {
            return Random.Range(0, value);
        }

        public static T FromArray<T>(T[] list)
        {
            return list[IntZeroTo(list.Length)];
        }

        public static T FromArray<T>(T[] list, out int index)
        {
            index = IntZeroTo(list.Length);
            return list[index];
        }

        public static T FromList<T>(List<T> list)
        {
            return list[IntZeroTo(list.Count)];
        }

        public static T FromList<T>(List<T> list, out int index)
        {
            index = IntZeroTo(list.Count);
            return list[index];
        }

        public static Vector2 InArea2D(float x, float y)
        {
            return InArea2D(new Vector2(x, y));
        }

        public static Vector2 InArea2D(Vector2 spawnArea)
        {
            float x = Random.Range((0f - spawnArea.x) / 2f, spawnArea.x / 2f);
            float y = Random.Range((0f - spawnArea.y) / 2f, spawnArea.y / 2f);
            return new Vector2(x, y);
        }

        public static Vector3 InArea3D(float x, float y, float z)
        {
            return InArea3D(new Vector3(x, y, z));
        }

        public static Vector3 InArea3D(Vector3 spawnArea)
        {
            float x = Random.Range((0f - spawnArea.x) / 2f, spawnArea.x / 2f);
            float y = Random.Range((0f - spawnArea.y) / 2f, spawnArea.y / 2f);
            float z = Random.Range((0f - spawnArea.z) / 2f, spawnArea.z / 2f);
            return new Vector3(x, y, z);
        }

        public static Vector2Int InGrid(Vector2Int grid)
        {
            return InGrid(grid.x, grid.y);
        }

        public static Vector2Int InGrid(int x, int y)
        {
            int x2 = IntZeroTo(x);
            int y2 = IntZeroTo(y);
            return new Vector2Int(x2, y2);
        }

        public static Vector3Int InGrid3D(int x, int y, int z)
        {
            int x2 = IntZeroTo(x);
            int y2 = IntZeroTo(y);
            int z2 = IntZeroTo(z);
            return new Vector3Int(x2, y2, z2);
        }

        public static Vector3 VectorDirAroundY()
        {
            return IntZeroTo(4) switch
            {
                0 => Vector3.forward,
                1 => Vector3.back,
                3 => Vector3.left,
                _ => Vector3.right,
            };
        }

        public static Vector3 PointAtCircumferenceXZ(Vector3 center, float radius)
        {
            float f = FloatZeroTo(360f);
            float z = radius * Mathf.Sin(f);
            float x = radius * Mathf.Cos(f);
            return center + new Vector3(x, 0f, z);
        }

        public static Vector3 VectorDirectionAny()
        {
            return IntZeroTo(6) switch
            {
                0 => Vector3.forward,
                1 => Vector3.back,
                3 => Vector3.left,
                4 => Vector3.right,
                5 => Vector3.up,
                _ => Vector3.down,
            };
        }

        public static int RandomIndex<T>(T[] list, float[] chances)
        {
            float[] array = new float[list.Length];
            float num = 0f;
            for (int i = 0; i < list.Length; i++)
            {
                array[i] = num + chances[i];
                num = array[i];
            }

            int num2 = Random.Range(0, 100);
            for (int j = 0; j < array.Length; j++)
            {
                float num3 = ((j == array.Length - 1) ? 100f : array[j]);
                float num4 = ((j == 0) ? 0f : array[j - 1]);
                if ((float)num2 > num4 && (float)num2 < num3)
                {
                    return j;
                }
            }

            return 0;
        }

        public static void Randomize(this int value, int incluseiveMin = 0, int exclusiveMax = 1)
        {
            value = Random.Range(incluseiveMin, exclusiveMax);
        }

        public static void Randomize(this int value, Vector2Int range)
        {
            value = Random.Range(range.x, range.y);
        }

        public static void Randomize(this float value, float inclusiveMin = 0f, float exclusiveMax = 1f)
        {
            value = Random.Range(inclusiveMin, exclusiveMax);
        }

        public static void Randomize(this float value, Vector2 range)
        {
            value = Random.Range(range.x, range.y);
        }

        public static void Randomize(this Vector2 value, Vector2 minInclusive, Vector2 maxExclusive)
        {
            value.x.Randomize(minInclusive.x, maxExclusive.x);
            value.y.Randomize(minInclusive.y, maxExclusive.y);
        }

        public static void Randomize(this Vector2Int value, Vector2Int minInclusive, Vector2Int maxExclusive)
        {
            value.x.Randomize(minInclusive.x, maxExclusive.x);
            value.y.Randomize(minInclusive.y, maxExclusive.y);
        }

        public static void Randomize(this Vector3 value, Vector3 minInclusive, Vector3 maxExclusive)
        {
            value.x.Randomize(minInclusive.x, maxExclusive.x);
            value.y.Randomize(minInclusive.y, maxExclusive.y);
            value.z.Randomize(minInclusive.z, maxExclusive.z);
        }

        public static void Randomize(this Vector3Int value, Vector3Int minInclusive, Vector3Int maxExclusive)
        {
            value.x.Randomize(minInclusive.x, maxExclusive.x);
            value.y.Randomize(minInclusive.y, maxExclusive.y);
            value.z.Randomize(minInclusive.z, maxExclusive.z);
        }

        public static T GetRandomElement<T>(this T[] value)
        {
            return FromArray(value);
        }

        public static T GetRandomElement<T>(this List<T> value)
        {
            return FromList(value);
        }
    }
}