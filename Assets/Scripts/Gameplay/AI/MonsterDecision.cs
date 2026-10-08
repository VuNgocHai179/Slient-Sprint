using UnityEngine;

namespace SilentSprint.Gameplay.AI
{
    /// <summary>
    /// Dau ra cua "bo nao": 3 xac suat trang thai + 4 tham so tinh cach.
    /// Day la lop QUYET DINH; lop phan xa (reflex) nam trong <c>MonsterAI</c> va co quyen phu quyet.
    /// </summary>
    public struct MonsterDecision
    {
        /// <summary>Xac suat nen di tuan.</summary>
        public float PatrolProbability;
        /// <summary>Xac suat nen di dieu tra.</summary>
        public float InvestigateProbability;
        /// <summary>Xac suat nen duoi.</summary>
        public float ChaseProbability;

        /// <summary>0..1 - do hung hang, quyet dinh toc do (thay cho viec luon chay het toc khi duoi).</summary>
        public float Aggression;
        /// <summary>0..1 - khi mat dau thi luc soat rong bao nhieu quanh vi tri cuoi.</summary>
        public float SearchRadius;
        /// <summary>0..1 - xu huong cat duong theo van toc player thay vi bam duoi truc tiep.</summary>
        public float Flank;
        /// <summary>0..1 - do canh giac, keo dai thoi gian luc soat.</summary>
        public float Alertness;

        /// <summary>Trang thai mac dinh khi chua co du doan nao - di tuan cho an toan.</summary>
        public static MonsterDecision Neutral => new MonsterDecision
        {
            PatrolProbability = 1f,
            InvestigateProbability = 0f,
            ChaseProbability = 0f,
            Aggression = 0.2f,
            SearchRadius = 0.25f,
            Flank = 0f,
            Alertness = 0.25f,
        };

        /// <summary>Chi so trang thai co xac suat cao nhat (0 = patrol, 1 = investigate, 2 = chase).</summary>
        public int PreferredStateIndex
        {
            get
            {
                if (InvestigateProbability > PatrolProbability &&
                    InvestigateProbability >= ChaseProbability)
                    return MonsterBrainOutputLayout.StateInvestigate;

                if (ChaseProbability > PatrolProbability &&
                    ChaseProbability > InvestigateProbability)
                    return MonsterBrainOutputLayout.StateChase;

                return MonsterBrainOutputLayout.StatePatrol;
            }
        }

        public void SetStateProbabilities(float patrol, float investigate, float chase)
        {
            PatrolProbability = patrol;
            InvestigateProbability = investigate;
            ChaseProbability = chase;
        }

        public void SetIntents(float aggression, float searchRadius, float flank, float alertness)
        {
            Aggression = Mathf.Clamp01(aggression);
            SearchRadius = Mathf.Clamp01(searchRadius);
            Flank = Mathf.Clamp01(flank);
            Alertness = Mathf.Clamp01(alertness);
        }

        public override string ToString()
        {
            return string.Format(
                "patrol={0:F2} investigate={1:F2} chase={2:F2} | aggression={3:F2} searchRadius={4:F2} flank={5:F2} alertness={6:F2}",
                PatrolProbability, InvestigateProbability, ChaseProbability,
                Aggression, SearchRadius, Flank, Alertness);
        }
    }

    /// <summary>
    /// Chinh sach "giao vien" viet bang tay, dung o hai cho:
    ///   1. Sinh nhan cho behaviour cloning trong <c>Tools/MonsterBrain/generate_monster_brain.py</c>
    ///      (ban Python cua lop nay - SUA MOT BEN THI PHAI SUA BEN KIA).
    ///   2. Fallback khi khong tim thay model ONNX, de quai van hoat dong binh thuong.
    /// </summary>
    public static class MonsterHeuristicPolicy
    {
        /// <summary>Nguong do on de coi la "nghe thay" (khop voi NOISE_THRESHOLD trong script Python).</summary>
        public const float NoiseThreshold = 0.4f;

        /// <summary>Nhiet do softmax khi doi utility thanh xac suat.</summary>
        private const float Temperature = 0.6f;

        public static MonsterDecision Evaluate(in MonsterObservation obs)
        {
            float seen = obs.PlayerVisible ? 1f : 0f;
            float heard = (obs.PlayerNoise > NoiseThreshold && obs.Proximity <= 1f) ? 1f : 0f;

            float uPatrol = 1f - 3.0f * seen - 1.8f * heard;
            float uInvestigate = 2.2f * heard
                                 + 2.6f * obs.HasLastKnown * (1f - obs.TimeSinceSeen)
                                 - 1.2f * seen;
            float uChase = 5.0f * seen + 2.0f * seen * (1f - obs.DistNorm) - 2.5f * (1f - seen);

            var decision = new MonsterDecision();
            Softmax(uPatrol, uInvestigate, uChase, out decision.PatrolProbability,
                    out decision.InvestigateProbability, out decision.ChaseProbability);

            float aggression = 0.20f + 0.80f * seen * (1f - 0.55f * obs.DistNorm) + 0.30f * heard;
            float lossAge = obs.TimeSinceSeen * (1f - seen);
            float searchRadius = obs.HasLastKnown > 0.5f ? 0.25f + 0.75f * lossAge : 0.20f;
            float flank = 0.15f + 0.85f * seen * obs.PlayerSpeedNorm;
            float alertness = 0.25f
                              + 0.45f * heard
                              + 0.40f * (1f - obs.TimeSinceSeen) * obs.HasLastKnown
                              + 0.35f * seen;

            decision.SetIntents(aggression, searchRadius, flank, alertness);
            return decision;
        }

        private static void Softmax(float u0, float u1, float u2,
                                    out float p0, out float p1, out float p2)
        {
            float l0 = u0 / Temperature;
            float l1 = u1 / Temperature;
            float l2 = u2 / Temperature;

            float max = Mathf.Max(l0, Mathf.Max(l1, l2));
            float e0 = Mathf.Exp(l0 - max);
            float e1 = Mathf.Exp(l1 - max);
            float e2 = Mathf.Exp(l2 - max);
            float sum = e0 + e1 + e2;

            p0 = e0 / sum;
            p1 = e1 / sum;
            p2 = e2 / sum;
        }
    }
}
