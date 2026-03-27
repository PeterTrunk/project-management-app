using System.Text;

namespace ProjectManager.API.Services.LexorankService
{
    public class LexorankService : ILexorankService
    {
        private const string BASE36_CHARS = "0123456789abcdefghijklmnopqrstuvwxyz";
        private const int BASE = 36;
        private const int BUCKET_COUNT = 3;
        private const int MAX_POSITION_LENGTH = 50;
        private const char MIDDLE_CHAR = 'i'; // 18-as karakter, Base36 közepe

        // Kezdeti pozíció új taskhoz egy oszlop végére
        public string GetInitialPosition(string? lastPosition, int bucket = 0)
        {
            if (lastPosition == null)
                return $"{bucket}|{MIDDLE_CHAR}";

            var pos = GetPositionPart(lastPosition);
            return $"{bucket}|{IncrementPosition(pos)}";
        }

        // Közbeszúrás két pozíció közé
        public string GetMiddle(string? prevPosition, string? nextPosition, int bucket = 0)
        {
            // Első helyre kerül
            if (prevPosition == null && nextPosition == null)
                return $"{bucket}|{MIDDLE_CHAR}";

            if (prevPosition == null)
            {
                var nextPos = GetPositionPart(nextPosition!);
                return $"{bucket}|{GetBefore(nextPos)}";
            }

            if (nextPosition == null)
            {
                var prevPos = GetPositionPart(prevPosition);
                return $"{bucket}|{IncrementPosition(prevPos)}";
            }

            var prev = GetPositionPart(prevPosition);
            var next = GetPositionPart(nextPosition);

            return $"{bucket}|{GetBetween(prev, next)}";
        }

        // Kell-e rebalancing?
        public bool NeedsRebalancing(string position)
        {
            return GetPositionPart(position).Length > MAX_POSITION_LENGTH;
        }

        // Ütközés detektálás
        public bool HasCollision(string pos1, string pos2)
        {
            return pos1 == pos2;
        }

        // Bucket kinyerése
        public int GetBucket(string position)
        {
            return int.Parse(position.Split('|')[0]);
        }

        // Következő bucket
        public int GetNextBucket(int currentBucket)
        {
            return (currentBucket + 1) % BUCKET_COUNT;
        }

        // Teljes oszlop rebalancingelése
        public List<string> RebalancePositions(int count, int bucket)
        {
            var positions = new List<string>();

            // Egyenletes elosztás a Base36 skálán
            var step = (double)BASE / (count + 1);

            for (int i = 1; i <= count; i++)
            {
                var charIndex = (int)(step * i);
                charIndex = Math.Clamp(charIndex, 0, BASE - 1);
                positions.Add($"{bucket}|{BASE36_CHARS[charIndex]}");
            }

            return positions;
        }

        // Helper metódusok

        private string GetPositionPart(string position)
        {
            var parts = position.Split('|');
            return parts.Length > 1 ? parts[1] : parts[0];
        }

        private string GetBetween(string prev, string next)
        {
            // Azonos hosszra padding
            var maxLen = Math.Max(prev.Length, next.Length);
            var paddedPrev = prev.PadRight(maxLen, '0');
            var paddedNext = next.PadRight(maxLen, '0');

            var prevVal = ToBase10(paddedPrev);
            var nextVal = ToBase10(paddedNext);

            if (nextVal - prevVal > 1)
            {
                // Van hely közbeszúrásra
                var midVal = (prevVal + nextVal) / 2;
                return ToBase36(midVal, maxLen);
            }
            else
            {
                // Nincs hely -> string hosszabbítás
                return prev + MIDDLE_CHAR;
            }
        }

        private string GetBefore(string next)
        {
            var val = ToBase10(next);
            if (val > 0)
                return ToBase36(val / 2, next.Length);

            // Már a minimum -> prefix hozzáadása
            return "0" + next;
        }

        private string IncrementPosition(string pos)
        {
            var val = ToBase10(pos);
            return ToBase36(val + 1, pos.Length);
        }

        private long ToBase10(string base36)
        {
            long result = 0;
            foreach (char c in base36)
            {
                result = result * BASE + BASE36_CHARS.IndexOf(c);
            }
            return result;
        }

        private string ToBase36(long value, int minLength = 1)
        {
            if (value == 0)
                return "0".PadLeft(minLength, '0');

            var result = new StringBuilder();
            while (value > 0)
            {
                result.Insert(0, BASE36_CHARS[(int)(value % BASE)]);
                value /= BASE;
            }

            var str = result.ToString();
            return str.PadLeft(Math.Max(minLength, str.Length), '0');
        }
    }
}
