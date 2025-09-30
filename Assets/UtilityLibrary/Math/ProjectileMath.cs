using UnityEngine;

namespace UnityUtils
{
    public static class ProjectileMath
    {
        /// <summary>
        /// 任意の重力方向・大きさに対応した投射初速度を計算する関数
        /// </summary>
        /// <param name="gravity">重力加速度の大きさ</param>
        /// <param name="startPoint">開始位置</param>
        /// <param name="endPoint">目標位置</param>
        /// <param name="trajectoryHeight">最高到達点の高さ</param>
        /// <param name="initialSpeed">初速度の大きさ（平面方向）</param>
        /// <returns>初速度ベクトル</returns>
        public static Vector3 CalculateInitialVelocity(
            Vector3 gravity,
            Vector3 startPoint,
            Vector3 endPoint,
            float trajectoryHeight,
            float initialSpeed)
        {
            Vector3 gravityDir = gravity.normalized;
            float gravityMag = gravity.magnitude;

            float displacementGravity = Vector3.Dot(endPoint - startPoint, gravityDir);
            Vector3 displacementPlane = (endPoint - startPoint) - gravityDir * displacementGravity;

            Vector3 velocityGravity = -gravityDir * Mathf.Sqrt(2 * gravityMag * trajectoryHeight);

            float timeUp = Mathf.Sqrt(2 * trajectoryHeight / gravityMag);
            float timeDown = Mathf.Sqrt(2 * Mathf.Max(displacementGravity - trajectoryHeight, 0.01f) / gravityMag);
            float totalTime = timeUp + timeDown;

            // 平面方向の初速度（magnitudeがinitialSpeedになるように調整）
            Vector3 velocityPlane = displacementPlane.normalized * initialSpeed;

            // 初速度ベクトル（平面方向＋重力方向）
            return velocityPlane + velocityGravity;
        }

        /// <summary>
        /// 空気抵抗を考慮した投射シミュレーションを行い、弾が目標地点に到達できるか判定します。
        /// </summary>
        /// <param name="gravity">重力ベクトル（方向と大きさ）</param>
        /// <param name="startPoint">弾の開始位置</param>
        /// <param name="endPoint">目標位置</param>
        /// <param name="initialVelocity">初速度ベクトル</param>
        /// <param name="airFriction">空気抵抗係数（速度に比例して減速）</param>
        /// <param name="tolerance">命中判定の許容誤差（距離）</param>
        /// <returns>目標に到達できればtrue、できなければfalse</returns>
        public static bool Simulate(Vector3 gravity, Vector3 startPoint, Vector3 endPoint, Vector3 initialVelocity, float airFriction, float tolerance = 0.1f)
        {
            Vector3 gravityDir = gravity.normalized;
            Vector3 position = startPoint;
            Vector3 velocity = initialVelocity;
            float simulationStep = 0.01f;
            float endProj = Vector3.Dot(endPoint, gravityDir);

            float maxSimulationTime = 10f;
            int maxSteps = (int)(maxSimulationTime / simulationStep);

            for (int steps = 0; steps < maxSteps; steps++)
            {
                // 目標より下で、下降中ならシミュレーション継続
                if (Vector3.Dot(position, gravityDir) < endProj && Vector3.Dot(velocity, gravityDir) <= 0)
                {
                    // 継続
                }
                else
                {
                    // 目標を超えた、または上昇に転じたら終了
                    break;
                }

                // 位置を更新
                position += velocity * simulationStep;
                // 重力を加算
                velocity += gravity * simulationStep;
                // 空気抵抗を減算（速度に比例）
                velocity -= velocity * airFriction * simulationStep;

                // 重力方向を除いた平面上の位置
                Vector3 posPlane = position - gravityDir * Vector3.Dot(position, gravityDir);
                Vector3 endPlane = endPoint - gravityDir * Vector3.Dot(endPoint, gravityDir);

                // 平面距離と重力方向距離が許容範囲内なら命中と判定
                if (Vector3.Distance(posPlane, endPlane) <= tolerance
                    && Mathf.Abs(Vector3.Dot(position - endPoint, gravityDir)) <= tolerance)
                {
                    return true; // 命中
                }
            }

            // 目標に到達できなかった場合
            return false;
        }

        /// <summary>
        /// 空気抵抗を考慮した初速度を二分探索で求める
        /// </summary>
        /// <param name="gravity">重力ベクトル</param>
        /// <param name="startPoint">開始位置</param>
        /// <param name="endPoint">目標位置</param>
        /// <param name="trajectoryHeight">最高到達点</param>
        /// <param name="airFriction">空気抵抗係数</param>
        /// <param name="simulateJumpMaxVelocity">探索する最大初速</param>
        /// <param name="tolerance">許容誤差</param>
        /// <returns>初速度ベクトル</returns>
        public static Vector3 CalculateVelocityWithAirFriction(
            Vector3 gravity,
            Vector3 startPoint,
            Vector3 endPoint,
            float trajectoryHeight,
            float airFriction,
            float simulateJumpMaxVelocity = 20f,
            float tolerance = 0.1f)
        {
            float minVelocity = 0f;
            float maxVelocity = simulateJumpMaxVelocity;

            // 二分探索で最小の初速を求める
            while (maxVelocity - minVelocity > tolerance)
            {
                float midVelocity = (minVelocity + maxVelocity) * 0.5f;
                // 仮の初速で命中するかシミュレーション
                Vector3 initialVelocity = CalculateInitialVelocity(gravity, startPoint, endPoint, trajectoryHeight, midVelocity);
                if (Simulate(gravity, startPoint, endPoint, initialVelocity, airFriction: airFriction, tolerance: tolerance))
                {
                    // 命中したら上限を下げる
                    maxVelocity = midVelocity;
                }
                else
                {
                    // 届かないなら下限を上げる
                    minVelocity = midVelocity;
                }
            }

            // 最終的な初速を返す
            return CalculateInitialVelocity(gravity, startPoint, endPoint, trajectoryHeight, (minVelocity + maxVelocity) * 0.5f);
        }
    }
}
