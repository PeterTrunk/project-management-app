using System.Numerics;
using System.Text;

namespace ProjectManager.API.Services.LexorankService
{
    public class LexorankService : ILexorankService
    {
        private const string BASE36_CHARS = "0123456789abcdefghijklmnopqrstuvwxyz";
        private const int BASE = 36;
        private const int BUCKET_COUNT = 3;
        private const int MAX_POSITION_LENGTH = 50;
        private const int REBALANCE_POSITION_LENGTH = 6;
        private const char MIDDLE_CHAR = 'i'; // 18-as karakter, Base36 közepe

        // Kezdeti pozíció új taskhoz egy oszlop végére
        public string GetInitialPosition(string? lastPosition, int bucket = 0)
        {
            if (lastPosition == null)
                return $"{bucket}|{MIDDLE_CHAR}";

            // Meglévő pozíció bucketjét vesszük át!
            bucket = GetBucket(lastPosition);
            var pos = GetPositionPart(lastPosition);
            return $"{bucket}|{IncrementPosition(pos)}";
        }

        // Közbeszúrás két pozíció közé
        public string GetMiddle(string? prevPosition, string? nextPosition, int bucket = 0)
        {
            // Ha van prev vagy next, vegyük át a bucketjüket
            if (prevPosition != null)
                bucket = GetBucket(prevPosition);
            else if (nextPosition != null)
                bucket = GetBucket(nextPosition);

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

            var total = BigInteger.Pow(BASE, REBALANCE_POSITION_LENGTH);
            var step = total / (count + 1);

            for (int i = 1; i <= count; i++)
            {
                var val = step * i;
                positions.Add($"{bucket}|{ToBigBase36(val, REBALANCE_POSITION_LENGTH)}");
            }

            return positions;
        }

        // --- Helper metódusok ---

        private string GetPositionPart(string position)
        {
            var parts = position.Split('|');
            return parts.Length > 1 ? parts[1] : parts[0];
        }

        // String hozzáfűzés helyett hossz-növeléses BigInteger interpoláció
        private string GetBetween(string prev, string next)
        {
            var len = Math.Max(prev.Length, next.Length);

            while (len <= MAX_POSITION_LENGTH)
            {
                var paddedPrev = prev.PadRight(len, '0');
                var paddedNext = next.PadRight(len, '0');

                var prevVal = ToBigInt(paddedPrev);
                var nextVal = ToBigInt(paddedNext);

                if (nextVal - prevVal > 1)
                {
                    var midVal = (prevVal + nextVal) / 2;
                    return ToBigBase36(midVal, len);
                }

                len++;
            }

            throw new InvalidOperationException(
                $"Pozíció kimerült '{prev}' és '{next}' között. Rebalancing szükséges.");
        }

        private string GetBefore(string next)
        {
            var val = ToBigInt(next);
            if (val > 1)
                return ToBigBase36(val / 2, next.Length);

            return "0" + next;
        }

        private string IncrementPosition(string pos)
        {
            var val = ToBigInt(pos);
            return ToBigBase36(val + 1, pos.Length);
        }

        private BigInteger ToBigInt(string base36)
        {
            BigInteger result = 0;
            foreach (char c in base36.ToLower())
                result = result * BASE + BASE36_CHARS.IndexOf(c);
            return result;
        }

        private string ToBigBase36(BigInteger value, int minLength = 1)
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
