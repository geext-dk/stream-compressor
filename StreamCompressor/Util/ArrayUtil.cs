using System;

namespace StreamCompressor.Util
{
    public static class ArrayUtil
    {
        public static T[] Concatenate<T>(T[] lhs, int lhsSize, T[] rhs, int rhsSize)
        {
            var newArray = new T[lhsSize + rhsSize];
            Array.Copy(lhs, 0, newArray, 0, lhsSize);
            Array.Copy(rhs, 0, newArray, lhsSize, rhsSize);

            return newArray;
        }
        
        public static (T[] leftArray, T[] rightArray) SplitByIndex<T>(T[] array, int index)
        {
            if (index == 0)
                return (Array.Empty<T>(), array);

            if (index >= array.Length - 1)
                return (array, Array.Empty<T>());

            var rightArrayLength = array.Length - index;

            var leftArray = new T[index];
            var rightArray = new T[rightArrayLength];
            Array.Copy(array, 0, leftArray, 0, index);
            Array.Copy(array, index, rightArray, 0, rightArrayLength);

            return (leftArray, rightArray);
        }
    }
}