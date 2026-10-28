using UnityEngine;

namespace Incheol.Utils
{
    /// <summary>무작위 선택 모음.</summary>
    public static class RandomPicker
    {
        /// <summary>
        /// 0 ~ _total - 1 중 겹치지 않게 _count개를 무작위로 골라 뽑힌 순서대로 돌려준다(앞에서부터 _count개만 뽑는 부분 셔플).
        /// _count는 0 ~ _total로 맞춘다.
        /// </summary>
        public static int[] PickIndices(int _total, int _count)
        {
            _total = Mathf.Max(0, _total);
            _count = Mathf.Clamp(_count, 0, _total);

            int[] indices = new int[_total];
            for (int i = 0; i < indices.Length; i++)
            {
                indices[i] = i;
            }

            int[] picked = new int[_count];
            for (int i = 0; i < _count; i++)
            {
                int pick = Random.Range(i, indices.Length);
                (indices[i], indices[pick]) = (indices[pick], indices[i]);
                picked[i] = indices[i];
            }

            return picked;
        }
    }
}
