namespace ProjectManager.API.Services.LexorankService
{
    public interface ILexorankService
    {
        string GetInitialPosition(string? lastPosition, int bucket = 0);
        string GetMiddle(string? prevPosition, string? nextPosition, int bucket = 0);
        bool NeedsRebalancing(string position);
        bool HasCollision(string pos1, string pos2);
        int GetBucket(string position);
        int GetNextBucket(int currentBucket);
        List<string> RebalancePositions(int count, int bucket);
    }
}
