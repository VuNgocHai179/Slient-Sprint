using UnityEngine;

namespace SilentSprint.Gameplay.AI
{
    /// <summary>
    /// Bo cuc vector observation (16 chieu) ma <see cref="MonsterBrain"/> dua vao model ONNX.
    ///
    /// THU TU VA Y NGHIA TUNG CHIEU PHAI KHOP TUYET DOI voi
    /// <c>Tools/MonsterBrain/generate_monster_brain.py</c> (mang OBS_NAMES).
    /// Neu doi thu tu o day thi phai train lai model.
    /// </summary>
    public static class MonsterObservationLayout
    {
        public const int Size = 16;

        public const int DistNorm = 0;
        public const int AngleNorm = 1;
        public const int LosClear = 2;
        public const int InSightCone = 3;
        public const int RelDirX = 4;
        public const int RelDirZ = 5;
        public const int PlayerSpeedNorm = 6;
        public const int PlayerNoise = 7;
        public const int StaminaNorm = 8;
        public const int FlashlightOn = 9;
        public const int Proximity = 10;
        public const int TimeSinceSeen = 11;
        public const int HasLastKnown = 12;
        public const int LastKnownDist = 13;
        public const int PrevState = 14;
        public const int Bias = 15;

        /// <summary>Ten tung chieu, dung cho log/debug.</summary>
        public static readonly string[] Names =
        {
            "distNorm", "angleNorm", "losClear", "inSightCone",
            "relDirX", "relDirZ", "playerSpeedNorm", "playerNoise",
            "staminaNorm", "flashlightOn", "proximity", "timeSinceSeen",
            "hasLastKnown", "lastKnownDist", "prevState", "bias",
        };
    }

    /// <summary>
    /// "Giac quan" cua quai tai mot thoi diem: tat ca nhung gi model duoc phep biet.
    /// Khong chua logic hanh vi - chi chua du lieu.
    /// </summary>
    public struct MonsterObservation
    {
        /// <summary>Khoang cach toi player / tam nhin hieu dung (0..2).</summary>
        public float DistNorm;
        /// <summary>Goc lech so voi huong mat, chia 180 (0..1).</summary>
        public float AngleNorm;
        /// <summary>1 neu tia tu mat toi player khong bi vat can chan.</summary>
        public float LosClear;
        /// <summary>1 neu player nam trong hinh non thi giac.</summary>
        public float InSightCone;
        /// <summary>Huong toi player trong khong gian local cua quai (truc X).</summary>
        public float RelDirX;
        /// <summary>Huong toi player trong khong gian local cua quai (truc Z, tien ve phia truoc).</summary>
        public float RelDirZ;
        /// <summary>Toc do player / runSpeed (0..1).</summary>
        public float PlayerSpeedNorm;
        /// <summary>Do on: 0 dung yen, 0.4 di bo, 1.0 chay.</summary>
        public float PlayerNoise;
        /// <summary>The luc con lai cua player (0..1).</summary>
        public float StaminaNorm;
        /// <summary>1 neu den pin cua player dang bat.</summary>
        public float FlashlightOn;
        /// <summary>Khoang cach / hearingDistance (0..1).</summary>
        public float Proximity;
        /// <summary>Thoi gian ke tu lan cuoi thay player, chia 8 (0..1).</summary>
        public float TimeSinceSeen;
        /// <summary>1 neu quai con nho vi tri cuoi cung cua player.</summary>
        public float HasLastKnown;
        /// <summary>Khoang cach tu quai toi vi tri nho duoc / stalkRadius.</summary>
        public float LastKnownDist;
        /// <summary>Chi so state truoc do / 2 (0, 0.5 hoac 1).</summary>
        public float PrevState;

        /// <summary>Quai co dang thuc su nhin thay player khong (giong ham muc tieu cua teacher).</summary>
        public bool PlayerVisible => LosClear > 0.5f && InSightCone > 0.5f && DistNorm <= 1f;

        /// <summary>Ghi 16 gia tri vao buffer theo dung thu tu <see cref="MonsterObservationLayout"/>.</summary>
        public void WriteTo(float[] buffer)
        {
            buffer[MonsterObservationLayout.DistNorm] = DistNorm;
            buffer[MonsterObservationLayout.AngleNorm] = AngleNorm;
            buffer[MonsterObservationLayout.LosClear] = LosClear;
            buffer[MonsterObservationLayout.InSightCone] = InSightCone;
            buffer[MonsterObservationLayout.RelDirX] = RelDirX;
            buffer[MonsterObservationLayout.RelDirZ] = RelDirZ;
            buffer[MonsterObservationLayout.PlayerSpeedNorm] = PlayerSpeedNorm;
            buffer[MonsterObservationLayout.PlayerNoise] = PlayerNoise;
            buffer[MonsterObservationLayout.StaminaNorm] = StaminaNorm;
            buffer[MonsterObservationLayout.FlashlightOn] = FlashlightOn;
            buffer[MonsterObservationLayout.Proximity] = Proximity;
            buffer[MonsterObservationLayout.TimeSinceSeen] = TimeSinceSeen;
            buffer[MonsterObservationLayout.HasLastKnown] = HasLastKnown;
            buffer[MonsterObservationLayout.LastKnownDist] = LastKnownDist;
            buffer[MonsterObservationLayout.PrevState] = PrevState;
            buffer[MonsterObservationLayout.Bias] = 1f;
        }

        public override string ToString()
        {
            float[] buffer = new float[MonsterObservationLayout.Size];
            WriteTo(buffer);
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < buffer.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(MonsterObservationLayout.Names[i]).Append('=').Append(buffer[i].ToString("F3"));
            }
            return sb.ToString();
        }
    }

    /// <summary>Ten cac input/output cua model ONNX. Phai khop <c>build_onnx()</c> trong script Python.</summary>
    public static class MonsterBrainOutputLayout
    {
        public const string InputName = "observation";
        public const string StateProbsName = "state_probs";
        public const string IntentsName = "intents";

        /// <summary>So chieu dau ra cua head chon trang thai (patrol, investigate, chase).</summary>
        public const int StateCount = 3;
        /// <summary>So chieu dau ra cua head "tinh cach" (aggression, searchRadius, flank, alertness).</summary>
        public const int IntentCount = 4;

        public const int StatePatrol = 0;
        public const int StateInvestigate = 1;
        public const int StateChase = 2;

        public const int IntentAggression = 0;
        public const int IntentSearchRadius = 1;
        public const int IntentFlank = 2;
        public const int IntentAlertness = 3;
    }
}
